using System;
using System.IO;
using Xunit;

namespace StringSearch.Test
{
    public class FilePositionTests : TestBase
    {
        [Fact]
        public void FilePosition_Update()
        {
            string filePath = "HelloWorld.cs";
            FilePosition current = FilePosition.Start(filePath);

            Assert.Equal(0, current.ByteOffset);
            Assert.Equal(0, current.CharOffset);
            Assert.Equal(1, current.LineNumber);
            Assert.Equal(1, current.CharInLine);
            Assert.Equal(filePath, current.FilePath);

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
            int nextMatch = content.IndexOf((byte)b);
            if (nextMatch == -1) { return null; }

            FilePosition match = FilePosition.Update(current, content.Slice(0, nextMatch));

            current = match;
            current.ByteOffset++;
            current.CharInLine++;
            content = content.Slice(nextMatch + 1);

            return match;
        }
    }
}
