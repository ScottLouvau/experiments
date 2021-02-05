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
            Console.WriteLine("Running Copy Performance Tests...");

            // Measure each implementation for two seconds, up to 10k executions
            MeasureSettings settings = new MeasureSettings(TimeSpan.FromSeconds(2), 5, 10000, false);

            // Run all tests on this class
            CopyTests cpt = new CopyTests();

            // Find all 'CopyKernel' signature benchmark methods to test
            Dictionary<string, CopyTests.CopyKernel> methods = Reflector.BenchmarkMethods<CopyTests.CopyKernel>(cpt.GetType(), cpt);

            // Find all 'UnsafeCopyKernel' methods, wrap them, and include them
            foreach (var pair in Reflector.BenchmarkMethods<CopyTests.UnsafeCopyKernel>(cpt.GetType(), cpt))
            {
                methods[pair.Key] = cpt.Wrap(pair.Value);
            }

            ConsoleTable table = BuildTable();
            List<TestResult> results = new List<TestResult>();
            int threadCount = Environment.ProcessorCount;

            foreach (var method in methods)
            {
                TestResult last = null;

                // Run each method single and multi-threaded until it's not more than 10% faster
                for (int threads = 1; threads <= threadCount; threads *= 2)
                {
                    CopyTests.CopyKernel kernel = cpt.Parallelize(method.Value, threads);

                    TestResult current = new TestResult(
                        method.Key,
                        Measure.Operation(() => cpt.Run(kernel), settings),
                        threads
                    );

                    bool identical = cpt.VerifyIdentical();
                    cpt.Clear();

                    if (!identical)
                    {
                        Console.WriteLine($"ERROR: {current.DisplayName} did not correctly copy bytes.");
                        return;
                    }

                    table.AppendRow(RenderRow(current));
                    results.Add(current);

                    if (last != null && current.MeasureResult.SecondsPerIteration > last.MeasureResult.SecondsPerIteration * 0.9)
                    {
                        break;
                    }

                    last = current;
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
