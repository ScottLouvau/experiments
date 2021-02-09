// Copyright Scott Louvau, MIT License.

using RoughBench;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Copy
{
    class TestResult
    {
        public string FunctionName { get; set; }
        public List<MeasureResult> Results { get; set; }
        public double FastestSecondsPerIteration => Results.Min((r) => r.SecondsPerIteration);

        public TestResult(string functionName, List<MeasureResult> results)
        {
            FunctionName = functionName;
            Results = results;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            double seconds = (args.Length > 0 ? double.Parse(args[0]) : 5);
            int threadLimit = (args.Length > 1 ? int.Parse(args[1]) : Environment.ProcessorCount);
            string methodName = (args.Length > 2 ? args[2] : null);

            Console.WriteLine("Running Copy Performance Tests...");

            CopyTests cpt = new CopyTests();

            // Find all 'CopyKernel' signature benchmark methods to test
            Dictionary<string, CopyTests.CopyKernel> methods = BenchmarkReflector.BenchmarkMethods<CopyTests.CopyKernel>(cpt.GetType(), cpt);

            // Find all 'UnsafeCopyKernel' methods, wrap them, and include them
            foreach (var pair in BenchmarkReflector.BenchmarkMethods<CopyTests.UnsafeCopyKernel>(cpt.GetType(), cpt))
            {
                methods[pair.Key] = cpt.Wrap(pair.Value);
            }

            if (methodName != null)
            {
                methods = new Dictionary<string, CopyTests.CopyKernel>(methods.Where((m) => m.Key.Contains(methodName, StringComparison.OrdinalIgnoreCase)));
            }

            MeasureSettings settings = new MeasureSettings(TimeSpan.FromSeconds(seconds), 5, 1000000, false);
            ConsoleTable table = BuildTable(threadLimit);

            foreach (var method in methods)
            {
                List<MeasureResult> results = new List<MeasureResult>();

                for (int threads = 1; threads <= threadLimit; threads *= 2)
                {
                    CopyTests.CopyKernel kernel = cpt.Parallelize(method.Value, threads);
                    results.Add(Measure.Operation(() => cpt.Run(kernel), settings));

                    bool identical = cpt.VerifyIdentical();
                    cpt.Clear();

                    if (!identical)
                    {
                        Console.WriteLine($"ERROR: {method.Key} did not correctly copy bytes.");
                        return;
                    }
                }

                table.AppendRow(RenderRow(new TestResult(method.Key, results)));
            }
        }

        private static ConsoleTable BuildTable(int threadLimit)
        {
            List<TableCell> columns = new List<TableCell>();
            columns.Add(new TableCell("Method"));

            for (int threads = 1; threads <= threadLimit; threads *= 2)
            {
                columns.Add(new TableCell($"Speed [{threads}T]", Align.Right));
            }

            return new ConsoleTable(columns);
        }

        private static TableCell[] RenderRow(TestResult result)
        {
            List<TableCell> cells = new List<TableCell>();

            cells.Add(new TableCell(result.FunctionName));

            foreach(MeasureResult measurement in result.Results)
            {
                cells.Add(new TableCell(
                    Format.Rate(CopyTests.Length, measurement.SecondsPerIteration),
                    Align.Right,
                    (measurement.SecondsPerIteration == result.FastestSecondsPerIteration ? TableColor.Green : TableColor.Default)
                ));
            }

            return cells.ToArray();
        }
    }
}
