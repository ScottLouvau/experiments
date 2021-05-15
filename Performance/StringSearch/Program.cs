using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/* Goals:
 *   - Very fast compared to Parallel File.ReadAllText and string.IndexOf.
 *   - Constrain memory use (1 MB / core or less).
 *   - Minimize unused bytes read (identify binaries with prefix read only).
 *   - Minimize disk read count (read most useful files in one read, and nearly all in few more)
 *   - Identify text that's non-UTF8 and fall back
 *   - Find line, charInLine, and context string efficiently (line with match with <= 100 chars before and after)
 */

/* Plan:
 *   - Skip file extensions: "" | ".dll" | ".exe"
 *     - Avoids 33% of files (mostly git object files with no extension)
 *   - Load first 32 KB and classify.
 *     - Look for UTF-8 and UTF-16/32 BOMs, UTF-16/32 first characters (0x00 in alternating bytes)
 *       - For text that's not UTF-8, need .NET fallback.
 *     - Look for zlib (Git obj), zip prefixes (Maybe dll/exe, jpg, but both have early 0xFF)
 *     - Decide whether to reject early 0x00 (*very* common in binary files, but also legal UTF-8)
 *       - Only screen adjacent 0x00?
 *     - Screen for >= 0xF5 or UTF-8 validity in first N bytes.
 *       - Unsure whether 0xF5 is enough, or if check for leading/trailing byte patterns also important.
 *   - Search first 32 KB for value.
 *   - If file longer:
 *     - Vector newline count and last line char count as-you-go if "free".
 *     - If not, re-read file after first match found to map to line and char.
 *     - Read in 512 KB blocks and continue search (shift last FindValue.Length - 1 bytes to find straddling matches).
 * 
 */

namespace StringSearch
{
    public class Match
    {
        public string FilePath { get; set; }
        public int ByteIndex { get; set; }

        public long FileLength { get; set; }
        public Encoding Encoding { get; set; }
    }


    /* Pending Experiments / Questions:
     *  - Is filtering by file extension drastically cheaper than sniffing?
     *  - How many files and MB are filtered?
     *  - How early on are files filtered?
     *  - How many UTF-16 and UTF-32 files are found? Do they all have a BOM?
     */

    /// <summary>
    ///  Learnings:
    ///   - Cold times are much longer (OS Defender file scanning most likely).
    ///   - 'Normal' search = File.ReadAllText and string.IndexOf(Ordinal)
    ///      - Runtime is 75% string.IndexOf, 25% File.ReadAllText
    ///   - 'Fast' search = File.ReadAllBytes and Span.IndexOf.
    ///      - Runtime is 62% File.ReadAllBytes, 26% Directory.GetFiles, 11% Span.IndexOf.
    ///      
    ///   - 'Normal' is 40x slower than 'Fast' overall
    ///   - File.ReadAllBytes is 14x faster than File.ReadAllText.
    ///   - File.ReadAllBytes is as fast as ArrayPool + FileStream.Read(Span)
    ///   
    ///   - Reading 1 KB - 32 KB of each file all have the same cost (should read a 32KB prefix).
    ///   - Reading 32 KB of binary files saves 90% of bytes read.
    ///   - Only 1.5% of my text files are over 32KB, so 98.5% will still only be one read call.
    ///   - Large text files are often XML Doc (.xml), JavaScript libraries (.js), JSON data (.json), Logs (.log, .txt), or text data (.csv, .tsv).
    ///   - Only 4 of 10k files were detected non-UTF8 by FileStream.CurrentEncoding (all UTF-16 LE w/0xFF 0xFE BOM)
    ///   
    ///   - Replicate StreamReader.DetectEncoding for BOMs and UTF-8 validate prefix of file.
    ///      - StreamReader to detect encoding but then FileStream.Read is messy (block reading in StreamReader)
    ///      - UTF-16 and UTF-32 BOMs contain 0xFF, but the BOM isn't required.
    ///      - Git Object, ZIP, JPG, PNG, PACK, EXE, and DLL files are the most common binary files.
    ///        - Git Object: No extension, 0x78, then 0x01 or 0x9C or 0xDA. Git always 0x01?
    ///        - DLL/EXE: 0xFF 13 bytes in, in DOS Stub.
    ///        - PACK: 'PACK', then zero bytes, > 0xF5 common 25 bytes in.
    ///        - ZIP: 'PK' then 0x03 0x04
    ///        - JPG: 0xFF 0xD8 start.
    ///        - PNG: 0x89 'PNG' 
    ///        - DB: 'SQLite format 3' 0x00
    ///   
    ///   
    ///   - C:\Code, *.*: 9,585 files, 848 MB, 10 iterations.
    ///     - Default Search:             160.846 sec
    ///     - Enumerate only:               1.137 sec
    ///     - Enum and ReadAllBytes only:   3.789 sec
    ///     - Enum and ReadAllText only:   51.323 sec
    ///     - Enum, Read, Span.IndexOf:     4.250 sec
    ///     
    /// 
    ///   
    ///  TODO:
    ///   - Compare default to Utf8 matches and figure out why some are missing (hopefully UTF-16/32 files).
    ///     - Can I detect those files? (BOMs?)
    ///   - Easy way filter out binaries? (At least .exe and .dll?)
    ///     - How many have a byte > 0xF5? How early?
    ///     - How many bytes to check if looking for invalid sequences (>= 0x80 then wrong trailing count) instead? Almost always within 1KB?
    ///  
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            string valueToFind = args[0];
            string directoryToSearch = args[1];
            string fileExtensions = (args.Length > 2 ? args[2] : "*.*");

            Console.WriteLine($"Searching for \"{valueToFind}\" in '{directoryToSearch}'...");
            Stopwatch w = Stopwatch.StartNew();

            List<Match> matches = null;

            for (int i = 0; i < 10; ++i)
            {
                //matches = SearchDefault(valueToFind, directoryToSearch, fileExtensions);
                //matches = SearchUtf8Bytes(valueToFind, directoryToSearch, fileExtensions);
                //matches = EnumFilesOnly(valueToFind, directoryToSearch, fileExtensions);
                //matches = LoadFilesOnly(valueToFind, directoryToSearch, fileExtensions);
                //matches = LoadFilesOnlyV2(valueToFind, directoryToSearch, fileExtensions
                //matches = LoadFileTextOnly(valueToFind, directoryToSearch, fileExtensions);
                //matches = SearchUtf8BytesV2(valueToFind, directoryToSearch, fileExtensions);
                //matches = SearchUtf8BytesV3(valueToFind, directoryToSearch, fileExtensions);
                //matches = SniffFilesOnly(valueToFind, directoryToSearch, fileExtensions);
                //matches = LoadFilePrefixes(directoryToSearch, fileExtensions, 32 * 1024);
                matches = LoadFilePrefixesFilterExtension(directoryToSearch, fileExtensions, 32 * 1024);
            }
            Console.WriteLine($"{FilteredCount:n0} Filtered");
            //matches = LoadFilesAndEncoding(valueToFind, directoryToSearch, fileExtensions);

            Console.WriteLine($"Found {matches.Count:n0} matches in {w.Elapsed.TotalSeconds:n3} sec.");

            foreach (Match m in matches.Take(20))
            {
                Console.WriteLine($"{m.FilePath} @ {m.ByteIndex:n0} {m.Encoding.EncodingName}");
            }
        }

        static List<Match> SearchDefault(string valueToFind, string directoryToSearch, string fileExtensions)
        {
            List<Match> matches = new List<Match>();

            string[] filePathsToSearch = Directory.GetFiles(directoryToSearch, fileExtensions, SearchOption.AllDirectories);
            Parallel.ForEach(filePathsToSearch, (path) =>
            {
                List<Match> fileMatches = null;

                string text = File.ReadAllText(path);
                int startIndex = 0;

                while (true)
                {
                    int matchIndex = text.IndexOf(valueToFind, startIndex);
                    if (matchIndex == -1) { break; }

                    if (fileMatches == null) { fileMatches = new List<Match>(); }
                    fileMatches.Add(new Match() { FilePath = path, ByteIndex = matchIndex });

                    startIndex = matchIndex + 1;
                }

                if (fileMatches != null)
                {
                    lock (matches)
                    {
                        matches.AddRange(fileMatches);
                    }
                }
            });

            return matches;
        }

        static List<Match> SearchUtf8Bytes(string valueToFind, string directoryToSearch, string fileExtensions)
        {
            List<Match> matches = new List<Match>();

            byte[] bytesToFind = Encoding.UTF8.GetBytes(valueToFind);
            string[] filePathsToSearch = Directory.GetFiles(directoryToSearch, fileExtensions, SearchOption.AllDirectories);
            Parallel.ForEach(filePathsToSearch, (path) =>
            {
                List<Match> fileMatches = null;

                Span<byte> contents = File.ReadAllBytes(path);
                int startIndex = 0;

                while (true)
                {
                    int matchIndex = contents.IndexOf(bytesToFind);
                    if (matchIndex == -1) { break; }

                    if (fileMatches == null) { fileMatches = new List<Match>(); }
                    fileMatches.Add(new Match() { FilePath = path, ByteIndex = startIndex + matchIndex });

                    startIndex = matchIndex + 1;
                    contents = contents.Slice(matchIndex + 1);
                }

                if (fileMatches != null)
                {
                    lock (matches)
                    {
                        matches.AddRange(fileMatches);
                    }
                }
            });

            return matches;
        }

        static List<Match> EnumFilesOnly(string valueToFind, string directoryToSearch, string fileExtensions)
        {
            string[] filePathsToSearch = Directory.GetFiles(directoryToSearch, fileExtensions, SearchOption.AllDirectories);
            return new List<Match>();
        }

        static List<Match> LoadFilesOnly(string valueToFind, string directoryToSearch, string fileExtensions)
        {
            List<Match> matches = new List<Match>();

            string[] filePathsToSearch = Directory.GetFiles(directoryToSearch, fileExtensions, SearchOption.AllDirectories);
            Parallel.ForEach(filePathsToSearch, (path) =>
            {
                Span<byte> contents = File.ReadAllBytes(path);
            });

            return matches;
        }

        static List<Match> LoadFileTextOnly(string valueToFind, string directoryToSearch, string fileExtensions)
        {
            List<Match> matches = new List<Match>();

            string[] filePathsToSearch = Directory.GetFiles(directoryToSearch, fileExtensions, SearchOption.AllDirectories);
            Parallel.ForEach(filePathsToSearch, (path) =>
            {
                string text = File.ReadAllText(path);
            });

            return matches;
        }

        static List<Match> LoadFilesOnlyV2(string valueToFind, string directoryToSearch, string fileExtensions)
        {
            List<Match> matches = new List<Match>();
            ArrayPool<byte> pool = ArrayPool<byte>.Shared;

            string[] filePathsToSearch = Directory.GetFiles(directoryToSearch, fileExtensions, SearchOption.AllDirectories);
            Parallel.ForEach(filePathsToSearch, (path) =>
            {
                using (var stream = File.OpenRead(path))
                {
                    byte[] buffer = pool.Rent((int)stream.Length);

                    Span<byte> contents = buffer;
                    int usedLength = stream.Read(contents);
                    contents = contents.Slice(0, usedLength);
                }
            });

            return matches;
        }

        static List<Match> LoadFilePrefixes(string directoryToSearch, string fileExtensions, int prefixBytes)
        {
            List<Match> matches = new List<Match>();
            ArrayPool<byte> pool = ArrayPool<byte>.Shared;

            string[] filePathsToSearch = Directory.GetFiles(directoryToSearch, fileExtensions, SearchOption.AllDirectories);
            Parallel.ForEach(filePathsToSearch, (path) =>
            {
                using (var stream = File.OpenRead(path))
                {
                    int prefixLength = Math.Min((int)stream.Length, prefixBytes);
                    byte[] buffer = pool.Rent((int)stream.Length);

                    Span<byte> contents = buffer;
                    int usedLength = stream.Read(contents.Slice(0, prefixLength));
                    contents = contents.Slice(0, usedLength);
                }
            });

            return matches;
        }

        static int FilteredCount = 0;

        static List<Match> LoadFilePrefixesFilterExtension(string directoryToSearch, string fileExtensions, int prefixBytes)
        {
            List<Match> matches = new List<Match>();
            ArrayPool<byte> pool = ArrayPool<byte>.Shared;

            string[] filePathsToSearch = Directory.GetFiles(directoryToSearch, fileExtensions, SearchOption.AllDirectories);
            Parallel.ForEach(filePathsToSearch, (path) =>
            {
                string extension = Path.GetExtension(path).ToLowerInvariant();

                if (!(extension == ""))// || extension == ".dll" || extension == ".exe")) // || extension == ".pack" || extension == ".zip" || extension == ".jpg" || extension == ".png" ))
                {
                    using (var stream = File.OpenRead(path))
                    {
                        int prefixLength = Math.Min((int)stream.Length, prefixBytes);
                        byte[] buffer = pool.Rent((int)stream.Length);

                        Span<byte> contents = buffer;
                        int usedLength = stream.Read(contents.Slice(0, prefixLength));
                        contents = contents.Slice(0, usedLength);
                    }
                }
                else
                {
                    Interlocked.Increment(ref FilteredCount);
                }
            });

            return matches;
        }

        static List<Match> LoadFilesAndEncoding(string valueToFind, string directoryToSearch, string fileExtensions)
        {
            List<Match> matches = new List<Match>();
            ArrayPool<byte> pool = ArrayPool<byte>.Shared;

            string[] filePathsToSearch = Directory.GetFiles(directoryToSearch, fileExtensions, SearchOption.AllDirectories);
            Parallel.ForEach(filePathsToSearch, (path) =>
            {
                using (var streamReader = File.OpenText(path))
                {
                    string text = streamReader.ReadToEnd();
                    Encoding encoding = streamReader.CurrentEncoding;

                    if (encoding != Encoding.UTF8)
                    {
                        lock (matches)
                        {
                            matches.Add(new Match() { FilePath = path, FileLength = streamReader.BaseStream.Length, Encoding = encoding });
                        }
                    }
                }
            });

            return matches;
        }

        static List<Match> SearchUtf8BytesV2(string valueToFind, string directoryToSearch, string fileExtensions)
        {
            List<Match> matches = new List<Match>();
            ArrayPool<byte> pool = ArrayPool<byte>.Shared;

            byte[] bytesToFind = Encoding.UTF8.GetBytes(valueToFind);
            string[] filePathsToSearch = Directory.GetFiles(directoryToSearch, fileExtensions, SearchOption.AllDirectories);
            Parallel.ForEach(filePathsToSearch, (path) =>
            {
                Span<byte> contents = null;

                using (var stream = File.OpenRead(path))
                {
                    byte[] buffer = pool.Rent((int)stream.Length);

                    contents = buffer;
                    int usedLength = stream.Read(contents);
                    contents = contents.Slice(0, usedLength);
                }

                List<Match> fileMatches = null;
                int startIndex = 0;
                while (true)
                {
                    int matchIndex = contents.IndexOf(bytesToFind);
                    if (matchIndex == -1) { break; }

                    if (fileMatches == null) { fileMatches = new List<Match>(); }
                    fileMatches.Add(new Match() { FilePath = path, ByteIndex = startIndex + matchIndex });

                    startIndex = matchIndex + 1;
                    contents = contents.Slice(matchIndex + 1);
                }

                if (fileMatches != null)
                {
                    lock (matches)
                    {
                        matches.AddRange(fileMatches);
                    }
                }
            });

            return matches;
        }

        static List<Match> SearchUtf8BytesV3(string valueToFind, string directoryToSearch, string fileExtensions)
        {
            List<Match> matches = new List<Match>();
            ArrayPool<byte> pool = ArrayPool<byte>.Shared;

            byte[] bytesToFind = Encoding.UTF8.GetBytes(valueToFind);
            string[] filePathsToSearch = Directory.GetFiles(directoryToSearch, fileExtensions, SearchOption.AllDirectories);
            Parallel.ForEach(filePathsToSearch, (path) =>
            {
                Span<byte> contents = null;

                using (var stream = File.OpenRead(path))
                {
                    byte[] buffer = pool.Rent((int)stream.Length);

                    contents = buffer;
                    int usedLength = stream.Read(contents);
                    contents = contents.Slice(0, usedLength);
                }

                // Check for encoding in first 1024 bytes
                FileEncoding encoding = EncodingScanner.Identify(contents.Slice(0, Math.Min(contents.Length, 1024)), true);
                if (encoding == FileEncoding.UTF8 || encoding == FileEncoding.PossibleUTF8)
                {
                    List<Match> fileMatches = null;
                    int startIndex = 0;
                    while (true)
                    {
                        int matchIndex = contents.IndexOf(bytesToFind);
                        if (matchIndex == -1) { break; }

                        if (fileMatches == null) { fileMatches = new List<Match>(); }
                        fileMatches.Add(new Match() { FilePath = path, ByteIndex = startIndex + matchIndex });

                        startIndex = matchIndex + 1;
                        contents = contents.Slice(matchIndex + 1);
                    }

                    if (fileMatches != null)
                    {
                        lock (matches)
                        {
                            matches.AddRange(fileMatches);
                        }
                    }
                }
            });

            return matches;
        }

        static List<Match> SniffFilesOnly(string valueToFind, string directoryToSearch, string fileExtensions)
        {
            List<Match> matches = new List<Match>();
            ArrayPool<byte> pool = ArrayPool<byte>.Shared;

            byte[] bytesToFind = Encoding.UTF8.GetBytes(valueToFind);
            string[] filePathsToSearch = Directory.GetFiles(directoryToSearch, fileExtensions, SearchOption.AllDirectories);
            Parallel.ForEach(filePathsToSearch, (path) =>
            {
                Span<byte> contents = null;
                FileEncoding encoding = FileEncoding.PossibleUTF8;

                using (var stream = File.OpenRead(path))
                {
                    byte[] buffer = pool.Rent((int)stream.Length);

                    int prefixLength = Math.Min(buffer.Length, 1024);

                    contents = buffer;
                    stream.Read(contents.Slice(0, prefixLength));

                    encoding = EncodingScanner.Identify(contents.Slice(0, prefixLength), true);

                    if (encoding == FileEncoding.UTF8 || encoding == FileEncoding.PossibleUTF8)
                    {
                        int remainingLength = stream.Read(contents.Slice(prefixLength));
                        contents = contents.Slice(0, prefixLength + remainingLength);
                    }
                }
            });

            return matches;
        }
    }
}
