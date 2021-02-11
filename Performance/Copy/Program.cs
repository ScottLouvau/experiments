// Copyright Scott Louvau, MIT License.

using RoughBench;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

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
            string methodNameContains = (args.Length > 2 ? args[2] : null);

            RunAll(new CopyBuiltIns(), seconds, threadLimit, methodNameContains);
            RunAll(new CopyLoops(), seconds, threadLimit, methodNameContains);
            RunAll(new CopyAvx(), seconds, threadLimit, methodNameContains);
            RunAll(new CopyNonTemporal(), seconds, threadLimit, methodNameContains);
            RunAll(new CopyTestsUnrolled(), seconds, threadLimit, methodNameContains);
        }

        private static void RunAll(CopyTestBase instance, double seconds, int threadLimit, string methodNameContains = null)
        {
            // Find all 'CopyKernel' signature benchmark methods to test
            Dictionary<string, CopyTestBase.CopyKernel> methods = BenchmarkReflector.BenchmarkMethods<CopyTestBase.CopyKernel>(instance.GetType(), instance);

            // Find all 'UnsafeCopyKernel' methods, wrap them, and include them
            foreach (var pair in BenchmarkReflector.BenchmarkMethods<CopyTestBase.UnsafeCopyKernel>(instance.GetType(), instance))
            {
                methods[pair.Key] = instance.Wrap(pair.Value);
            }

            if (methodNameContains != null)
            {
                methods = methods.Where((m) => m.Key.IndexOf(methodNameContains, StringComparison.OrdinalIgnoreCase) >= 0).ToDictionary((m) => m.Key, (m) => m.Value);
            }

            MeasureSettings settings = new MeasureSettings(TimeSpan.FromSeconds(seconds), 5, 1000000, false);
            ConsoleTable table = BuildTable(threadLimit);

            foreach (var method in methods)
            {
                List<MeasureResult> results = new List<MeasureResult>();

                for (int threads = 1; threads <= threadLimit; threads *= 2)
                {
                    CopyTestBase.CopyKernel kernel = instance.Parallelize(method.Value, threads);
                    results.Add(Measure.Operation(() => instance.Run(kernel), settings));

                    bool identical = instance.VerifyIdentical();
                    instance.Clear();

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
            columns.Add(new TableCell($"Method [{RuntimeInformation.FrameworkDescription} on {Environment.MachineName}]"));

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
                    Format.Rate(CopyTestBase.Length, measurement.SecondsPerIteration),
                    Align.Right,
                    (measurement.SecondsPerIteration == result.FastestSecondsPerIteration ? TableColor.Green : TableColor.Default)
                ));
            }

            return cells.ToArray();
        }
    }
}
