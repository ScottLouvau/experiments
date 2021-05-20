using System;
using System.IO;
using Xunit;

namespace StringSearch.Test
{
    public class FilePositionTests : TestBase
    {
        [Fact]
        public void FilePosition_UpdateByte()
        {
            string filePath = "HelloWorld.cs";
            FilePosition current = FilePosition.Start(filePath);

            Assert.Equal(0, current.ByteOffset);
            Assert.Equal(0, current.CharOffset);
            Assert.Equal(1, current.LineNumber);
            Assert.Equal(1, current.CharInLine);
            Assert.Equal(filePath, current.FilePath);
            Assert.Equal("HelloWorld.cs (1, 1)", current.ToString());

            byte[] text = File.ReadAllBytes(Path.Combine(ContentFolderPath, "HelloWorld.cs"));
            Span<byte> content = text;

            Assert.Equal("(9, 31)", Next('"', ref current, ref content)?.LineAndChar);
            Assert.Equal("(9, 46)", Next('"', ref current, ref content)?.LineAndChar);
            Assert.Equal("(10, 31)", Next('"', ref current, ref content)?.LineAndChar);
            Assert.Equal("(10, 33)", Next('"', ref current, ref content)?.LineAndChar);
            Assert.Equal("(11, 31)", Next('"', ref current, ref content)?.LineAndChar);
            Assert.Equal("(11, 33)", Next('"', ref current, ref content)?.LineAndChar);
            Assert.Equal("(12, 31)", Next('"', ref current, ref content)?.LineAndChar);
            Assert.Equal("(12, 33)", Next('"', ref current, ref content)?.LineAndChar);

            Assert.Equal("HelloWorld.cs (12, 34)", current.ToString());
            Assert.Equal(278, current.ByteOffset);
            Assert.Equal((byte)'"', text[277]);

            Assert.Null(Next('"', ref current, ref content));

            current = FilePosition.Start(filePath);
            content = text;
            Assert.Equal("(7, 21)", Next('G', ref current, ref content)?.LineAndChar);
            Assert.Equal("(8, 1)", Next(' ', ref current, ref content)?.LineAndChar);
            Assert.Equal("(9, 48)", Next(';', ref current, ref content)?.LineAndChar);
            Assert.Equal("(9, 49)", Next('\r', ref current, ref content)?.LineAndChar);
        }

        [Fact]
        public void FilePosition_UpdateString()
        {
            string filePath = "HelloWorld.cs";
            FilePosition current = FilePosition.Start(filePath);

            Assert.Equal(0, current.ByteOffset);
            Assert.Equal(0, current.CharOffset);
            Assert.Equal(1, current.LineNumber);
            Assert.Equal(1, current.CharInLine);
            Assert.Equal(filePath, current.FilePath);
            Assert.Equal("HelloWorld.cs (1, 1)", current.ToString());

            string text = File.ReadAllText(Path.Combine(ContentFolderPath, "HelloWorld.cs"));
            ReadOnlySpan<char> content = text;

            Assert.Equal("(9, 31)", Next('"', ref current, ref content)?.LineAndChar);
            Assert.Equal("(9, 46)", Next('"', ref current, ref content)?.LineAndChar);
            Assert.Equal("(10, 31)", Next('"', ref current, ref content)?.LineAndChar);
            Assert.Equal("(10, 33)", Next('"', ref current, ref content)?.LineAndChar);
            Assert.Equal("(11, 31)", Next('"', ref current, ref content)?.LineAndChar);
            Assert.Equal("(11, 33)", Next('"', ref current, ref content)?.LineAndChar);
            Assert.Equal("(12, 31)", Next('"', ref current, ref content)?.LineAndChar);

            // 👍 is one codepoint but two .NET UTF-16 chars
            // [Reported as (12, 34) in Visual Studio 2019, but (12, 33) in VS Code]
            Assert.Equal("(12, 34)", Next('"', ref current, ref content)?.LineAndChar);

            Assert.Equal("HelloWorld.cs (12, 35)", current.ToString());
            Assert.Equal(269, current.CharOffset);
            Assert.Equal('"', text[268]);

            Assert.Null(Next('"', ref current, ref content));

            current = FilePosition.Start(filePath);
            content = text;
            Assert.Equal("(7, 21)", Next('G', ref current, ref content)?.LineAndChar);
            Assert.Equal("(8, 1)", Next(' ', ref current, ref content)?.LineAndChar);
            Assert.Equal("(9, 48)", Next(';', ref current, ref content)?.LineAndChar);
            Assert.Equal("(9, 49)", Next('\r', ref current, ref content)?.LineAndChar);
        }

        private FilePosition? Next(char b, ref FilePosition current, ref Span<byte> content)
        {
            // Look for a match
            int nextMatch = content.IndexOf((byte)b);
            if (nextMatch == -1) { return null; }

            // Map the index to a Line and Char
            FilePosition match = FilePosition.Update(current, content.Slice(0, nextMatch));

            // Reset the Span and Position to the next char (so the next search will find the next occurrence)
            current = match;
            current.ByteOffset++;
            current.CharInLine++;
            content = content.Slice(nextMatch + 1);

            return match;
        }

        private FilePosition? Next(char c, ref FilePosition current, ref ReadOnlySpan<char> content)
        {
            // Look for a match
            int nextMatch = content.IndexOf(c);
            if (nextMatch == -1) { return null; }

            // Map the index to a Line and Char
            FilePosition match = FilePosition.Update(current, content.Slice(0, nextMatch));

            // Reset the Span and Position to the next char (so the next search will find the next occurrence)
            current = match;
            current.CharOffset++;
            current.CharInLine++;
            content = content.Slice(nextMatch + 1);

            return match;
        }
    }
}
