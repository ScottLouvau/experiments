using System;
using System.Threading;

namespace StringSearch
{
    // NOTE:
    // BOM detection based on .NET CLR StreamReader.DetectEncoding
    // https://referencesource.microsoft.com/#mscorlib/system/io/streamreader.cs,ea5187ae9c79350e
    // Add (bytes[3] == 0 && bytes[5] == 0) || (bytes[4] == 0 && bytes[6] == 0); will trigger on UTF-16/32 when first characters are ASCII range.
    // Can identify ZIP, PNG, JPG, PACK, DLL, EXE files via first bytes.

    // Invalid UTF-8 identification based on Lemire's "Validating UTF-8 In Less Than One Instruction Per Byte"
    // https://lemire.me/blog/2018/05/16/validating-utf-8-strings-using-as-little-as-0-7-cycles-per-byte/
    // https://arxiv.org/pdf/2010.03090.pdf
    // https://github.com/simdjson/simdjson/blob/master/src/generic/stage1/utf8_lookup4_algorithm.h

    // Minimal Table:
    //  [0x00 - 0x7F] - single byte
    //  [0xC2 - 0xDF] - must have one [0x80 - 0xBF] afterward.
    //  [0xE0 - 0xEF] - must have two [0x80 - 0xBF] afterward.
    //  [0xF0 - 0xF4] - must have three [0x80 - 0xBF] afterward.
    //  [0xC0, 0xC1, 0xF5+] - illegal.
    // Guess that non-UTF-8 will 

    public enum FileEncoding
    {
        UTF8,
        UTF16_BigEndian,
        UTF16_LittleEndian,
        UTF32_BigEndian,
        UTF32_LittleEndian,
        Binary,
        NotLegalUTF8,
        PossibleUTF8
    }

    public class EncodingScanner
    {
        public static int UTF8viaBOM;
        public static int OtherViaBOM;
        public static int PossibleUTF8;
        public static int FilteredFileCount;

        private const byte TooShort = 1 << 0;
        private const byte TooLong = 1 << 1;
        private const byte Overlong3 = 1 << 2;
        private const byte Surrogate = 1 << 4;
        private const byte Overlong2 = 1 << 5;
        private const byte TwoConts = 1 << 7;
        private const byte TooLarge = 1 << 3;
        private const byte TooLarge1000 = 1 << 6;
        private const byte Overlong4 = 1 << 6;

        private static byte[] Byte1High = new byte[]
        {
            // 0xxxxxxx   [Single byte ASCII char]
            TooLong, TooLong, TooLong, TooLong,
            TooLong, TooLong, TooLong, TooLong,

            // 10xxxxxx   [Continuation byte]
            TwoConts, TwoConts, TwoConts, TwoConts,

            // 1100xxxx   [Two-Byte char start]
            TooShort | Overlong2,

            // 1101xxxx   [Two-Byte char start]
            TooShort,

            // 1110xxxx   [Three-Byte char start]
            TooShort | Overlong3 | Surrogate,

            // 1111xxxx   [Four-Byte start or invalid]
            TooShort | TooLarge | TooLarge1000 | Overlong4
        };

        private const byte Carry = TooShort | TooLong | TwoConts;

        private static byte[] Byte1Low = new byte[]
        {
            // xxxx0000
            Carry | Overlong2 | Overlong3 | Overlong4,

            // xxxx0001
            Carry | Overlong2,

            // xxxx001x
            Carry, Carry,

            // xxxx0100
            Carry | TooLarge,

            // xxxx0101
            Carry | TooLarge | TooLarge1000,

            // xxxx011x
            Carry | TooLarge | TooLarge1000,
            Carry | TooLarge | TooLarge1000,

            // xxxx1xxx
            Carry | TooLarge | TooLarge1000,
            Carry | TooLarge | TooLarge1000,
            Carry | TooLarge | TooLarge1000,
            Carry | TooLarge | TooLarge1000,

            // xxxx1101
            Carry | TooLarge | TooLarge1000 | Surrogate,
            Carry | TooLarge | TooLarge1000,
            Carry | TooLarge | TooLarge1000
        };

        private static byte[] Byte2High = new byte[]
        {
            // 0xxxxxxxx    [ASCII byte]
            TooShort, TooShort, TooShort, TooShort,
            TooShort, TooShort, TooShort, TooShort,

            // 1000xxxx
            TooLong | Overlong2 | TwoConts | Overlong3 | TooLarge1000 | Overlong4,

            // 1001xxxx
            TooLong | Overlong2 | TwoConts | Overlong3 | TooLarge,

            // 101xxxxx
            TooLong | Overlong2 | TwoConts | Surrogate | TooLarge,
            TooLong | Overlong2 | TwoConts | Surrogate | TooLarge,

            // 11xxxxxx
            TooShort, TooShort, TooShort, TooShort
        };

        /// <summary>
        ///  Identify tries to identify a file, looking for BOMs at the beginning
        ///  and then looking for invalid UTF-8 afterward.
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="isStartOfFile"></param>
        /// <returns></returns>
        public static FileEncoding Identify(Span<byte> bytes, bool isStartOfFile)
        {
            if (isStartOfFile && bytes.Length >= 4)
            {
                // Modelling Byte-Order-Mark detection in StreamReader.DetectEncoding
                // https://referencesource.microsoft.com/#mscorlib/system/io/streamreader.cs,ea5187ae9c79350e,references

                if (bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                {
                    Interlocked.Increment(ref UTF8viaBOM);
                    return FileEncoding.UTF8;
                }
                else if (bytes[0] == 0xFE && bytes[1] == 0xFF)
                {
                    Interlocked.Increment(ref OtherViaBOM);
                    return FileEncoding.UTF16_BigEndian;
                }
                else if (bytes[0] == 0xFF && bytes[1] == 0xFE)
                {
                    Interlocked.Increment(ref OtherViaBOM);

                    if (bytes[2] != 0 || bytes[3] != 0)
                    {
                        return FileEncoding.UTF16_LittleEndian;
                    }
                    else
                    {
                        return FileEncoding.UTF32_LittleEndian;
                    }
                }
                else if (bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0xFE && bytes[3] == 0xFF)
                {
                    Interlocked.Increment(ref OtherViaBOM);

                    return FileEncoding.UTF32_BigEndian;
                }
                else if (bytes.Length >= 14 && bytes[0] == 0x5A && bytes[1] == 0x4D && bytes[12] == 0xFF && bytes[13] == 0xFF)
                {
                    Interlocked.Increment(ref FilteredFileCount);

                    // PE 'MZ' header, then Dos Stub, or at least illegal UTF-8.
                    return FileEncoding.Binary;
                }
            }

            bool possibleUTF8 = ApparentlyValidUTF8(bytes);

            if(possibleUTF8)
            {
                Interlocked.Increment(ref PossibleUTF8);
                return FileEncoding.PossibleUTF8;
            }
            else
            {
                Interlocked.Increment(ref FilteredFileCount);
                return FileEncoding.NotLegalUTF8;
            }
        }

        private static bool ApparentlyValidUTF8(Span<byte> bytes)
        {
            for (int i = 0; i < bytes.Length - 4; ++i)
            {
                if (bytes[i] < 0x80) { continue; }

                if (bytes[i] < 0xC2)
                {
                    return false;
                }
                else if (bytes[i] >= 0xF5)
                {
                    return false;
                }
                else if (bytes[i] > 0xF0)
                {
                    if (!HasContinuationBytes(bytes, i, 3))
                    {
                        return false;
                    }
                    else
                    {
                        i += 3;
                    }
                }
                else if (bytes[i] > 0xE0)
                {
                    if (!HasContinuationBytes(bytes, i, 2))
                    {
                        return false;
                    }
                    else
                    {
                        i += 2;
                    }
                }
                else if (bytes[i] > 0xC1)
                {
                    if (!HasContinuationBytes(bytes, i, 1))
                    {
                        return false;
                    }
                    else
                    {
                        i += 1;
                    }
                }
            }

            return true;
        }

        private static bool HasContinuationBytes(Span<byte> bytes, int index, int count)
        {
            for (int i = index + 1; i <= index + count; ++i)
            {
                if (bytes[i] < 0x80 || bytes[i] >= 0xC0) { return false; }
            }

            return true;
        }
    }
}
