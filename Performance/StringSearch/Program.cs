using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

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
 *   
 *   - Load 64 KB first.
 *     - Same time cost as 1 KB.
 *     - 99.5% of my text files are still just one read.
 *   
 *   - Classify text by first 1 KB.
 *     - Can skip ~90% of bytes in folder classifying the first 1 KB.
 *     - About 1/3 of my UTF-8 has a BOM.
 *     - UTF-16/32 identified by BOM. (It looks like FileStream does only this)
 *     - Common, large non-text files are quickly skipped (zip, zlib, exe, dll, png, jpg)
 *     - 99% of my invalid UTF-8 are found by byte pairs (continuation after single byte, continuation missing after multi-byte)
 *       - 4,142 filtered checking continuation bytes fully in first 32 KB.
 *       - 4,141 filtered checking byte pairs only.
 *       - 4,133 filtered checking byte pairs in first 1 KB only.
 *
 *   - Search for matches in 64 KB blocks.
 *     - If no match in first block, nothing else to do.
 *     - If match found, figure out line and character. 
 */

namespace StringSearch
{
    /// <summary>
    ///  Learnings:
    ///   - Cold times are much longer (OS Defender file scanning most likely).
    ///   - 'Normal' search = File.ReadAllText and string.IndexOf(Ordinal)
    ///      - Runtime is all File.ReadAllText (1.4% string.IndexOf(ordinal))
    ///      - With default string.IndexOf, runtime is 75% string.IndexOf(default), 25% File.ReadAllText
    ///    - 'Fast' search = File.ReadAllBytes and Span.IndexOf.
    ///      - Runtime is 62% File.ReadAllBytes, 26% Directory.GetFiles, 11% Span.IndexOf.
    ///      
    ///   - Reading 1 KB - 32 KB of each file all have the same cost (should read a 32KB prefix).
    ///   - Reading 32 KB of binary files saves 90% of bytes read.
    ///   - Only 1.5% of my text files are over 32KB, so 98.5% will still only be one read call.
    ///   - Large text files are often XML Doc (.xml), JavaScript libraries (.js), JSON data (.json), Logs (.log, .txt), or text data (.csv, .tsv).
    ///   - Only 4 of 10k files were detected non-UTF8 by FileStream.CurrentEncoding (all UTF-16 LE w/0xFF 0xFE BOM)
    ///   
    ///   - C:\Code, *.*: 9,585 files, 848 MB, 5 iterations.
    ///     - Default Search:              17.913 sec
    ///     - Enumerate only:               0.586 sec
    ///     - Enum and ReadAllBytes only:   1.788 sec
    ///     - Enum and ReadAllText only:   16.948 sec
    ///     - Enum, Read, Span.IndexOf:     1.952 sec
    ///     
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: StringSearch [valueToFind] [searchUnderPath?] [fileExtension?] [logMatchesToPath?]");
                Console.WriteLine("   ex: StringSearch \"Console.WriteLine\" \"C:\\Code\" \"*.cs\" \"ConsoleLoggingClasses.log\"");
                Console.WriteLine();
                Console.WriteLine("  Finds 'valueToFind' in all text files ");
                Console.WriteLine("  under [searchUnderPath] (or current directory)");
                Console.WriteLine("  matching [fileExtension] (or *.*)");
                Console.WriteLine("  and writes sorted match list to log path, if provided.");
            }

            string valueToFind = args[0];
            string directoryToSearch = Path.GetFullPath((args.Length > 1 ? args[1] : Environment.CurrentDirectory));
            string searchPattern = (args.Length > 2 ? args[2] : "*.*");
            string logMatchesToPath = (args.Length > 3 ? args[3] : null);

            FileSearcherMode mode = FileSearcherMode.DotNet;
            int iterations = 1;

            Console.WriteLine($"Searching for \"{valueToFind}\" in '{directoryToSearch}'...");
            Stopwatch w = Stopwatch.StartNew();

            List<FilePosition> matches = null;

            DirectorySearcher searcher = new DirectorySearcher(
                mode: mode,
                multithreaded: false,
                filterOnFileExtension: true,
                filterOnFirstBytes: true
            );

            for (int i = 0; i < iterations; ++i)
            {
                matches = searcher.FindMatches(valueToFind, directoryToSearch, searchPattern);
            }

            w.Stop();
            Console.WriteLine($"Found {matches.Count:n0} matches in {w.Elapsed.TotalSeconds:n3} sec.");

            if (logMatchesToPath == null)
            {
                foreach (FilePosition m in matches.Take(20))
                {
                    Console.WriteLine($"{m}");
                }
            }
            else
            {
                matches.Sort(FilePosition.FileLineCharOrder);

                using (StreamWriter writer = File.CreateText(logMatchesToPath))
                {
                    foreach (FilePosition m in matches)
                    {
                        writer.WriteLine($"{m}");
                    }
                }

                Console.WriteLine($"Sorted and logged to \"{logMatchesToPath}\".");
            }
        }
    }
}
