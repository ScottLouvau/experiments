using System.Diagnostics;
using System.Reflection;
using System.Xml;

namespace DateTimeParse
{
    /// <summary>
    ///  Performance Tuning Example showing alternatives for parsing a file of DateTimes.    
    /// </summary>
    /// <remarks>
    ///  Uses 'O' DateTime format, which is 28 bytes (29 with \n)
    ///    2022-04-14T02:32:53.4028225Z
    ///    **** ** ** ** ** ** *******
    ///    0123456789012345678901234567
    ///    
    ///  Uses UTC Ticks as binary form (8 bytes).
    /// </remarks>
    public static class Program
    {
        // One folder up from executable or current folder; should be the 'datetime-parse' shared folder for 'dotnet run' or built .dll or .exe
        public const string DateTimesPath = @"../Sample.DatesOnly.log";

        // Build the sample files (many DateTimes, roundtrippable 'O' format)
        public static void WriteSampleFile(string filePath, int count = 10 * 1000 * 1000)
        {
            Random r = new Random();
            DateTime current = DateTime.UtcNow.AddDays(-180 * r.NextDouble());

            using (StreamWriter wT = File.CreateText(filePath))
            {
                for (int i = 0; i < count; i++)
                {
                    wT.Write($"{current:O}\n");
                    current = current.AddMilliseconds(10000 * r.NextDouble() * r.NextDouble());
                }
            }
        }

        public static IList<System.DateTime> Time(Func<IList<System.DateTime>> action, string name)
        {
            IList<System.DateTime> result = null!;

            int iterations = 0;
            Stopwatch w = Stopwatch.StartNew();

            for (int i = 0; i < 10; ++i)
            {
                result = action();

                iterations += 1;
                if (w.ElapsedMilliseconds > 1500) { break; }
            }

            w.Stop();
            long average = w.ElapsedMilliseconds / iterations;

            // After timing stops, verify loading by finding the sum of milliseconds
            long check = result.Sum((dt) => (long)dt.TimeOfDay.Milliseconds);

            // Log the runtime, method name, count loaded, and milliseconds-sum-mod-10000
            Console.WriteLine($"| {average.ToString("n0").PadLeft(5)} | {name.PadRight(30)} | {check} |");

            return result;
        }

        public static Dictionary<string, Func<string, IList<DateTime>>> Reflect()
        {
            Dictionary<string, Func<string, IList<DateTime>>> methods = new Dictionary<string, Func<string, IList<DateTime>>>(StringComparer.OrdinalIgnoreCase);

            Type type = typeof(ParseVariations);
            Type returnType = typeof(IList<DateTime>);
            Type[] paramsTypes = new[] { typeof(string) };

            foreach (MethodInfo method in type.GetMethods())
            {
                if (method.ReturnType == returnType && Enumerable.SequenceEqual(method.GetParameters().Select((pi) => pi.ParameterType), paramsTypes))
                {
                    methods[method.Name] = (Func<string, IList<DateTime>>)(object)Delegate.CreateDelegate(typeof(Func<string, IList<DateTime>>), method);
                }
            }

            return methods;
        }

        public static void RunAll()
        {
            if (!File.Exists(DateTimesPath)) { 
                Console.WriteLine("Generating DateTime data file...");
                WriteSampleFile(DateTimesPath); 
            }

            Dictionary<string, Func<string, IList<DateTime>>> methods = Reflect();

            Console.WriteLine();
            Console.WriteLine($"|    ms | .NET {System.Environment.Version,-25} | SumMillis  |");
            Console.WriteLine("| ----- | ------------------------------ | ---------- |");

            foreach (string name in methods.Keys)
            {
                Time(() => methods[name](DateTimesPath), name);
            }

            Console.WriteLine();
        }

        public static void Main(string[] args)
        {
            RunAll();
        }
    }
}
