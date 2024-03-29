using System.Buffers.Text;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Xml;

/// <summary>
///  Variations parsing DateTimes in a file.
///  
///  Uses 'O' DateTime format, which is 28 bytes, delimited with bare newlines (\n), for 29 bytes per line.
///    2022-04-14T02:32:53.4028225Z
///    **** ** ** ** ** ** *******
///    0123456789012345678901234567
/// </summary>
public static class ParseVariations
{
    // Known lengths of each value and line.
    public const int ValueLength = 28;
    public const int LineLength = 29;

    // ---- Naive Implementations ----

    // My actual original code.
    public static IList<DateTime> DateTimeParse(string filePath)
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

    // My guess about the most likely first C# implemetation.
    public static IList<DateTime> DateTimeParseExact(string filePath)
    {
        // 594k views, first result for "C# read file lines":
        // https://stackoverflow.com/questions/8037070/whats-the-fastest-way-to-read-a-text-file-line-by-line

        // 449k views, first result for "C# parse datetime":
        // https://stackoverflow.com/questions/5366285/how-to-parse-strings-to-datetime-in-c-sharp-properly

        // Need 'AdjustToUniversal' to get Utc DateTimes out.

        List<DateTime> results = new List<DateTime>();

        var lines = File.ReadLines(filePath);
        foreach (string line in lines)
        {
            DateTime value = DateTime.ParseExact(line, "O", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
            results.Add(value);
        }

        return results;
    }

    // Closest equivalent to Rust Naive implementation, where reading files by lines isn't as obvious.
    public static IList<DateTime> RustNaiveClosest(string filePath)
    {
        List<DateTime> results = new List<DateTime>();

        string text = File.ReadAllText(filePath);
        foreach (string line in text.Split("\n")) {
            if (line.Length == 0) { break; }

            DateTime value = DateTime.ParseExact(line, "O", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
            results.Add(value);
        }

        return results;
    }

    // Returns DateTimeKind.Local, but about twice as fast without DateTimeStyles.AdjustToUniversal
    // It's weird that internally DateTime.ParseExact can't avoid converting to local time and back.
    public static IList<DateTime> DateTimeParseExactNotUtc(string filePath)
    {
        List<DateTime> results = new List<DateTime>();

        var lines = File.ReadLines(filePath);
        foreach (string line in lines)
        {
            DateTime value = DateTime.ParseExact(line, "O", CultureInfo.InvariantCulture);//, DateTimeStyles.AdjustToUniversal);
            results.Add(value);
        }

        return results;
    }

    // Modern Naive choice: ReadLines and DateTimeOffset.ParseExact, which both perform well.
    public static IList<DateTime> DateTimeOffsetParseExact(string filePath)
    {
        List<DateTime> results = new List<DateTime>();

        var lines = File.ReadLines(filePath);
        foreach (string line in lines) 
        {
            DateTimeOffset value = DateTimeOffset.ParseExact(line, "O", CultureInfo.InvariantCulture);
            results.Add(value.UtcDateTime);
        }

        return results;
    }

    // ---- Optimized Implementations ----

    // Read in blocks into Span<char> and use DateTimeOffset.ParseExact.
    // This avoids creating a string per line.
    public static IList<DateTime> SpanOfChar(string filePath)
    {
        using (StreamReader r = File.OpenText(filePath))
        {
            int count = (int)((r.BaseStream.Length - r.BaseStream.Position) / LineLength);
            List<DateTime> results = new List<DateTime>(count);

            Span<char> buffer = new Span<char>(new char[LineLength * 2048]);
            int lengthReused = 0;

            while (!r.EndOfStream)
            {
                int lengthRead = r.Read(buffer.Slice(lengthReused));
                Span<char> block = buffer.Slice(0, lengthReused + lengthRead);

                while (block.Length > 0)
                {
                    int newline = block.IndexOf('\n'); // ValueLength; (550 -> 460 ms)

                    Span<char> valueText = block.Slice(0, newline);
                    DateTimeOffset value = DateTimeOffset.ParseExact(valueText, "O", CultureInfo.InvariantCulture);
                    results.Add(value.UtcDateTime);
                    block = block.Slice(newline + 1);
                }

                if (block.Length > 0) { 
                    block.CopyTo(buffer);
                }
                
                lengthReused = block.Length;
            }

            return results;
        }
    }

    // Read as bytes, avoiding UTF-16 conversion and UTF-8 validation.
    // Split at known length, avoiding newline searches.
    // Use built-in functions to parse each number.
    public static IList<DateTime> BytesAndCustomParse(string filePath)
    {
        using (Stream stream = File.OpenRead(filePath))
        {
            int count = (int)((stream.Length - stream.Position) / LineLength);
            List<DateTime> results = new List<DateTime>(count);

            Span<byte> buffer = new Span<byte>(new byte[LineLength * 2048]);

            long length = stream.Length;
            while (stream.Position < length)
            {
                int lengthRead = stream.Read(buffer);
                Span<byte> block = buffer.Slice(0, lengthRead);

                while (block.Length > 0)
                {
                    int newline = block.IndexOf((byte)'\n');
                    Span<byte> t = block.Slice(0, newline);

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
                    block = block.Slice(newline + 1);
                }
            }

            return results;
        }
    }

    // Read as bytes, avoiding UTF-16 conversion and UTF-8 validation.
    // Split at known length, avoiding newline searches.
    // Use built-in functions to parse each number.
    public static IList<DateTime> KnownLengthSplitAndCustomParse(string filePath)
    {
        using (Stream stream = File.OpenRead(filePath))
        {
            int count = (int)((stream.Length - stream.Position) / LineLength);
            List<DateTime> results = new List<DateTime>(count);

            Span<byte> buffer = new Span<byte>(new byte[LineLength * 2048]);

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

    // Same as Custom, but use my own number parsing function.
    public static IList<DateTime> Custom_MyParse(string filePath)
    {
        using (Stream stream = File.OpenRead(filePath))
        {
            int count = (int)((stream.Length - stream.Position) / LineLength);
            List<DateTime> results = new List<DateTime>(count);

            Span<byte> buffer = new Span<byte>(new byte[LineLength * 2048]);

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

    // Same as custom, but remove verifying the digits are in range, unroll the digit parsing, and inline the parsing function
    public static IList<DateTime> Custom_NoErrors(string filePath)
    {
        using (Stream stream = File.OpenRead(filePath))
        {
            int count = (int)((stream.Length - stream.Position) / LineLength);
            List<DateTime> results = new List<DateTime>(count);

            Span<byte> buffer = new Span<byte>(new byte[LineLength * 2048]);

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
        int subseconds = 
            1000000 * t[20] 
            + 100000 * t[21] 
            + 10000 * t[22] 
            + 1000 * t[23] 
            + 100 * t[24] 
            + 10 * t[25] 
            + t[26] 
            - 1111111 * Zero;

        value = value.AddTicks(subseconds);
        return value;
    }

    // ---- Experiments ----

    // // 45 ms; how much for the I/O only?
    // public static IList<DateTime> ReadBytesOnly(string filePath)
    // {
    //     File.ReadAllBytes(filePath);
    //     return new List<DateTime>();
    // }

    // // 240 ms; How much to read, convert to UTF-16, and split on line?
    // public static IList<DateTime> ReadLineOnly(string filePath)
    // {
    //     var lines = File.ReadLines(filePath);
    //     foreach (string line in lines)
    //     {
    //         // Read Line only
    //     }

    //     return new List<DateTime>();
    // }
}