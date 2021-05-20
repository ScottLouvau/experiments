using System;

namespace FastTextSearch
{
    public static class Utf8
    {
        /// <summary>
        ///  Looks for invalid UTF-8 byte patterns in the given bytes.
        /// </summary>
        /// <remarks>
        ///  In UTF-8, characters may be one to four bytes long.
        ///  Single byte characters are less than 0x80 (start with a 0 bit).
        ///  Multi-byte characters are 0xC0 or greater (start with 11 bits).
        ///  Continuation bytes (bytes after the first in a character) are 0x80 to 0xBF (start with 10 bits).
        ///  
        ///  Most non-UTF8 data will have a disallowed byte pair early on:
        ///   - A single byte character followed by a continuation,
        ///   - A multi-byte start not followed by a continuation.
        /// </remarks>
        /// <param name="bytes">First Bytes from file to scan</param>
        /// <returns>True if a byte pair invalid in UTF-8 detected, False if bytes could be valid UTF-8</returns>
        public static bool IsInvalidSequence(Span<byte> bytes)
        {
            // Invalid UTF-8 detection informed by Lemire's "Validating UTF-8 In Less Than One Instruction Per Byte"
            if (bytes == null || bytes.Length < 2) { return true; }

            byte last = bytes[0];

            for (int i = 1; i < bytes.Length; ++i)
            {
                byte current = bytes[i];

                if (IsContinuationByte(current))
                {
                    if (IsSingleByte(last))
                    {
                        return true;
                    }
                }
                else if (IsMultiByteStart(last))
                {
                    return true;
                }

                last = current;
            }

            return false;
        }

        /// <summary>
        ///  Returns the number of codepoints in a set of UTF-8 bytes.
        ///  A codepoint may be one to four bytes.
        ///  Codepoints don't always correspond to separate visual glyphs.
        /// </summary>
        /// <param name="content">UTF-8 bytes in which to count codepoints</param>
        /// <returns>Number of codepoints in content span</returns>
        public static int CodepointCount(ReadOnlySpan<byte> content)
        {
            if (content == null) { return 0; }

            int continuations = 0;

            for (int i = 0; i < content.Length; ++i)
            {
                if (IsContinuationByte(content[i]))
                {
                    continuations++;
                }
            }

            return content.Length - continuations;
        }

        /// <summary>
        ///  Returns the number of codepoints in a set of UTF-16 chars.
        ///  A codepoint may be one or two chars.
        ///  Codepoints don't always correspond to separate visual glyphs.
        /// </summary>
        /// <param name="content">UTF-16 chars in which to count codepoints</param>
        /// <returns>Number of codepoints in content span</returns>
        public static int CodepointCount(ReadOnlySpan<char> content)
        {
            if (content == null) { return 0; }

            int continuations = 0;

            for (int i = 0; i < content.Length; ++i)
            {
                if (char.IsHighSurrogate(content[i]))
                {
                    continuations++;
                }
            }

            return content.Length - continuations;
        }

        /// <summary>
        ///  Return the number of occurrences of a byte in the sequence,
        ///  and the index of the last one found.
        /// </summary>
        /// <param name="c">Byte to find</param>
        /// <param name="content">Span of bytes to search</param>
        /// <param name="lastIndex">Index where last match was found or -1</param>
        /// <returns>Number of occurrences found</returns>
        public static int CountAndLastIndex(byte c, ReadOnlySpan<byte> content, out int lastIndex)
        {
            lastIndex = -1;
            int count = 0;

            while (true)
            {
                int next = content.IndexOf(c);
                if (next == -1) { break; }

                count++;
                lastIndex += (next + 1);
                content = content.Slice(next + 1);
            }

            return count;
        }

        /// <summary>
        ///  Return the number of occurrences of a char in the sequence,
        ///  and the index of the last one found.
        /// </summary>
        /// <param name="c">Char to find</param>
        /// <param name="content">Span of chars to search</param>
        /// <param name="lastIndex">Index where last match was found or -1</param>
        /// <returns>Number of occurrences found</returns>
        public static int CountAndLastIndex(char c, ReadOnlySpan<char> content, out int lastIndex)
        {
            lastIndex = -1;
            int count = 0;

            while (true)
            {
                int next = content.IndexOf(c);
                if (next == -1) { break; }

                count++;
                lastIndex += (next + 1);
                content = content.Slice(next + 1);
            }

            return count;
        }

        public static bool IsSingleByte(byte b)
        {
            return (b < 0x80);
        }

        public static bool IsContinuationByte(byte b)
        {
            return (b >= 0x80 && b < 0xC0);
        }

        public static bool IsMultiByteStart(byte b)
        {
            return (b >= 0xC0);
        }
    }
}
