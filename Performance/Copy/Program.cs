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
        public MeasureResult MeasureResult { get; set; }
        public int Threads { get; set; }

        public string DisplayName => (Threads == 1 ? FunctionName : $"{FunctionName} {Threads}t");

        public TestResult(string functionName, MeasureResult measureResult, int threads = 1)
        {
            FunctionName = functionName;
            MeasureResult = measureResult;
            Threads = threads;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            int threadLimit = (args.Length > 0 ? int.Parse(args[0]) : Environment.ProcessorCount);
            string methodName = (args.Length > 1 ? args[1] : null);
            int seconds = (args.Length > 2 ? int.Parse(args[2]) : 5);

            Console.WriteLine("Running Copy Performance Tests...");

            CopyTests cpt = new CopyTests();

            // Find all 'CopyKernel' signature benchmark methods to test
            Dictionary<string, CopyTests.CopyKernel> methods = Reflector.BenchmarkMethods<CopyTests.CopyKernel>(cpt.GetType(), cpt);

            // Find all 'UnsafeCopyKernel' methods, wrap them, and include them
            foreach (var pair in Reflector.BenchmarkMethods<CopyTests.UnsafeCopyKernel>(cpt.GetType(), cpt))
            {
                methods[pair.Key] = cpt.Wrap(pair.Value);
            }

            if (methodName != null)
            {
                methods = new Dictionary<string, CopyTests.CopyKernel>(methods.Where((m) => m.Key.Contains(methodName, StringComparison.OrdinalIgnoreCase)));
            }

            MeasureSettings settings = new MeasureSettings(TimeSpan.FromSeconds(seconds), 5, 1000000, false);
            ConsoleTable table = BuildTable();
            List<TestResult> results = new List<TestResult>();

            foreach (var method in methods)
            {
                for (int threads = 1; threads <= threadLimit; threads *= 2)
                {
                    CopyTests.CopyKernel kernel = cpt.Parallelize(method.Value, threads);

                    TestResult current = new TestResult(
                        method.Key,
                        Measure.Operation(() => cpt.Run(kernel), settings),
                        threads
                    );

                    //bool identical = cpt.VerifyIdentical();
                    cpt.Clear();

                    //if (!identical)
                    //{
                    //    Console.WriteLine($"ERROR: {current.DisplayName} did not correctly copy bytes.");
                    //    return;
                    //}

                    table.AppendRow(RenderRow(current));
                    results.Add(current);
                }
            }

            // Re-write results sorted by performance ascending
            Console.WriteLine();
            Console.WriteLine("Sorted by Performance: ");

            table = BuildTable();
            foreach (TestResult result in results.OrderByDescending((r) => r.MeasureResult.SecondsPerIteration))
            {
                table.AppendRow(RenderRow(result));
            }
        }

        private static ConsoleTable BuildTable()
        {
            return new ConsoleTable(
                new TableCell("Method [+ threads]"),
                new TableCell("Speed", Align.Right, TableColor.Green),
                new TableCell($"Per {Format.Size(CopyTests.Length)}", Align.Right));
        }

        private static string[] RenderRow(TestResult result)
        {
            return new string[] {
                result.DisplayName,
                Format.Rate(CopyTests.Length, result.MeasureResult.SecondsPerIteration),
                Format.Time(result.MeasureResult.SecondsPerIteration)
            };
        }
    }
}
