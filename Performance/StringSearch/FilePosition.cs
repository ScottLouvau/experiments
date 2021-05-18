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

        public override string ToString()
        {
            if (LineNumber > 0)
            {
                return $"{FilePath} ({LineNumber:n0}, {CharInLine:n0}) @C {CharOffset:n0}; @B {ByteOffset:n0}";
            }
            else if (ByteOffset == 0 && CharOffset > 0)
            {
                return $"{FilePath} @C {CharOffset:n0}";
            }
            else
            {
                return $"{FilePath} @B {ByteOffset:n0}";
            }
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

            while (true)
            {
                int nextNewline = content.IndexOf((byte)'\n');
                if (nextNewline == -1) { break; }

                current.LineNumber++;
                current.CharInLine = 1;

                content = content.Slice(nextNewline + 1);
            }

            int continuations = 0;
            for (int i = 0; i < content.Length; ++i)
            {
                if (content[i] >= 0x80 && content[i] < 0xC0) { continuations++; }
                //if (content[i] - 0x80 < 0x40) { continuations++; }
            }

            current.CharInLine += content.Length - continuations;
            return current;
        }
    }
}