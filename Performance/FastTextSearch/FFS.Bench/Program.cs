using RoughBench;
using System;
using System.Collections.Generic;

namespace FFS.Bench
{
    class Program
    {
        static void Main(string[] args)
        {
            //Benchmarker b = new Benchmarker();
            //b.Run<Enumerate>();
            //b.Run<Search>();

            InMemorySearch instance = new InMemorySearch();
            BenchmarkWithRate(instance, () => instance.BytesSearched);
        }

        static ConsoleTable _table;

        static void BenchmarkWithRate(object instance, Func<long> getBytesProcessed)
        {
            if (_table == null) { _table = new ConsoleTable("Method", "Average", "Speed"); }
            Dictionary<string, Action> methods = BenchmarkReflector.BenchmarkMethods<Action>(instance);
            foreach (var pair in methods)
            {
                MeasureResult result = Measure.Operation(pair.Value);
                _table.AppendRow(pair.Key, Format.Time(result.SecondsPerIteration), Format.Rate(getBytesProcessed(), result.SecondsPerIteration));
            }
        }
    }
}
