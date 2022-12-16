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
        public const string DateTimesPath = @"../../../Sample.DatesOnly.log";
        public const int ValueLength = 28;
        public readonly static int LineLength = ValueLength + Environment.NewLine.Length;

        public static IList<DateTime> Original(string filePath)
        {
            using (StreamReader r = File.OpenText(filePath))
            {
                List<DateTime> results = new List<DateTime>();

                while (true)
                {
                    string? line = r.ReadLine();
                    if (line == null) { break; }

                    DateTime value = DateTime.Parse(line).ToUniversalTime();
                    results.Add(value);
                }

                return results;
            }
        }

        public static IList<DateTime> AdjustToUniversal(string filePath)
        {
            using (StreamReader r = File.OpenText(filePath))
            {
                List<DateTime> results = new List<DateTime>();

                while (true)
                {
                    string? line = r.ReadLine();
                    if (line == null) { break; }

                    DateTime value = DateTime.Parse(line, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
                    results.Add(value);
                }

                return results;
            }
        }

        public static IList<DateTime> ParseExact(string filePath)
        {
            using (StreamReader r = File.OpenText(filePath))
            {
                List<DateTime> results = new List<DateTime>();

                while (true)
                {
                    string? line = r.ReadLine();
                    if (line == null) { break; }

                    // Fast [~2,550 ms], but returns adjusted to DateTimeKind.Local. Adding DateTimeStyles.AdjustToUniversal makes it much slower [~5,875 ms].
                    DateTime value = DateTime.ParseExact(line, "O", CultureInfo.InvariantCulture);//, DateTimeStyles.AdjustToUniversal);

                    results.Add(value);
                }

                return results;
            }
        }

        public static IList<DateTime> Offset(string filePath)
        {
            using (StreamReader r = File.OpenText(filePath))
            {
                List<DateTime> results = new List<DateTime>();

                while (true)
                {
                    string? line = r.ReadLine();
                    if (line == null) { break; }

                    // ~3,500
                    //DateTimeOffset value = DateTimeOffset.Parse(line);

                    // ~1,400
                    DateTimeOffset value = DateTimeOffset.ParseExact(line, "O", CultureInfo.InvariantCulture);
                    results.Add(value.UtcDateTime);
                }

                return results;
            }
        }

        public static IList<DateTime> Span(string filePath)
        {
            using (StreamReader r = File.OpenText(filePath))
            {
                int count = (int)((r.BaseStream.Length - r.BaseStream.Position) / LineLength);
                List<DateTime> results = new List<DateTime>(count);
                Span<char> buffer = new Span<char>(new char[LineLength * 1024]);

                while (!r.EndOfStream)
                {
                    int lengthRead = r.Read(buffer);
                    Span<char> block = buffer.Slice(0, lengthRead);

                    while (block.Length > 0)
                    {
                        Span<char> valueText = block.Slice(0, ValueLength);
                        DateTimeOffset value = DateTimeOffset.ParseExact(valueText, "O", CultureInfo.InvariantCulture);
                        results.Add(value.UtcDateTime);
                        block = block.Slice(LineLength);
                    }
                }

                return results;
            }
        }

        public static IList<DateTime> Span_ParseInParts(string filePath)
        {
            using (StreamReader r = File.OpenText(filePath))
            {
                int count = (int)((r.BaseStream.Length - r.BaseStream.Position) / LineLength);
                List<DateTime> results = new List<DateTime>(count);
                Span<char> buffer = new Span<char>(new char[LineLength * 1024]);

                while (true)
                {
                    int lengthRead = r.Read(buffer);
                    Span<char> block = buffer.Slice(0, lengthRead);
                    if (block.Length == 0) { break; }

                    while (block.Length > 0)
                    {
                        Span<char> t = block.Slice(0, ValueLength);

                        DateTime value = new DateTime(
                            int.Parse(t.Slice(0, 4), NumberStyles.None),
                            int.Parse(t.Slice(5, 2), NumberStyles.None),
                            int.Parse(t.Slice(8, 2), NumberStyles.None),
                            int.Parse(t.Slice(11, 2), NumberStyles.None),
                            int.Parse(t.Slice(14, 2), NumberStyles.None),
                            int.Parse(t.Slice(17, 2), NumberStyles.None),
                            DateTimeKind.Utc
                        );

                        // Add sub-seconds (no ctor takes below milliseconds as parts)
                        value = value.AddTicks(int.Parse(t.Slice(20, 7), NumberStyles.None));

                        results.Add(value);
                        block = block.Slice(LineLength);
                    }
                }

                return results;
            }
        }

        public static IList<DateTime> SpanByte(string filePath)
        {
            using (Stream stream = File.OpenRead(filePath))
            {
                int count = (int)((stream.Length - stream.Position) / LineLength);
                List<DateTime> results = new List<DateTime>(count);

                Span<byte> buffer = new Span<byte>(new byte[LineLength * 1024]);

                long length = stream.Length;
                while (stream.Position < length)
                {
                    int lengthRead = stream.Read(buffer);
                    Span<byte> block = buffer.Slice(0, lengthRead);

                    while (block.Length > 0)
                    {
                        Span<byte> t = block.Slice(0, ValueLength);
                        int unused = 0;
                        bool success = true;

                        // Parse and build DateTime from integer parts (year, month, day, ...)
                        success &= Utf8Parser.TryParse(t.Slice(0, 4), out int year, out unused);
                        success &= Utf8Parser.TryParse(t.Slice(5, 2), out int month, out unused);
                        success &= Utf8Parser.TryParse(t.Slice(8, 2), out int day, out unused);
                        success &= Utf8Parser.TryParse(t.Slice(11, 2), out int hour, out unused);
                        success &= Utf8Parser.TryParse(t.Slice(14, 2), out int minute, out unused);
                        success &= Utf8Parser.TryParse(t.Slice(17, 2), out int second, out unused);
                        success &= Utf8Parser.TryParse(t.Slice(20, 7), out int ticks, out unused);

                        DateTime value = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
                        value = value.AddTicks(ticks);

                        if (!success) throw new FormatException("...");

                        results.Add(value);
                        block = block.Slice(LineLength);
                    }
                }

                return results;
            }
        }

        public static IList<DateTime> SpanByte_MyParse(string filePath)
        {
            using (Stream stream = File.OpenRead(filePath))
            {
                int count = (int)((stream.Length - stream.Position) / LineLength);
                List<DateTime> results = new List<DateTime>(count);

                Span<byte> buffer = new Span<byte>(new byte[LineLength * 1024]);

                long length = stream.Length;
                while (stream.Position < length)
                {
                    int lengthRead = stream.Read(buffer);
                    Span<byte> block = buffer.Slice(0, lengthRead);

                    while (block.Length > 0)
                    {
                        Span<byte> t = block.Slice(0, ValueLength);
                        bool success = true;

                        DateTime value = new DateTime(
                            MyParse(t.Slice(0, 4), ref success),
                            MyParse(t.Slice(5, 2), ref success),
                            MyParse(t.Slice(8, 2), ref success),
                            MyParse(t.Slice(11, 2), ref success),
                            MyParse(t.Slice(14, 2), ref success),
                            MyParse(t.Slice(17, 2), ref success),
                            DateTimeKind.Utc);

                        // Add sub-seconds (no ctor to pass with other parts)
                        value = value.AddTicks(MyParse(t.Slice(20, 7), ref success));

                        if (!success) throw new FormatException("...");

                        results.Add(value);
                        block = block.Slice(LineLength);
                    }
                }

                return results;
            }
        }

        private const byte Zero = (byte)'0';
        public static int MyParse(ReadOnlySpan<byte> value, ref bool success)
        {
            int result = 0;

            for (int i = 0; i < value.Length; ++i)
            {
                byte digit = (byte)(value[i] - Zero);

                result *= 10;
                result += digit;
                success &= (digit <= 9);
            }

            return result;
        }

        public static IList<DateTime> SpanByte_Unrolled(string filePath)
        {
            using (Stream stream = File.OpenRead(filePath))
            {
                int count = (int)((stream.Length - stream.Position) / LineLength);
                List<DateTime> results = new List<DateTime>(count);

                Span<byte> buffer = new Span<byte>(new byte[LineLength * 1024]);

                long length = stream.Length;
                while (stream.Position < length)
                {
                    int lengthRead = stream.Read(buffer);
                    Span<byte> block = buffer.Slice(0, lengthRead);

                    while (block.Length >= 28)
                    {
                        Span<byte> t = block.Slice(0, ValueLength);
                        DateTime value = DateTimeParseOUnrolled(t);

                        //if (!success) throw new FormatException("...");

                        results.Add(value);
                        block = block.Slice(LineLength);
                    }
                }

                return results;
            }
        }

        // 190 with AggressiveInlining vs 320 without
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static DateTime DateTimeParseOUnrolled(Span<byte> t)
        {
            int year = 1000 * t[0] + 100 * t[1] + 10 * t[2] + t[3] - 1111 * Zero;
            int month = 10 * t[5] + t[6] - 11 * Zero;
            int day = 10 * t[8] + t[9] - 11 * Zero;
            int hour = 10 * t[11] + t[12] - 11 * Zero;
            int minute = 10 * t[14] + t[15] - 11 * Zero;
            int second = 10 * t[17] + t[18] - 11 * Zero;

            DateTime value = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);

            // Add sub-seconds (no ctor to pass with other parts)
            int subseconds = 1000000 * t[20] + 100000 * t[21] + 10000 * t[22] + 1000 * t[23] + 100 * t[24] + 10 * t[25] + t[26] - 1111111 * Zero;
            value = value.AddTicks(subseconds);
            return value;
        }

        public static IList<DateTime> BinaryTicks(string filePath)
        {
            using (BinaryReader r = new BinaryReader(File.OpenRead(Path.ChangeExtension(filePath, ".bin"))))
            {
                int count = (int)((r.BaseStream.Length - r.BaseStream.Position) / 8);

                // Make a List correctly pre-sized
                List<DateTime> results = new List<DateTime>(count);

                // Read UTC ticks values directly and construct DateTimes from them
                for (int i = 0; i < count; ++i)
                {
                    long ticks = r.ReadInt64();
                    results.Add(new DateTime(ticks, DateTimeKind.Utc));
                }

                return results;
            }
        }

        public static IList<DateTime> BinaryBulk(string filePath)
        {
            using (Stream stream = File.OpenRead(Path.ChangeExtension(filePath, ".bin")))
            {
                int count = (int)((stream.Length - stream.Position) / 8);

                // Make a DateTime array the correct size
                DateTime[] results = new DateTime[count];

                // Read the Ticks bytes directly into the array, interpreting them as DateTimes with no per-value conversion
                stream.Read(MemoryMarshal.Cast<DateTime, byte>(results.AsSpan()));

                return results;
            }
        }

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

        public static IList<System.DateTime> Time(Func<IList<System.DateTime>> action, string name = "Parsed", bool logSum = false)
        {
            Stopwatch w = Stopwatch.StartNew();
            IList<System.DateTime> result = action();
            Console.WriteLine($" {w.ElapsedMilliseconds:n0}\t{name}");

            if (logSum)
            {
                Console.WriteLine($"Ms Total: {result.Sum((dt) => (long)dt.Millisecond):n0}");
            }

            return result;
        }

        private static IList<DateTime>? Expected = null;
        public static IList<DateTime> Verify(IList<DateTime> values)
        {
            Expected ??= Original(DateTimesPath);

            int different = 0;
            for (int i = 0; i < Expected.Count; ++i)
            {
                if (!values[i].Equals(Expected[i])) { different++; }
            }

            Console.WriteLine($" {(different == 0 ? "PASS" : "FAIL")}");
            return values;
        }

        public static Dictionary<string, Func<string, IList<DateTime>>> SelfReflect()
        {
            Dictionary<string, Func<string, IList<DateTime>>> methods = new Dictionary<string, Func<string, IList<DateTime>>>(StringComparer.OrdinalIgnoreCase);

            Type type = typeof(Program);
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

        public static void RunAll(Dictionary<string, Func<string, IList<DateTime>>> methods)
        {
            Console.WriteLine(" ms\tName");
            foreach (string name in methods.Keys)
            {
                Time(() => methods[name](DateTimesPath), name);
                //Verify(Time(() => methods[name](DateTimesPath), name));
            }
        }

        public static void Usage(Dictionary<string, Func<string, IList<DateTime>>> methods)
        {
            Console.WriteLine("Usage: DateTimeParse <mode>");
            Console.WriteLine(" Modes:");
            Console.WriteLine("   write  - writes sample DateTime input files");
            Console.WriteLine("   all    - run and time all parse variations");
            Console.WriteLine("   [name] - test parse variation with 'name' (case-insensitive).");
            Console.WriteLine($"     Variations: {String.Join(", ", methods.Keys)}");
        }

        public static int Main(string[] args)
        {
            Dictionary<string, Func<string, IList<DateTime>>> methods = SelfReflect();

            if (args.Length < 1)
            {
                Usage(methods);
                return -1;
            }

            string mode = args[0].ToLowerInvariant();

            switch (mode)
            {
                case "write":
                    WriteSampleFile(DateTimesPath);
                    break;

                case "all":
                    RunAll(methods);
                    break;

                default:
                    if (methods.TryGetValue(mode, out var method))
                    {
                        Time(() => method(DateTimesPath), mode, logSum: true);
                        break;
                    }
                    else
                    {
                        Console.WriteLine($"Error: Unknown Mode '{mode}'.");
                        Usage(methods);
                        return -1;
                    }
            }

            return 0;
        }
    }
}
