// Copyright (c) Scott Louvau. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Text;

using Xunit;

namespace FastTextSearch.Test
{
    public class VectorTests : TestBase
    {
        [Fact]
        public void Vector_CodepointCount()
        {
            Assert.Equal(0, Vector.CodepointCount((Span<byte>)null));
            Assert.Equal(0, Vector.CodepointCount(new byte[0]));

            // Partial-block fallback
            Assert.Equal(5, Vector.CodepointCount(Encoding.UTF8.GetBytes("Hello")));
            Assert.Equal(4, Vector.CodepointCount(Encoding.UTF8.GetBytes("©λ⅔👍")));
            Assert.Equal(30, Vector.CodepointCount(Encoding.UTF8.GetBytes("123456789012345678901234567890")));

            // In-block count
            Assert.Equal(32, Vector.CodepointCount(Encoding.UTF8.GetBytes("12345678901234567890123456789012")));
            Assert.Equal(34, Vector.CodepointCount(Encoding.UTF8.GetBytes("1234567890123456789012345678901234")));
            Assert.Equal(12, Vector.CodepointCount(Encoding.UTF8.GetBytes("👍👍👍👍👍👍👍👍👍👍👍👍")));
            Assert.Equal(16, Vector.CodepointCount(Encoding.UTF8.GetBytes("👍👍👍👍👍👍👍👍👍👍👍👍AAAA")));
            Assert.Equal(16, Vector.CodepointCount(Encoding.UTF8.GetBytes("©λ⅔👍©λ⅔👍©λ⅔👍©λ⅔👍")));

            // All continuations
            Assert.Equal(0, Vector.CodepointCount(Enumerable.Range(0x80, 0xBF - 0x80).Select(i => (byte)i).ToArray()));
        }

        [Fact]
        public void Vector_FilePositionUpdate()
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
            Assert.Equal("[278b]", current.Offset);

            Assert.Null(Next('"', ref current, ref content));

            current = FilePosition.Start(filePath);
            content = text;
            Assert.Equal("(7, 21)", Next('G', ref current, ref content)?.LineAndChar);
            Assert.Equal("(8, 1)", Next(' ', ref current, ref content)?.LineAndChar);
            Assert.Equal("(9, 48)", Next(';', ref current, ref content)?.LineAndChar);
            Assert.Equal("(9, 49)", Next('\r', ref current, ref content)?.LineAndChar);

            text = File.ReadAllBytes(@"C:\Code\bion\csharp\Bion.Console\Program.cs");
            content = text;
            current = FilePosition.Start(@"Bion.Console\Program.cs");

            Assert.Equal("(24, 7)", Next("Convert", ref current, ref content)?.LineAndChar);
            Assert.Equal("(27, 7)", Next("Convert", ref current, ref content)?.LineAndChar);
            Assert.Equal("(161, 39)", Next("Convert", ref current, ref content)?.LineAndChar);
            Assert.Equal("(164, 25)", Next("Convert", ref current, ref content)?.LineAndChar);


        }

        private FilePosition? Next(char b, ref FilePosition current, ref Span<byte> content)
        {
            // Look for a match
            int nextMatch = content.IndexOf((byte)b);
            if (nextMatch == -1) { return null; }

            // Map the index to a Line and Char
            FilePosition match = Vector.FilePositionUpdate(current, content.Slice(0, nextMatch));

            // Reset the Span and Position to the next char (so the next search will find the next occurrence)
            current = match;
            current.ByteOffset++;
            current.CharInLine++;
            content = content.Slice(nextMatch + 1);

            return match;
        }

        private FilePosition? Next(string valueToFind, ref FilePosition current, ref Span<byte> content)
        {
            Span<byte> bytesToFind = Encoding.UTF8.GetBytes(valueToFind);

            // Look for a match
            int nextMatch = Vector.IndexOf(bytesToFind, content);
            if (nextMatch == -1) { return null; }

            // Map the index to a Line and Char
            FilePosition match = Vector.FilePositionUpdate(current, content.Slice(0, nextMatch));

            // Reset the Span and Position to the next char (so the next search will find the next occurrence)
            current = match;
            current.ByteOffset++;
            current.CharInLine++;
            content = content.Slice(nextMatch + 1);

            return match;
        }

        [Fact]
        public void Vector_IndexOf_Byte()
        {
            // Empty string
            Assert.Equal("", VectorIndexOfAll('!', ""));

            // First/Middle/Last, falling through to after-block logic
            Assert.Equal("0", VectorIndexOfAll('H', "Hello!"));
            Assert.Equal("2, 3", VectorIndexOfAll('l', "Hello!"));
            Assert.Equal("5", VectorIndexOfAll('!', "Hello!"));
            Assert.Equal("", VectorIndexOfAll('+', "Hello!"));

            // Early/Middle/Late in block
            Assert.Equal("0", VectorIndexOfAll('T', "This is a slightly longer test to exercise the block search logic."));
            Assert.Equal("35", VectorIndexOfAll('x', "This is a slightly longer test to exercise the block search logic."));
            Assert.Equal("65", VectorIndexOfAll('.', "This is a slightly longer test to exercise the block search logic."));
            Assert.Equal("", VectorIndexOfAll('!', "This is a slightly longer test to exercise the block search logic."));

            Assert.Equal("11, 16, 19, 48, 60", VectorIndexOfAll('l', "This is a slightly longer test to exercise the block search logic."));

            Span<byte> helloWorldCs = File.ReadAllBytes(Path.Combine(ContentFolderPath, "HelloWorld.cs"));
            Assert.Equal(helloWorldCs.IndexOf((byte)'G').ToString(), VectorIndexOfAll((byte)'G', helloWorldCs));
        }

        [Fact]
        public void Vector_IndexOf()
        {
            // Empty
            Assert.Equal("", VectorIndexOfAll("text", ""));

            // Fallback to .NET (short string to find)
            Assert.Equal("0", VectorIndexOfAll("T", "This is a short sample."));
            Assert.Equal("22", VectorIndexOfAll(".", "This is a short sample."));
            Assert.Equal("2, 5", VectorIndexOfAll("is", "This is a short sample."));

            // First/Middle/Last, in after-block logic
            Assert.Equal("0", VectorIndexOfAll("This", "This is a short sample."));
            Assert.Equal("20", VectorIndexOfAll("le.", "This is a short sample."));
            Assert.Equal("6", VectorIndexOfAll("s a s", "This is a short sample."));

            // First/Middle/Last in main block
            Assert.Equal("0", VectorIndexOfAll("Thi", "This is a slightly longer test to exercise the block search logic."));
            Assert.Equal("0", VectorIndexOfAll("This is", "This is a slightly longer test to exercise the block search logic."));
            Assert.Equal("34", VectorIndexOfAll("exercise", "This is a slightly longer test to exercise the block search logic."));
            Assert.Equal("63", VectorIndexOfAll("ic.", "This is a slightly longer test to exercise the block search logic."));
            Assert.Equal("", VectorIndexOfAll("ic..", "This is a slightly longer test to exercise the block search logic."));

            // Partial overlapping real match
            Assert.Equal("28", VectorIndexOfAll("test", "This is a slightly longer tttest to exercise the block search logic."));

            // Search HelloWorld.cs (trim the BOM prefix)
            Span<byte> helloWorldCs = File.ReadAllBytes(Path.Combine(ContentFolderPath, "HelloWorld.cs"));
            helloWorldCs = helloWorldCs.Slice(3);

            Span<byte> valueToFind = Encoding.UTF8.GetBytes("using");
            Assert.Equal("0", VectorIndexOfAll(valueToFind, helloWorldCs));

            valueToFind = Encoding.UTF8.GetBytes("namespace");
            Assert.Equal("17", VectorIndexOfAll(Encoding.UTF8.GetBytes("namespace"), helloWorldCs));

            valueToFind = Encoding.UTF8.GetBytes("Console.WriteLine");
            Assert.Equal(IndexOfAll(valueToFind, helloWorldCs), VectorIndexOfAll(valueToFind, helloWorldCs));

            valueToFind = Encoding.UTF8.GetBytes("\r\n}");
            Assert.Equal(IndexOfAll(valueToFind, helloWorldCs), VectorIndexOfAll(valueToFind, helloWorldCs));
        }

        [Fact]
        public void Vector_CountAndLastIndex()
        {
            // == Utf8_CountAndLastIndex, but calling Vector.CountAndLastIndex inside

            byte[] text = File.ReadAllBytes(Path.Combine(ContentFolderPath, "HelloWorld.cs"));

            Span<byte> content = text;
            int lineNumber = 1;
            int charInLine = 1;

            Assert.Equal("(9, 31)", VectorCountAndIndexNext('"', ref content, ref lineNumber, ref charInLine));
            Assert.Equal("(9, 46)", VectorCountAndIndexNext('"', ref content, ref lineNumber, ref charInLine));
            Assert.Equal("(10, 31)", VectorCountAndIndexNext('"', ref content, ref lineNumber, ref charInLine));
            Assert.Equal("(10, 33)", VectorCountAndIndexNext('"', ref content, ref lineNumber, ref charInLine));
            Assert.Equal("(11, 31)", VectorCountAndIndexNext('"', ref content, ref lineNumber, ref charInLine));
            Assert.Equal("(11, 33)", VectorCountAndIndexNext('"', ref content, ref lineNumber, ref charInLine));
            Assert.Equal("(12, 31)", VectorCountAndIndexNext('"', ref content, ref lineNumber, ref charInLine));
            Assert.Equal("(12, 33)", VectorCountAndIndexNext('"', ref content, ref lineNumber, ref charInLine));
            Assert.Null(VectorCountAndIndexNext('"', ref content, ref lineNumber, ref charInLine));

            // Look for ';' and then ' ', too close together for the vector loop but containing a newline
            content = text;
            lineNumber = 1;
            charInLine = 1;

            Assert.Equal("(1, 14)", VectorCountAndIndexNext(';', ref content, ref lineNumber, ref charInLine));
            Assert.Equal("(9, 48)", VectorCountAndIndexNext(';', ref content, ref lineNumber, ref charInLine));
            Assert.Equal("(10, 1)", VectorCountAndIndexNext(' ', ref content, ref lineNumber, ref charInLine));
        }

        private string VectorIndexOfAll(char b, string content)
        {
            return VectorIndexOfAll((byte)b, Encoding.UTF8.GetBytes(content));
        }

        private string VectorIndexOfAll(byte b, ReadOnlySpan<byte> content)
        {
            StringBuilder results = new StringBuilder();

            int startIndex = 0;
            while (true)
            {
                int next = Vector.IndexOf(b, content.Slice(startIndex));
                if (next == -1) { break; }

                if (results.Length > 0) { results.Append(", "); }
                results.Append(startIndex + next);

                startIndex += next + 1;
            }

            return results.ToString();
        }

        private string VectorIndexOfAll(string valueToFind, string content)
        {
            return VectorIndexOfAll(Encoding.UTF8.GetBytes(valueToFind), Encoding.UTF8.GetBytes(content));
        }

        private string VectorIndexOfAll(ReadOnlySpan<byte> valueToFind, ReadOnlySpan<byte> content)
        {
            StringBuilder results = new StringBuilder();

            int startIndex = 0;
            while (true)
            {
                int next = Vector.IndexOf(valueToFind, content.Slice(startIndex));
                if (next == -1) { break; }

                if (results.Length > 0) { results.Append(", "); }
                results.Append(startIndex + next);

                startIndex += next + 1;
            }

            return results.ToString();
        }

        private string IndexOfAll(ReadOnlySpan<byte> valueToFind, ReadOnlySpan<byte> content)
        {
            StringBuilder results = new StringBuilder();

            int startIndex = 0;
            while (true)
            {
                int next = content.Slice(startIndex).IndexOf(valueToFind);
                if (next == -1) { break; }

                if (results.Length > 0) { results.Append(", "); }
                results.Append(startIndex + next);

                startIndex += next + 1;
            }

            return results.ToString();
        }

        private string VectorCountAndIndexNext(char b, ref Span<byte> content, ref int lineNumber, ref int charInLine)
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
