// Copyright (c) Scott Louvau. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using FastTextSearch;

namespace FFS
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Fast File Search by Scott Louvau, 2021.");
                Console.WriteLine("Usage: FFS [valueToFind] [searchUnderPath?] [fileExtension?] [logMatchesToPath?]");
                Console.WriteLine("   ex: FFS \"Console.WriteLine\" \"C:\\Code\" \"*.cs\" \"ConsoleLoggingClasses.log\"");
                Console.WriteLine();
                Console.WriteLine("  Finds 'valueToFind' in all text files ");
                Console.WriteLine("  under [searchUnderPath] (or current directory)");
                Console.WriteLine("  matching [fileExtension] (or *.*)");
                Console.WriteLine("  and writes sorted match list to log path, if provided.");
            }

            string valueToFind = args[0];
            string directoryToSearch = Path.GetFullPath((args.Length > 1 ? args[1] : Environment.CurrentDirectory));
            string searchPattern = (args.Length > 2 ? args[2] : "*.*");
            string logMatchesToPath = (args.Length > 3 ? Path.GetFullPath(args[3]) : null);
            FileSearcher searcher = (args.Length > 4 ? Enum.Parse<FileSearcher>(args[4]) : FileSearcher.Utf8);
            int iterations = (args.Length > 5 ? int.Parse(args[5]) : 1);

            Console.WriteLine($"Searching for \"{valueToFind}\" with {searcher} in '{directoryToSearch}'{(iterations > 1 ? $" [{iterations:n0}x]" : "")}...");
            Stopwatch w = Stopwatch.StartNew();

            DirectorySearcher directorySearcher = new DirectorySearcher(
                searcher: searcher,
                multithreaded: true,
                filterOnFileExtension: true,
                sniffFile: true
            );

            List<FilePosition> matches = null;
            for (int i = 0; i < iterations; ++i)
            {
                matches = directorySearcher.FindMatches(valueToFind, directoryToSearch, searchPattern);
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

                Directory.CreateDirectory(Path.GetDirectoryName(logMatchesToPath));
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
