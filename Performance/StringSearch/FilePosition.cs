using System;

namespace StringSearch
{
    public struct FilePosition
    {
        public string FilePath;

        public long ByteOffset;
        public long CharOffset;

        public int LineNumber;
        public int CharInLine;

        public string LineAndChar => $"({LineNumber}, {CharInLine})";
        public string Offset => (CharOffset > 0 ? $"[{CharOffset:n0}c]" : $"[{ByteOffset:n0}b]");

        public static FilePosition Start(string filePath) => new FilePosition() { FilePath = filePath, LineNumber = 1, CharInLine = 1 };

        public override string ToString()
        {
            return $"{FilePath} {LineAndChar}";
        }

        public static int FileLineCharOrder(FilePosition left, FilePosition right)
        {
            int cmp = left.FilePath.CompareTo(right.FilePath);
            if (cmp != 0) { return cmp; }

            cmp = left.LineNumber.CompareTo(right.LineNumber);
            if (cmp != 0) { return cmp; }

            return left.CharInLine.CompareTo(right.CharInLine);
        }

        public static FilePosition Update(FilePosition start, ReadOnlySpan<char> content)
        {
            FilePosition current = start;

            current.ByteOffset = 0;
            current.CharOffset += content.Length;

            // Count lines to the end of the buffer
            int newlines = Utf8.CountAndLastIndex('\n', content, out int lastNewlineIndex);
            if (newlines > 0)
            {
                current.LineNumber += newlines;
                current.CharInLine = 1;
                content = content.Slice(lastNewlineIndex + 1);
            }

            current.CharInLine += content.Length;

            return current;
        }

        public static FilePosition Update(FilePosition start, ReadOnlySpan<byte> content)
        {
            FilePosition current = start;

            current.ByteOffset += content.Length;
            current.CharOffset = 0;

            // Count lines to the end of the buffer
            int newlines = Utf8.CountAndLastIndex((byte)'\n', content, out int lastNewlineIndex);
            if (newlines > 0)
            {
                current.LineNumber += newlines;
                current.CharInLine = 1;
                content = content.Slice(lastNewlineIndex + 1);
            }

            // Count codepoints after the last newline
            current.CharInLine += Utf8.CodepointCount(content);

            return current;
        }
    }
}