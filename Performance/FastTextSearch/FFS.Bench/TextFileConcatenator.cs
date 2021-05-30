using FastTextSearch;
using System;
using System.IO;
namespace FFS.Bench
{
    public class TextFileConcatenator
    {
        public static void ConcatenateTextUnder(string rootPath, string concatenatedFilePath)
        {
            using (var outStream = File.Create(concatenatedFilePath))
            {
                foreach (string filePath in Directory.GetFiles(rootPath, "*.*", SearchOption.AllDirectories))
                {
                    Span<byte> text = File.ReadAllBytes(filePath);
                    FileSniffResult result = FileSniffer.Sniff(text.Slice(0, Math.Min(text.Length, 1024)));
                    if (result.Type == FileTypeDetected.UTF8)
                    {
                        text = text.Slice(result.BomByteCount);
                        outStream.Write(text);
                    }
                }
            }
        }
    }
}
