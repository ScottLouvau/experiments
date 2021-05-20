﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FastTextSearch
{
    public interface IFileSearcher
    {
        List<FilePosition> Search(string filePath);
    }

    public enum FileSearcher
    {
        Utf8,
        DotNet,
    }

    public static class FileSearcherFactory
    {
        public static IFileSearcher Build(string valueToFind, bool scanFilePrefix = true, FileSearcher searcher = FileSearcher.Utf8)
        {
            switch (searcher)
            {
                case FileSearcher.Utf8:
                    return new Utf8Searcher(valueToFind, scanFilePrefix);

                case FileSearcher.DotNet:
                    return new DotNetSearcher(valueToFind, scanFilePrefix);

                default:
                    throw new NotImplementedException($"FileSearcher {searcher} unknown.");
            }
        }
    }

    public class DotNetSearcher : IFileSearcher
    {
        public string ValueToFind { get; }
        private bool ScanFilePrefix { get; }

        public DotNetSearcher(string valueToFind, bool scanFilePrefix = true)
        {
            ValueToFind = valueToFind;
            ScanFilePrefix = scanFilePrefix;
        }

        public List<FilePosition> Search(string filePath)
        {
            List<FilePosition> matches = null;
            ReadOnlySpan<char> contents = null;

            using (Stream stream = File.OpenRead(filePath))
            {
                if (ScanFilePrefix && IsNotUnicode(stream))
                {
                    return null;
                }

                using (StreamReader reader = new StreamReader(stream))
                {
                    contents = reader.ReadToEnd();
                }
            }

            FilePosition current = FilePosition.Start(filePath);

            while (true)
            {
                int matchIndex = contents.IndexOf(this.ValueToFind, StringComparison.Ordinal);
                if (matchIndex == -1) { break; }

                current = FilePosition.Update(current, contents.Slice(0, matchIndex));
                matches ??= new List<FilePosition>();
                matches.Add(current);

                current.CharOffset++;
                current.CharInLine++;
                contents = contents.Slice(matchIndex + 1);
            }

            return matches;
        }

        private bool IsNotUnicode(Stream stream)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(FileTypeSniffer.RecommendedSniffBytes);

            try
            {
                int bytesRead = stream.Read(buffer);
                stream.Seek(0, SeekOrigin.Begin);

                FileSniffResult result = FileTypeSniffer.Sniff(buffer.AsSpan().Slice(0, bytesRead));
                return (result.Type != FileTypeDetected.UTF8 && result.Type != FileTypeDetected.UnicodeOther);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    public class Utf8Searcher : IFileSearcher
    {
        private const int BlockSizeBytes = 512 * 1024;
        private const int FirstBlockSizeBytes = 64 * 1024;

        private byte[] ValueToFind { get; }
        private bool ScanFilePrefix { get; }
        private IFileSearcher Fallback { get; }

        public Utf8Searcher(string valueToFind, bool scanFilePrefix = true)
        {
            ValueToFind = Encoding.UTF8.GetBytes(valueToFind);
            ScanFilePrefix = scanFilePrefix;
            Fallback = new DotNetSearcher(valueToFind, scanFilePrefix: false);
        }

        public List<FilePosition> Search(string filePath)
        {
            List<FilePosition> matches = null;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(BlockSizeBytes);

            try
            {
                using (Stream stream = File.OpenRead(filePath))
                {
                    long totalBytesRead = 0;
                    long totalFileBytes = stream.Length - stream.Position;

                    // Read the first block
                    int bytesRead = stream.Read(buffer.AsSpan().Slice(0, FirstBlockSizeBytes));
                    totalBytesRead += bytesRead;
                    Span<byte> content = buffer.AsSpan().Slice(0, bytesRead);
                    FilePosition current = FilePosition.Start(filePath);

                    // Sniff the file; fall back for UTF-16/32 and stop with no matches for non-UTF-8
                    if (ScanFilePrefix)
                    {
                        FileSniffResult result = FileTypeSniffer.Sniff(content.Slice(0, Math.Min(content.Length, FileTypeSniffer.RecommendedSniffBytes)));
                        if (result.Type == FileTypeDetected.UnicodeOther)
                        {
                            return Fallback.Search(filePath);
                        }
                        else if (result.Type != FileTypeDetected.UTF8)
                        {
                            return null;
                        }

                        // Skip BOM bytes, if detected
                        if (result.BomFound)
                        {
                            current.ByteOffset += result.BomByteCount;
                            content = content.Slice(result.BomByteCount);
                        }
                    }

                    while (true)
                    {
                        int startIndex = 0;

                        // Look for matches in the buffer
                        while (true)
                        {
                            int matchIndex = content.Slice(startIndex).IndexOf(ValueToFind);
                            if (matchIndex == -1) { break; }

                            current = FilePosition.Update(current, content.Slice(0, startIndex + matchIndex));
                            matches ??= new List<FilePosition>();
                            matches.Add(current);

                            content = content.Slice(startIndex + matchIndex);
                            startIndex = 1;
                        }

                        // Stop if all bytes have been read
                        if (totalBytesRead >= totalFileBytes) { break; }

                        // Keep the last ValueToFind-1 bytes, in case a match was just off the end of the read buffer
                        if (content.Length >= ValueToFind.Length)
                        {
                            int bytesNotKept = (content.Length - (ValueToFind.Length - 1));
                            current = FilePosition.Update(current, content.Slice(0, bytesNotKept));
                            content = content.Slice(bytesNotKept);
                        }

                        // Copy unused bytes to buffer start
                        content.CopyTo(buffer);

                        // Read another block
                        bytesRead = stream.Read(buffer.AsSpan(content.Length));
                        totalBytesRead += bytesRead;
                        content = buffer.AsSpan().Slice(0, content.Length + bytesRead);
                    }

                    return matches;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}