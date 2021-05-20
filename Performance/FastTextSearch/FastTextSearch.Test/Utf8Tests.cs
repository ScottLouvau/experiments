// Copyright (c) Scott Louvau. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Text;

using Xunit;

namespace FastTextSearch.Test
{
    public class Utf8Tests : TestBase
    {
        [Fact]
        public void Utf8_IsInvalidUtf8()
        {
            // Empty/Null return as invalid (nothing meaningful to find anyway)
            Assert.True(Utf8.IsInvalidSequence(null));
            Assert.True(Utf8.IsInvalidSequence(new byte[0]));

            // All Ascii
            Assert.False(Utf8.IsInvalidSequence(Encoding.UTF8.GetBytes("Hello")));

            // All continuations (no invalid pairs to find)
            Assert.False(Utf8.IsInvalidSequence(new byte[] { 0x80, 0x81, 0x95, 0xA1, 0xB3, 0xBF }));

            // Multi-byte with continuations
            Assert.False(Utf8.IsInvalidSequence(new byte[] { 0xE0, 0x81, 0x82, 0xD1, 0xB0, 0xB1 }));

            // Ascii with continuation (invalid)
            Assert.True(Utf8.IsInvalidSequence(new byte[] { 0x50, 0x51, 0x82 }));

            // Multi-byte without continuation (invalid)
            Assert.True(Utf8.IsInvalidSequence(new byte[] { 0x50, 0xC1, 0x52 }));
        }

        [Fact]
        public void Utf8_ByteClassifications()
        {
            Assert.True(Utf8.IsSingleByte((byte)'\0'));
            Assert.True(Utf8.IsSingleByte((byte)'\n'));
            Assert.True(Utf8.IsSingleByte((byte)'a'));
            Assert.True(Utf8.IsSingleByte((byte)'S'));
            Assert.True(Utf8.IsSingleByte(0x7F));
            Assert.False(Utf8.IsSingleByte(0x80));
            Assert.False(Utf8.IsSingleByte(0x99));
            Assert.False(Utf8.IsSingleByte(0xA7));
            Assert.False(Utf8.IsSingleByte(0xBF));
            Assert.False(Utf8.IsSingleByte(0xC0));
            Assert.False(Utf8.IsSingleByte(0xDA));
            Assert.False(Utf8.IsSingleByte(0xFF));

            Assert.False(Utf8.IsContinuationByte((byte)'\0'));
            Assert.False(Utf8.IsContinuationByte((byte)'\n'));
            Assert.False(Utf8.IsContinuationByte((byte)'a'));
            Assert.False(Utf8.IsContinuationByte((byte)'S'));
            Assert.False(Utf8.IsContinuationByte(0x7F));
            Assert.True(Utf8.IsContinuationByte(0x80));
            Assert.True(Utf8.IsContinuationByte(0x99));
            Assert.True(Utf8.IsContinuationByte(0xA7));
            Assert.True(Utf8.IsContinuationByte(0xBF));
            Assert.False(Utf8.IsContinuationByte(0xC0));
            Assert.False(Utf8.IsContinuationByte(0xDA));
            Assert.False(Utf8.IsContinuationByte(0xFF));

            Assert.False(Utf8.IsMultiByteStart((byte)'\0'));
            Assert.False(Utf8.IsMultiByteStart((byte)'\n'));
            Assert.False(Utf8.IsMultiByteStart((byte)'a'));
            Assert.False(Utf8.IsMultiByteStart((byte)'S'));
            Assert.False(Utf8.IsMultiByteStart(0x7F));
            Assert.False(Utf8.IsMultiByteStart(0x80));
            Assert.False(Utf8.IsMultiByteStart(0x99));
            Assert.False(Utf8.IsMultiByteStart(0xA7));
            Assert.False(Utf8.IsMultiByteStart(0xBF));
            Assert.True(Utf8.IsMultiByteStart(0xC0));
            Assert.True(Utf8.IsMultiByteStart(0xDA));
            Assert.True(Utf8.IsMultiByteStart(0xFF));
        }

        [Fact]
        public void Utf8_Codepoints()
        {
            Assert.Equal(0, Utf8.CodepointCount((Span<byte>)null));
            Assert.Equal(0, Utf8.CodepointCount(new byte[0]));

            Assert.Equal(5, Utf8.CodepointCount(Encoding.UTF8.GetBytes("Hello")));
            Assert.Equal(4, Utf8.CodepointCount(Encoding.UTF8.GetBytes("©λ⅔👍")));
        }

        [Fact]
        public void Utf8_CountAndLastIndex()
        {
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
            int newlines = Utf8.CountAndLastIndex((byte)'\n', beforeMatch, out int lastNewline);
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
