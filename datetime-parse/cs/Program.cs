using System.Buffers.Text;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DateTimeParse
{
    /// <summary>
    ///  Performance Tuning Example showing alternatives for parsing a file of DateTimes.    
    /// </summary>
    /// <remarks>
    ///  Uses 'O' DateTime format, which is 28 bytes (30 with \r\n)
    ///    2022-04-14T02:32:53.4028225Z
    ///    **** ** ** ** ** ** *******
    ///    0123456789012345678901234567
    ///    
    ///  Uses UTC Ticks as binary form (8 bytes).
    /// </remarks>
    public static class Program
    {
        public const string DateTimesPath = @"../../Sample.DatesOnly.log";



        // Build the sample files (many DateTimes, in text one per line or in binary ticks form)
        public static void WriteSampleFile(string filePath, int count = 10 * 1000 * 1000)
        {
            Random r = new Random();
            DateTime current = DateTime.UtcNow.AddDays(-180 * r.NextDouble());

            using (StreamWriter wT = File.CreateText(filePath))
            using (BinaryWriter wB = new BinaryWriter(File.Create(Path.ChangeExtension(filePath, ".bin"))))
            {
                for (int i = 0; i < count; i++)
                {
                    wT.WriteLine($"{current:O}");
                    wB.Write(current.Ticks);
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

            long average = w.ElapsedMilliseconds / iterations;
            Console.WriteLine($"| {average.ToString("n0").PadLeft(5)} | {name.PadRight(30)} |");

            return result;
        }

        private static IList<DateTime>? Expected = null;
        public static IList<DateTime> Verify(IList<DateTime> values)
        {
            Expected ??= ParseVariations.Original(DateTimesPath);

            int different = 0;
            for (int i = 0; i < Expected.Count; ++i)
            {
                if (!values[i].Equals(Expected[i])) { different++; }
            }

            Console.WriteLine($" {(different == 0 ? "PASS" : "FAIL")}");
            return values;
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
            Dictionary<string, Func<string, IList<DateTime>>> methods = Reflect();

            Console.WriteLine($"|    ms | .NET {System.Environment.Version,-25} |");
            Console.WriteLine("| ----- | ------------------------------ |");

            foreach (string name in methods.Keys)
            {
                Time(() => methods[name](DateTimesPath), name);
                //Verify(Time(() => methods[name](DateTimesPath), name));
            }
        }

        public static void Main(string[] args)
        {
            RunAll();
        }
    }
}
