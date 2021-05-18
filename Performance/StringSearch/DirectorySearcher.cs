using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StringSearch
{
    public class Match
    {
        public string FilePath { get; set; }
        public long MatchByteIndex { get; set; }
    }

    public enum FileSearcherMode
    {
        Utf8,
        DotNetDefault
    }

    // TODO:
    //  - Files > 2 GB? (Want to search ranges, not full file)
    //  - Need newline mapping
    //  - Need to get vectorized stuff going
    //  - Not respecting FilterOnFirstBytes with Mode == DotNetDefault
    //  - Avoid/Reduce Match allocations?
    //  - Will Kirill want matches to a count limit or matches enumerator (for earlier first results?)

    public class DirectorySearcher
    {
        public FileSearcherMode Mode { get; set; } = FileSearcherMode.Utf8;
        public bool Multithreaded { get; set; } = true;
        public bool FilterOnFileExtension { get; set; } = true;
        public bool FilterOnFirstBytes { get; set; } = true;
        public bool CaseInsensitiveAscii { get; set; } = false;

        public int PrefixBytesToLoad { get; } = 64 * 1024;
        public int PrefixBytesToCheck { get; } = 1024;

        public string DirectoryToSearch { get; }
        public string SearchPattern { get; }
        public string ValueToFind { get; }
        private byte[] BytesToFind { get; }

        private ArrayPool<byte> Pool { get; } = ArrayPool<byte>.Shared;

        public long BytesSearched => _bytesSearched;
        private long _bytesSearched;

        public DirectorySearcher(string valueToFind, string directoryToSearch, string searchPattern)
        {
            DirectoryToSearch = directoryToSearch;
            SearchPattern = searchPattern;
            ValueToFind = valueToFind;
            BytesToFind = Encoding.UTF8.GetBytes(ValueToFind);
        }

        public List<Match> FindMatches()
        {
            List<Match> result = new List<Match>();
            string[] filePaths = Directory.GetFiles(DirectoryToSearch, SearchPattern, SearchOption.AllDirectories);

            if (this.Multithreaded)
            {
                Parallel.ForEach(filePaths, (path) =>
                {
                    List<Match> fileMatches = SearchFile(path);

                    if (fileMatches != null)
                    {
                        lock (result)
                        {
                            result.AddRange(fileMatches);
                        }
                    }
                });
            }
            else
            {
                foreach (string path in filePaths)
                {
                    List<Match> fileMatches = SearchFile(path);

                    if (fileMatches != null)
                    {
                        result.AddRange(fileMatches);
                    }
                }
            }

            return result;
        }

        private List<Match> SearchFile(string filePath)
        {
            if (this.FilterOnFileExtension)
            {
                string extension = Path.GetExtension(filePath).ToLowerInvariant();

                if (extension == "" || extension == ".dll" || extension == ".exe" || extension == ".zip")
                {
                    return null;
                }
            }

            if (this.FilterOnFirstBytes)
            {
                FileScanResult result = default;
                byte[] buffer = Pool.Rent(PrefixBytesToLoad);

                try
                {
                    Span<byte> view = null;

                    using (FileStream stream = File.OpenRead(filePath))
                    {
                        int prefixLength = Math.Min((int)stream.Length, PrefixBytesToLoad);
                        view = buffer;
                        view = view.Slice(0, prefixLength);

                        int prefixLengthRead = stream.Read(view);
                        view = view.Slice(0, prefixLengthRead);

                        result = FileTypeScanner.Identify(view.Slice(0, Math.Min(prefixLengthRead, PrefixBytesToCheck)));

                        if (result.Type == FileTypeDetected.UTF8)
                        {
                            if (stream.Length > view.Length)
                            {
                                byte[] fullFileBuffer = Pool.Rent((int)stream.Length);

                                view.CopyTo(fullFileBuffer);
                                Pool.Return(buffer);
                                buffer = fullFileBuffer;
                                view = fullFileBuffer;

                                int remainderRead = stream.Read(view.Slice(prefixLengthRead));
                                view = buffer.AsSpan().Slice(0, prefixLengthRead + remainderRead);
                            }

                            view = view.Slice(result.BomByteCount);

                            return SearchFileUtf8(filePath, view);
                        }
                    }
                }
                finally
                {
                    Pool.Return(buffer);
                }

                if (result.Type == FileTypeDetected.UnicodeOther)
                {
                    return SearchFileDotNet(filePath, File.ReadAllText(filePath));
                }
                else
                {
                    return null;
                }
            }

            if (this.Mode == FileSearcherMode.DotNetDefault)
            {
                return SearchFileDotNet(filePath, File.ReadAllText(filePath));
            }
            else
            {
                return SearchFileUtf8(filePath, File.ReadAllBytes(filePath));
            }
        }

        private List<Match> SearchFileDotNet(string filePath, string contents)
        {
            List<Match> matches = null;

            int startIndex = 0;

            while (true)
            {
                int matchIndex = contents.IndexOf(this.ValueToFind, startIndex, StringComparison.Ordinal);
                if (matchIndex == -1) { break; }

                matches ??= new List<Match>();
                matches.Add(new Match() { FilePath = filePath, MatchByteIndex = matchIndex });

                startIndex = matchIndex + 1;
            }

            return matches;
        }

        private List<Match> SearchFileUtf8(string filePath, Span<byte> contents)
        {
            Interlocked.Add(ref _bytesSearched, contents.Length);
            List<Match> matches = null;

            int startIndex = 0;

            while (true)
            {
                int matchIndex = contents.IndexOf(BytesToFind);
                if (matchIndex == -1) { break; }

                matches ??= new List<Match>();
                matches.Add(new Match() { FilePath = filePath, MatchByteIndex = startIndex + matchIndex });

                startIndex = matchIndex + 1;
                contents = contents.Slice(matchIndex + 1);
            }

            return matches;
        }
    }
}
