using RoughBench;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FFS.Bench
{
    class Program
    {
        static void Main(string[] args)
        {
            //Benchmarker b = new Benchmarker();
            //b.Run<Enumerate>();

            // bion: 862 files, 42.8 MB. Text: 504 files, 3.65 MB.
            Sniffer.Assess(@"C:\CodeBig");
            //RunSeries("Convert", @"C:\CodeSnap\bion");
            //RunSeries("~~~", @"C:\CodeSnap\bion");

            //RunSeries("destroy", @"C:\CodeSnap");
            //RunSeries(".destroy", @"C:\CodeSnap");
        }

        static void RunSeries(string searchFor, string searchUnder)
        {
            _table = null;
            Console.WriteLine();
            Console.WriteLine($"Searching for '{searchFor}' under '{searchUnder}'...");

            FileInfo[] files = new DirectoryInfo(searchUnder).GetFiles("*.*", SearchOption.AllDirectories);
            long totalFileSize = files.Sum((fi) => fi.Length);

            string[] textFiles = Sniffer.TextFilePaths(searchUnder);

            string concatenatedTextPath = "Text.concat";
            Sniffer.ConcatenateTextFilesUnder(searchUnder, concatenatedTextPath);
            long totalTextSize = new FileInfo(concatenatedTextPath).Length;

            Console.WriteLine($" Total: {files.Length:n0} files, {Format.Size(totalFileSize)}");
            Console.WriteLine($" Text:  {textFiles.Length:n0} files, {Format.Size(totalTextSize)}");

            // Search all Files
            FilesSearch fs = new FilesSearch(searchFor, Directory.GetFiles(searchUnder, "*.*", SearchOption.AllDirectories));
            BenchmarkWithRate(fs, () => fs.BytesSearched, $"Read all files");

            // Search sniffed text Files
            fs = new FilesSearch(searchFor, textFiles);
            BenchmarkWithRate(fs, () => fs.BytesSearched, $"Read text files");

            // Search concatenated text from disk
            InMemorySearch ims = new InMemorySearch(searchFor, () => File.ReadAllText(concatenatedTextPath), () => File.ReadAllBytes(concatenatedTextPath));
            BenchmarkWithRate(ims, () => ims.BytesSearched, $"Concatenated on disk bytes");

            // Search concatenated text in memory
            byte[] concatenatedBytes = File.ReadAllBytes(concatenatedTextPath);
            string concatenatedString = File.ReadAllText(concatenatedTextPath);
            ims = new InMemorySearch(searchFor, () => concatenatedString, () => concatenatedBytes);
            BenchmarkWithRate(ims, () => ims.BytesSearched, $"Concatenated in-memory bytes");

            _table.Save(File.Create($"{searchFor}.{Path.GetFileName(searchUnder)}.md"));
        }

        static ConsoleTable _table;

        static void BenchmarkWithRate(object instance, Func<long> getBytesProcessed, string scenario)
        {
            if (_table == null) { _table = new ConsoleTable("Scenario", "Method", "Average", "Speed"); }
            Dictionary<string, Action> methods = BenchmarkReflector.BenchmarkMethods<Action>(instance);
            foreach (var pair in methods)
            {
                MeasureResult result = Measure.Operation(pair.Value);
                _table.AppendRow(
                    TableCell.String(scenario),
                    TableCell.String(pair.Key),
                    TableCell.Time(result.SecondsPerIteration),
                    TableCell.Rate(getBytesProcessed(), result.SecondsPerIteration)
                );
            }

            _table.AppendRow("---", "---", "---", "---");
        }
    }
}
