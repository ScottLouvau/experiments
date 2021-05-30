using FastTextSearch;
using RoughBench;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FFS.Bench
{
    public class Sniffer
    {
        public static void Assess(string rootPath)
        {
            ConsoleTable t = new ConsoleTable(rootPath, "Files", "Size");

            FileInfo[] files = new DirectoryInfo(rootPath).GetFiles("*.*", SearchOption.AllDirectories);
            Stats(t, "All", files);

            HashSet<string> textSet = new HashSet<string>(TextFilePaths(files));
            FileInfo[] textFiles = files.Where((fi) => textSet.Contains(fi.FullName)).ToArray();
            Stats(t, "Text", textFiles);

            Stats(t, "Small", textFiles.Where((fi) => fi.Length < 64 * 1024));
            Stats(t, "Static", textFiles.Where((fi) => fi.CreationTimeUtc >= fi.LastWriteTimeUtc));

            DateTime cutoff = DateTime.UtcNow.AddDays(-7);
            Stats(t, "Stale", textFiles.Where((fi) => fi.LastWriteTimeUtc < cutoff));

            Stats(t, "Stale Index", textFiles.Where((fi) => fi.Length < 64 * 1024 && fi.LastWriteTimeUtc < cutoff));
            Stats(t, "Fresh Index", textFiles.Where((fi) => fi.Length < 64 * 1024 && fi.LastWriteTimeUtc >= cutoff));
        }

        static void Stats(ConsoleTable table, string name, IEnumerable<FileInfo> files)
        {
            table.AppendRow(TableCell.String(name), new TableCell($"{files.Count():n0}", Align.Right), TableCell.Size(files.Sum((fi) => fi.Length)));
        }

        public static string[] TextFilePaths(string rootPath)
        {
            return TextFilePaths(new DirectoryInfo(rootPath).GetFiles("*.*", SearchOption.AllDirectories));
        }

        public static string[] TextFilePaths(FileInfo[] allFiles)
        {
            List<string> textFilePaths = new List<string>();

            Parallel.ForEach(allFiles, (fi) =>
            {
                string filePath = fi.FullName;

                byte[] array = ArrayPool<byte>.Shared.Rent(1024);
                Span<byte> buffer = array.AsSpan().Slice(0, 1024);

                using (var inStream = File.OpenRead(filePath))
                {
                    int bytesRead = inStream.Read(buffer);
                    Span<byte> prefix = buffer.Slice(0, bytesRead);

                    FileSniffResult result = FileSniffer.Sniff(prefix);
                    if (result.Type == FileTypeDetected.UTF8)
                    {
                        lock (textFilePaths)
                        {
                            textFilePaths.Add(filePath);
                        }
                    }
                }

                ArrayPool<byte>.Shared.Return(array);
            });

            return textFilePaths.ToArray();
        }

        public static void ConcatenateTextFilesUnder(string rootPath, string concatenatedFilePath)
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
