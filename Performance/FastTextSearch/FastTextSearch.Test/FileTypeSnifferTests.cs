using System.IO;
using System.Reflection;
using System.Text;
using Xunit;

namespace FastTextSearch.Test
{
    public class FileTypeSnifferTests : TestBase
    {
        [Fact]
        public void FileSniffer_Prefixes()
        {
            FileSniffResult result;

            // Null/Empty
            result = FileSniffer.Sniff(null);
            Assert.Equal(FileTypeDetected.OtherNonUtf8, result.Type);

            result = FileSniffer.Sniff(new byte[0]);
            Assert.Equal(FileTypeDetected.OtherNonUtf8, result.Type);

            // Short UTF-8
            result = FileSniffer.Sniff(Encoding.UTF8.GetBytes("hello"));
            Assert.Equal(FileTypeDetected.UTF8, result.Type);
            Assert.Equal(0, result.BomByteCount);
            Assert.False(result.BomFound);

            // Multi-byte without continuation (invalid)
            result = FileSniffer.Sniff(new byte[] { 0x50, 0xC1, 0x52 });
            Assert.Equal(FileTypeDetected.OtherNonUtf8, result.Type);

            // UTF-8, no BOM
            result = FileSniffer.Sniff(File.ReadAllBytes(Path.Combine(ContentFolderPath, "Simple.UTF8.NoBOM.txt")));
            Assert.Equal(FileTypeDetected.UTF8, result.Type);
            Assert.Equal(0, result.BomByteCount);
            Assert.False(result.BomFound);

            // UTF-8 w/BOM
            result = FileSniffer.Sniff(File.ReadAllBytes(Path.Combine(ContentFolderPath, "Simple.UTF8.WithBOM.txt")));
            Assert.Equal(FileTypeDetected.UTF8, result.Type);
            Assert.Equal(3, result.BomByteCount);
            Assert.True(result.BomFound);

            // UTF-16 Little Endian (w/BOM)
            result = FileSniffer.Sniff(File.ReadAllBytes(Path.Combine(ContentFolderPath, "Simple.UTF16.LE.txt")));
            Assert.Equal(FileTypeDetected.UnicodeOther, result.Type);
            Assert.Equal(2, result.BomByteCount);
            Assert.True(result.BomFound);

            // UTF-16 Big Endian (w/BOM)
            result = FileSniffer.Sniff(File.ReadAllBytes(Path.Combine(ContentFolderPath, "Simple.UTF16.BE.txt")));
            Assert.Equal(FileTypeDetected.UnicodeOther, result.Type);
            Assert.Equal(2, result.BomByteCount);
            Assert.True(result.BomFound);

            // UTF-32 LE
            result = FileSniffer.Sniff(File.ReadAllBytes(Path.Combine(ContentFolderPath, "Simple.UTF32.LE.txt")));
            Assert.Equal(FileTypeDetected.UnicodeOther, result.Type);
            Assert.Equal(4, result.BomByteCount);
            Assert.True(result.BomFound);

            // UTF-32 BE
            result = FileSniffer.Sniff(File.ReadAllBytes(Path.Combine(ContentFolderPath, "Simple.UTF32.BE.txt")));
            Assert.Equal(FileTypeDetected.UnicodeOther, result.Type);
            Assert.Equal(4, result.BomByteCount);
            Assert.True(result.BomFound);

            // EXE
            result = FileSniffer.Sniff(File.ReadAllBytes(Path.Combine(ContentFolderPath, "HelloWorld.exe")));
            Assert.Equal(FileTypeDetected.Executable, result.Type);

            // DLL
            result = FileSniffer.Sniff(File.ReadAllBytes(Path.Combine(ContentFolderPath, "HelloWorld.dll")));
            Assert.Equal(FileTypeDetected.Executable, result.Type);

            // PDB (Portable)
            result = FileSniffer.Sniff(File.ReadAllBytes(Path.Combine(ContentFolderPath, "HelloWorld.pdb")));
            Assert.Equal(FileTypeDetected.DebugSymbols, result.Type);

            // PDB (Older Format)
            result = FileSniffer.Sniff(File.ReadAllBytes(Path.Combine(ContentFolderPath, "HelloWorld.NonPortable.pdb")));
            Assert.Equal(FileTypeDetected.DebugSymbols, result.Type);

            // ZIP
            result = FileSniffer.Sniff(File.ReadAllBytes(Path.Combine(ContentFolderPath, "Simple.zip")));
            Assert.Equal(FileTypeDetected.Compressed, result.Type);

            // ZLIB
            result = FileSniffer.Sniff(File.ReadAllBytes(Path.Combine(ContentFolderPath, "GitObject.zlib")));
            Assert.Equal(FileTypeDetected.Compressed, result.Type);

            // GZIP
            result = FileSniffer.Sniff(File.ReadAllBytes(Path.Combine(ContentFolderPath, "Simple.gz")));
            Assert.Equal(FileTypeDetected.Compressed, result.Type);

            // CAB
            result = FileSniffer.Sniff(File.ReadAllBytes(Path.Combine(ContentFolderPath, "Simple.cab")));
            Assert.Equal(FileTypeDetected.Compressed, result.Type);

            // JPG
            result = FileSniffer.Sniff(File.ReadAllBytes(Path.Combine(ContentFolderPath, "Simple.jpg")));
            Assert.Equal(FileTypeDetected.Image, result.Type);

            // PNG
            result = FileSniffer.Sniff(File.ReadAllBytes(Path.Combine(ContentFolderPath, "Simple.png")));
            Assert.Equal(FileTypeDetected.Image, result.Type);
        }
    }
}
