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
        public int MatchByteIndex { get; set; }
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
    ///      - Runtime is all File.ReadAllText (1.4% string.IndexOf(ordinal))
    ///      - With default string.IndexOf, runtime is 75% string.IndexOf(default), 25% File.ReadAllText
    ///    - 'Fast' search = File.ReadAllBytes and Span.IndexOf.
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
    ///   - C:\Code, *.*: 9,585 files, 848 MB, 5 iterations.
    ///     - Default Search:              17.913 sec
    ///     - Enumerate only:               0.586 sec
    ///     - Enum and ReadAllBytes only:   1.788 sec
    ///     - Enum and ReadAllText only:   16.948 sec
    ///     - Enum, Read, Span.IndexOf:     1.952 sec
    ///     
    ///   - 1,888 UTF-8 with BOM
    ///   - 3,522 UTF-8 (byte check)
    ///   - Filtered Out: 4,142 (32 KB); 4,135 (1 KB); 4,123 (256 b); 4,106 (64 b)
    ///     - Filtered (2b check): 4,141 (32 KB); 4,133 (1 KB)
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
            byte[] bytesToFind = Encoding.UTF8.GetBytes(valueToFind);
            string directoryToSearch = Path.GetFullPath((args.Length > 1 ? args[1] : Environment.CurrentDirectory));
            string searchPattern = (args.Length > 2 ? args[2] : "*.*");

            Console.WriteLine($"Searching for \"{valueToFind}\" in '{directoryToSearch}'...");
            Stopwatch w = Stopwatch.StartNew();

            List<Match> matches = null;

            for (int i = 0; i < 5; ++i)
            {
                //matches = SearchParallel(directoryToSearch, searchPattern, (path) => FindDefault(valueToFind, path));
                //matches = SearchParallel(directoryToSearch, searchPattern, (path) => FindUtf8(bytesToFind, path));

                matches = SearchParallel(directoryToSearch, searchPattern, (path) => WithExtensionFilter(path, (path) => FindUtf8(bytesToFind, path)));
                //matches = SearchParallel(directoryToSearch, searchPattern, (path) => WithExtensionFilter(path, (path) => WithFileIdentification(path, valueToFind, bytesToFind, 64 * 1024, 1024)));

                // Enumerate only
                //matches = SearchParallel(directoryToSearch, searchPattern, (path) => null);

                // Enumerate and File.ReadAllText only
                //matches = SearchParallel(directoryToSearch, searchPattern, (path) => { File.ReadAllText(path); return null; });

                // Enumerate and File.ReadAllBytes only
                //matches = SearchParallel(directoryToSearch, searchPattern, (path) => { File.ReadAllBytes(path); return null; });

                //matches = SearchUtf8BytesV2(valueToFind, directoryToSearch, searchPattern);
                //matches = SearchUtf8BytesV3(valueToFind, directoryToSearch, searchPattern);
                //matches = SniffFilesOnly(valueToFind, directoryToSearch, searchPattern);
                //matches = LoadFilePrefixes(directoryToSearch, searchPattern, 32 * 1024);
                //matches = LoadFilePrefixesFilterExtension(directoryToSearch, searchPattern, 32 * 1024);
            }

            Console.WriteLine($"Found {matches.Count:n0} matches in {w.Elapsed.TotalSeconds:n3} sec.");

            foreach (Match m in matches.Take(20))
            {
                Console.WriteLine($"{m.FilePath} @ {m.MatchByteIndex:n0}");
            }
        }

        static List<Match> SearchParallel(string directoryToSearch, string searchPattern, Func<string, List<Match>> searchInFile)
        {
            List<Match> matches = new List<Match>();
            string[] filePathsToSearch = Directory.GetFiles(directoryToSearch, searchPattern, SearchOption.AllDirectories);

            Parallel.ForEach(filePathsToSearch, (path) =>
            {
                List<Match> fileMatches = searchInFile(path);

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

        static List<Match> FindDefault(string valueToFind, string filePath)
        {
            List<Match> matches = null;
            string text = File.ReadAllText(filePath);

            int startIndex = 0;

            while (true)
            {
                int matchIndex = text.IndexOf(valueToFind, startIndex, StringComparison.Ordinal);
                if (matchIndex == -1) { break; }

                matches ??= new List<Match>();
                matches.Add(new Match() { FilePath = filePath, MatchByteIndex = matchIndex });

                startIndex = matchIndex + 1;
            }

            return matches;
        }

        static List<Match> FindUtf8(Span<byte> valueToFind, string filePath)
        {
            return FindUtf8(valueToFind, filePath, File.ReadAllBytes(filePath));
        }

        static List<Match> FindUtf8(Span<byte> valueToFind, string filePath, Span<byte> contents)
        {
            List<Match> matches = null;

            int startIndex = 0;

            while (true)
            {
                int matchIndex = contents.IndexOf(valueToFind);
                if (matchIndex == -1) { break; }

                if (matches == null) { matches = new List<Match>(); }
                matches.Add(new Match() { FilePath = filePath, MatchByteIndex = startIndex + matchIndex });

                startIndex = matchIndex + 1;
                contents = contents.Slice(matchIndex + 1);
            }

            return matches;
        }

        static List<Match> WithFileIdentification(string filePath, string valueToFind, Span<byte> bytesToFind, int prefixToLoad, int prefixToScan)
        {
            using (var stream = File.OpenRead(filePath))
            {
                int prefixLength = Math.Min((int)stream.Length, prefixToLoad);
                byte[] buffer = ArrayPool<byte>.Shared.Rent((int)stream.Length);

                Span<byte> contents = buffer;
                int prefixReadLength = stream.Read(contents.Slice(0, prefixLength));
                contents = contents.Slice(0, prefixReadLength);

                FileDetectionResult result = FileTypeScanner.Identify(contents.Slice(Math.Min(contents.Length, prefixToScan)));
                if (result.Type == FileTypeDetected.UTF8)
                {
                    contents = buffer;

                    int remainingLength = 0;
                    if (stream.Length > prefixReadLength)
                    {
                        remainingLength = stream.Read(contents.Slice(prefixReadLength));
                    }

                    contents = contents.Slice(result.BomByteCount, (prefixReadLength + remainingLength - result.BomByteCount));

                    return FindUtf8(bytesToFind, filePath, contents);
                }
                else if (result.Type == FileTypeDetected.UnicodeOther)
                {
                    return FindDefault(valueToFind, filePath);
                }
                else
                {
                    return null;
                }
            }
        }

        static List<Match> SearchUtf8Bytes(string valueToFind, string directoryToSearch, string searchPattern)
        {
            List<Match> matches = new List<Match>();

            byte[] bytesToFind = Encoding.UTF8.GetBytes(valueToFind);
            string[] filePathsToSearch = Directory.GetFiles(directoryToSearch, searchPattern, SearchOption.AllDirectories);
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
                    fileMatches.Add(new Match() { FilePath = path, MatchByteIndex = startIndex + matchIndex });

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

        static List<Match> LoadFilePrefixes(string directoryToSearch, string searchPattern, int prefixBytes)
        {
            List<Match> matches = new List<Match>();
            ArrayPool<byte> pool = ArrayPool<byte>.Shared;

            string[] filePathsToSearch = Directory.GetFiles(directoryToSearch, searchPattern, SearchOption.AllDirectories);
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

        static int ExtensionFilteredCount;
        static List<Match> WithExtensionFilter(string filePath, Func<string, List<Match>> next)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (extension == "" || extension == ".dll" || extension == ".exe" || extension == ".zip")
            {
                Interlocked.Increment(ref ExtensionFilteredCount);
                return null;
            }

            return next(filePath);
        }

        static List<Match> SearchUtf8BytesV2(string valueToFind, string directoryToSearch, string searchPattern)
        {
            List<Match> matches = new List<Match>();
            ArrayPool<byte> pool = ArrayPool<byte>.Shared;

            byte[] bytesToFind = Encoding.UTF8.GetBytes(valueToFind);
            string[] filePathsToSearch = Directory.GetFiles(directoryToSearch, searchPattern, SearchOption.AllDirectories);
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
                    fileMatches.Add(new Match() { FilePath = path, MatchByteIndex = startIndex + matchIndex });

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

        //static List<Match> SearchUtf8BytesV3(string valueToFind, string directoryToSearch, string searchPattern)
        //{
        //    List<Match> matches = new List<Match>();
        //    ArrayPool<byte> pool = ArrayPool<byte>.Shared;

        //    byte[] bytesToFind = Encoding.UTF8.GetBytes(valueToFind);
        //    string[] filePathsToSearch = Directory.GetFiles(directoryToSearch, searchPattern, SearchOption.AllDirectories);
        //    Parallel.ForEach(filePathsToSearch, (path) =>
        //    {
        //        Span<byte> contents = null;

        //        using (var stream = File.OpenRead(path))
        //        {
        //            byte[] buffer = pool.Rent((int)stream.Length);

        //            contents = buffer;
        //            int usedLength = stream.Read(contents);
        //            contents = contents.Slice(0, usedLength);
        //        }

        //        // Check for encoding in first 1024 bytes
        //        FileDetectionResult result = FileTypeScanner.Identify(contents.Slice(0, Math.Min(contents.Length, 1024)));
        //        if (typeDetected == FileTypeDetected.UTF8 || typeDetected == FileTypeDetected.PossibleUTF8)
        //        {
        //            List<Match> fileMatches = null;
        //            int startIndex = 0;
        //            while (true)
        //            {
        //                int matchIndex = contents.IndexOf(bytesToFind);
        //                if (matchIndex == -1) { break; }

        //                if (fileMatches == null) { fileMatches = new List<Match>(); }
        //                fileMatches.Add(new Match() { FilePath = path, MatchByteIndex = startIndex + matchIndex });

        //                startIndex = matchIndex + 1;
        //                contents = contents.Slice(matchIndex + 1);
        //            }

        //            if (fileMatches != null)
        //            {
        //                lock (matches)
        //                {
        //                    matches.AddRange(fileMatches);
        //                }
        //            }
        //        }
        //    });

        //    return matches;
        //}
    }
}
