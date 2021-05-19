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

        public static FilePosition Start(string filePath) => new FilePosition() { FilePath = filePath, LineNumber = 1, CharInLine = 1 };

        public override string ToString()
        {
            return $"{FilePath} {LineAndChar} @C {CharOffset:n0}; @B {ByteOffset:n0}";
        }

        public static FilePosition Update(FilePosition start, ReadOnlySpan<char> content)
        {
            FilePosition current = start;

            current.ByteOffset = 0;
            current.CharOffset += content.Length;

            while (true)
            {
                int nextNewline = content.IndexOf('\n');
                if (nextNewline == -1) { break; }

                current.LineNumber++;
                current.CharInLine = 1;

                content = content.Slice(nextNewline + 1);
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
            while (true)
            {
                int nextNewline = content.IndexOf((byte)'\n');
                if (nextNewline == -1) { break; }

                current.LineNumber++;
                current.CharInLine = 1;

                content = content.Slice(nextNewline + 1);
            }

            // Count codepoints after the last newline
            current.CharInLine += Utf8.CodepointCount(content);

            return current;
        }

        public static FilePosition Update2(FilePosition start, ReadOnlySpan<byte> content)
        {
            FilePosition current = start;

            current.ByteOffset += content.Length;
            current.CharOffset = 0;

            // Count lines to the end of the buffer
            current.LineNumber += Vector.NewlineCount(content, out int lastLineIndex);

            if (lastLineIndex > 0)
            {
                content = content.Slice(lastLineIndex + 1);
            }

            // Count codepoints after the last newline
            current.CharInLine += Utf8.CodepointCount(content);

            return current;
        }
    }
}