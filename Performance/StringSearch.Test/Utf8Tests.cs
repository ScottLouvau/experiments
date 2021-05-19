using System.Text;
using Xunit;

namespace StringSearch.Test
{
    public class Utf8Tests
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
            Assert.Equal(0, Utf8.CodepointCount(null));
            Assert.Equal(0, Utf8.CodepointCount(new byte[0]));

            Assert.Equal(5, Utf8.CodepointCount(Encoding.UTF8.GetBytes("Hello")));
            Assert.Equal(4, Utf8.CodepointCount(Encoding.UTF8.GetBytes("©λ⅔👍")));
        }
    }
}
