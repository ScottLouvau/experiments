using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace StringSearch
{
    public interface IFileSearcher
    {
        List<FilePosition> Search(string filePath);
        List<FilePosition> Search(Stream streamToSearch, string filePath);
    }

    public class FileExtensionFilter : IFileSearcher
    {
        private IFileSearcher _inner;

        public FileExtensionFilter(IFileSearcher inner)
        {
            _inner = inner;
        }

        public bool IsExcluded(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            return (extension == "" || extension == ".dll" || extension == ".exe" || extension == ".zip");
        }

        public List<FilePosition> Search(string filePath)
        {
            return (IsExcluded(filePath) ? null : _inner.Search(filePath));
        }

        public List<FilePosition> Search(Stream streamToSearch, string filePath)
        {
            return (IsExcluded(filePath) ? null : _inner.Search(streamToSearch, filePath));
        }
    }

    public class FilePrefixFilter : IFileSearcher
    {
        private const int PrefixBytesToLoad = 64 * 1024;
        private const int PrefixBytesToScan = 1024;

        private IFileSearcher _inner;
        private IFileSearcher _fallback;

        public FilePrefixFilter(IFileSearcher inner, IFileSearcher fallback)
        {
            _inner = inner;
            _fallback = fallback;
        }

        public List<FilePosition> Search(string filePath)
        {
            using (Stream stream = File.OpenRead(filePath))
            {
                return Search(stream, filePath);
            }
        }

        public List<FilePosition> Search(Stream streamToSearch, string filePath)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(PrefixBytesToLoad);

            try
            {
                int prefixLengthRead = streamToSearch.Read(buffer);
                Span<byte> content = buffer.AsSpan().Slice(0, prefixLengthRead);

                Span<byte> prefixToScan = content.Slice(0, Math.Min(prefixLengthRead, PrefixBytesToScan));
                FileScanResult result = FileTypeScanner.Identify(prefixToScan);

                if (result.Type == FileTypeDetected.UTF8)
                {
                    if (streamToSearch.Length > content.Length)
                    {
                        byte[] fullBuffer = ArrayPool<byte>.Shared.Rent((int)content.Length);
                        content.CopyTo(fullBuffer);
                        int remainderRead = streamToSearch.Read(fullBuffer.AsSpan().Slice(prefixLengthRead));

                        ArrayPool<byte>.Shared.Return(buffer);
                        buffer = fullBuffer;
                    }

                    return (_inner.Search(new MemoryStream(buffer, 0, content.Length, false), filePath));
                }
                else if (result.Type == FileTypeDetected.UnicodeOther)
                {
                    return _fallback.Search(filePath);
                }
                else
                {
                    return null;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    public class DotNetFileSearcher : IFileSearcher
    {
        public string ValueToFind { get; }

        public DotNetFileSearcher(string valueToFind)
        {
            ValueToFind = valueToFind;
        }

        public List<FilePosition> Search(string filePath)
        {
            using (Stream stream = File.OpenRead(filePath))
            {
                return Search(stream, filePath);
            }
        }

        public List<FilePosition> Search(Stream streamToSearch, string filePath)
        {
            List<FilePosition> matches = null;

            string contents = null;
            using (StreamReader reader = new StreamReader(streamToSearch))
            {
                contents = reader.ReadToEnd();
            }

            int startIndex = 0;

            while (true)
            {
                int matchIndex = contents.IndexOf(this.ValueToFind, startIndex, StringComparison.Ordinal);
                if (matchIndex == -1) { break; }

                matches ??= new List<FilePosition>();
                matches.Add(new FilePosition() { FilePath = filePath, CharOffset = startIndex + matchIndex });

                startIndex = matchIndex + 1;
            }

            return matches;
        }
    }

    public class Utf8FileSearcher : IFileSearcher
    {
        private byte[] ValueToFind { get; }

        public Utf8FileSearcher(string valueToFind)
        {
            ValueToFind = Encoding.UTF8.GetBytes(valueToFind);
        }

        public List<FilePosition> Search(string filePath)
        {
            using (Stream stream = File.OpenRead(filePath))
            {
                return Search(stream, filePath);
            }
        }

        public List<FilePosition> Search(Stream streamToSearch, string filePath)
        {
            byte[] buffer = new byte[(int)streamToSearch.Length];
            int lengthRead = streamToSearch.Read(buffer);
            Span<byte> contents = buffer.AsSpan().Slice(0, lengthRead);

            List<FilePosition> matches = null;

            int startIndex = 0;

            while (true)
            {
                int matchIndex = contents.IndexOf(ValueToFind);
                if (matchIndex == -1) { break; }

                matches ??= new List<FilePosition>();
                matches.Add(new FilePosition() { FilePath = filePath, ByteOffset = startIndex + matchIndex });

                startIndex += matchIndex + 1;
                contents = contents.Slice(matchIndex + 1);
            }

            return matches;
        }
    }
}
