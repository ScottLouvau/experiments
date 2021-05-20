// Copyright (c) Scott Louvau. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace FastTextSearch
{
    /* Goals:
     *   - Very fast compared to Parallel File.ReadAllText and string.IndexOf.
     *   - Constrain memory use (1 MB / core or less).
     *   - Minimize unused bytes read (identify binaries with prefix read only).
     *   - Minimize disk read count (read most useful files in one read, and nearly all in few more)
     *   - Identify text that's non-UTF8 and fall back
     *   - Find line, charInLine, and context string efficiently (line with match with <= 100 chars before and after)
     */

    /* Design:
     *   - Skip file extensions: "" | ".dll" | ".exe"
     *     - Avoids 33% of files (mostly git object files with no extension)
     *   
     *   - Load 64 KB first.
     *     - Same time cost as 1 KB.
     *     - 99.5% of my text files are still just one read.
     *   
     *   - Classify text by first 1 KB.
     *     - Can skip ~90% of bytes in folder classifying the first 1 KB.
     *     - About 1/3 of my UTF-8 has a BOM.
     *     - UTF-16/32 identified by BOM. (It looks like FileStream does only this)
     *     - Common, large non-text files are quickly skipped (zip, zlib, exe, dll, png, jpg)
     *     - 99% of my invalid UTF-8 are found by byte pairs (continuation after single byte, continuation missing after multi-byte)
     *       - 4,142 filtered checking continuation bytes fully in first 32 KB.
     *       - 4,141 filtered checking byte pairs only.
     *       - 4,133 filtered checking byte pairs in first 1 KB only.
     *
     *   - Search for matches in 512 KB blocks.
     *     - If no match in first block, nothing else to do.
     *     - If match found, figure out line and character. 
     */

    public interface IFileSearcher
    {
        long TotalBytesRead { get; }
        List<FilePosition> Search(Stream stream, string filePath);
    }

    public enum FileSearcher
    {
        Utf8,
        DotNet,
    }

    internal static class Settings
    {
        // Bytes to load in first file read.
        // 64 KB is as fast to read as 1 KB, but reads >99% of text files fully in one read.
        public const int FirstBlockSizeBytes = 64 * 1024;

        // Bytes to sniff to identify file type.
        // >99% of non-UTF8 files can be identified within the first 1 KB.
        // Sniffing avoids reading ~90% of the total file bytes in the.
        public const int SniffBytes = 1024;

        // Bytes to read per iteration after the first.
        // 512 KB resulted in the highest overall search bandwidth while minimizing per-thread RAM use.
        public const int BlockSizeBytes = 512 * 1024;
    }

    public static class FileSearcherFactory
    {
        public static IFileSearcher Build(FileSearcher searcher, string valueToFind, bool sniffFile = true)
        {
            switch (searcher)
            {
                case FileSearcher.Utf8:
                    return new Utf8Searcher(valueToFind, sniffFile);

                case FileSearcher.DotNet:
                    return new DotNetSearcher(valueToFind, sniffFile);

                default:
                    throw new NotImplementedException($"FileSearcher {searcher} unknown.");
            }
        }
    }

    public class DotNetSearcher : IFileSearcher
    {
        public string ValueToFind { get; }
        private bool SniffFile { get; }

        public long TotalBytesRead => _bytesRead;
        private long _bytesRead = 0;

        public DotNetSearcher(string valueToFind, bool sniffFile = true)
        {
            ValueToFind = valueToFind;
            SniffFile = sniffFile;
        }

        public List<FilePosition> Search(Stream stream, string filePath)
        {
            List<FilePosition> matches = null;
            char[] buffer = ArrayPool<char>.Shared.Rent(Settings.BlockSizeBytes);

            try
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    long totalFileBytes = stream.Length - stream.Position;

                    if (SniffFile && IsNotUnicode(stream))
                    {
                        return null;
                    }

                    // Read the first block
                    int charsRead = reader.Read(buffer.AsSpan().Slice(0, Settings.FirstBlockSizeBytes));
                    ReadOnlySpan<char> content = buffer.AsSpan().Slice(0, charsRead);
                    FilePosition current = FilePosition.Start(filePath);

                    while (true)
                    {
                        int startIndex = 0;

                        // Look for matches in the buffer
                        while (true)
                        {
                            int matchIndex = content.Slice(startIndex).IndexOf(ValueToFind, StringComparison.Ordinal);
                            if (matchIndex == -1) { break; }

                            current = FilePosition.Update(current, content.Slice(0, startIndex + matchIndex));
                            matches ??= new List<FilePosition>();
                            matches.Add(current);

                            content = content.Slice(startIndex + matchIndex);
                            startIndex = 1;
                        }

                        // Stop if all bytes have been read
                        if (stream.Position >= totalFileBytes) { break; }

                        // Keep the last ValueToFind-1 bytes, in case a match was just off the end of the read buffer
                        if (content.Length >= ValueToFind.Length)
                        {
                            int charsNotKept = (content.Length - (ValueToFind.Length - 1));
                            current = FilePosition.Update(current, content.Slice(0, charsNotKept));
                            content = content.Slice(charsNotKept);
                        }

                        // Copy unused bytes to buffer start
                        content.CopyTo(buffer);

                        // Read another block
                        charsRead = reader.Read(buffer.AsSpan(content.Length));
                        content = buffer.AsSpan().Slice(0, content.Length + charsRead);
                    }

                    Interlocked.Add(ref _bytesRead, totalFileBytes);
                    return matches;
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }

        private bool IsNotUnicode(Stream stream)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(Settings.SniffBytes);

            try
            {
                int bytesRead = stream.Read(buffer);
                Interlocked.Add(ref _bytesRead, bytesRead);
                stream.Seek(0, SeekOrigin.Begin);

                FileSniffResult result = FileSniffer.Sniff(buffer.AsSpan().Slice(0, bytesRead));
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
        private byte[] ValueToFind { get; }
        private bool SniffFile { get; }
        private IFileSearcher Fallback { get; }

        public long TotalBytesRead => _bytesRead + Fallback.TotalBytesRead;
        private long _bytesRead = 0;

        public Utf8Searcher(string valueToFind, bool sniffFile = true)
        {
            ValueToFind = Encoding.UTF8.GetBytes(valueToFind);
            SniffFile = sniffFile;
            Fallback = new DotNetSearcher(valueToFind, sniffFile: false);
        }

        public List<FilePosition> Search(Stream stream, string filePath)
        {
            List<FilePosition> matches = null;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(Settings.BlockSizeBytes);

            try
            {
                long totalBytesRead = 0;
                long totalFileBytes = stream.Length - stream.Position;

                // Read the first block
                int bytesRead = stream.Read(buffer.AsSpan().Slice(0, Settings.FirstBlockSizeBytes));
                totalBytesRead += bytesRead;
                Span<byte> content = buffer.AsSpan().Slice(0, bytesRead);
                FilePosition current = FilePosition.Start(filePath);

                // Sniff the file; fall back for UTF-16/32 and stop with no matches for non-UTF-8
                if (SniffFile)
                {
                    FileSniffResult result = FileSniffer.Sniff(content.Slice(0, Math.Min(content.Length, Settings.SniffBytes)));

                    if (result.Type != FileTypeDetected.UTF8)
                    {
                        Interlocked.Add(ref _bytesRead, totalBytesRead);

                        if (result.Type == FileTypeDetected.UnicodeOther)
                        {
                            Interlocked.Add(ref _bytesRead, totalBytesRead);
                            stream.Seek(0, SeekOrigin.Begin);
                            return Fallback.Search(stream, filePath);
                        }
                        else
                        {
                            return null;
                        }
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
                        int matchIndex = Vector.IndexOf(ValueToFind, content.Slice(startIndex));
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

                Interlocked.Add(ref _bytesRead, totalBytesRead);
                return matches;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
