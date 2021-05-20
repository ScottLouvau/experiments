using System;
using System.IO;
using Xunit;

namespace FastTextSearch.Test
{
    public class VectorTests : TestBase
    {
        [Fact]
        public void Vector_CountAndLastIndex()
        {
            // == Utf8_CountAndLastIndex, but calling Vector.CountAndLastIndex inside

            byte[] text = File.ReadAllBytes(Path.Combine(ContentFolderPath, "HelloWorld.cs"));

            Span<byte> content = text;
            int lineNumber = 1;
            int charInLine = 1;

            Assert.Equal("(9, 31)", Next('"', ref content, ref lineNumber, ref charInLine));
            Assert.Equal("(9, 46)", Next('"', ref content, ref lineNumber, ref charInLine));
            Assert.Equal("(10, 31)", Next('"', ref content, ref lineNumber, ref charInLine));
            Assert.Equal("(10, 33)", Next('"', ref content, ref lineNumber, ref charInLine));
            Assert.Equal("(11, 31)", Next('"', ref content, ref lineNumber, ref charInLine));
            Assert.Equal("(11, 33)", Next('"', ref content, ref lineNumber, ref charInLine));
            Assert.Equal("(12, 31)", Next('"', ref content, ref lineNumber, ref charInLine));
            Assert.Equal("(12, 33)", Next('"', ref content, ref lineNumber, ref charInLine));
            Assert.Null(Next('"', ref content, ref lineNumber, ref charInLine));

            // Look for ';' and then ' ', too close together for the vector loop but containing a newline
            content = text;
            lineNumber = 1;
            charInLine = 1;

            Assert.Equal("(1, 14)", Next(';', ref content, ref lineNumber, ref charInLine));
            Assert.Equal("(9, 48)", Next(';', ref content, ref lineNumber, ref charInLine));
            Assert.Equal("(10, 1)", Next(' ', ref content, ref lineNumber, ref charInLine));
        }

        private string Next(char b, ref Span<byte> content, ref int lineNumber, ref int charInLine)
        {
            int next = content.IndexOf((byte)b);
            if (next == -1) { return null; }

            // Find lineNumber and charInLine of match
            Span<byte> beforeMatch = content.Slice(0, next);
            int newlines = Vector.CountAndLastIndex((byte)'\n', beforeMatch, out int lastNewline);
            if (newlines > 0)
            {
                lineNumber += newlines;
                charInLine = 1;
                beforeMatch = beforeMatch.Slice(lastNewline + 1);
            }

            charInLine += Utf8.CodepointCount(beforeMatch);

            // Move content to byte after match for next search
            charInLine++;
            content = content.Slice(next + 1);

            return $"({lineNumber}, {charInLine - 1})";
        }
    }
}
