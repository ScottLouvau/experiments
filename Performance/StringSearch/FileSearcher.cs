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
    //  - Files > 2 GB?

    public class FileSearcher
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

        private ArrayPool<byte> Pool { get; } = ArrayPool<byte>.Create();

        public long BytesSearched => _bytesSearched;
        private long _bytesSearched;

        public FileSearcher(string valueToFind, string directoryToSearch, string searchPattern)
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
                foreach(string path in filePaths)
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
                Span<byte> contents = null;
                FileScanResult result = default;

                using (FileStream stream = File.OpenRead(filePath))
                {
                    int prefixLength = Math.Min((int)stream.Length, PrefixBytesToLoad);
                    //byte[] buffer = new byte[(int)stream.Length]; 
                    byte[] buffer = Pool.Rent((int)stream.Length);
                    contents = buffer;

                    int prefixLengthRead = stream.Read(contents.Slice(0, prefixLength));
                    contents = contents.Slice(0, prefixLengthRead);

                    result = FileTypeScanner.Identify(contents.Slice(0, Math.Min(prefixLengthRead, PrefixBytesToCheck)));

                    if (result.Type == FileTypeDetected.UTF8)
                    {
                        int remainderRead = stream.Read(buffer.AsSpan().Slice(prefixLengthRead));
                        contents = buffer.AsSpan().Slice(0, prefixLengthRead + remainderRead);
                        contents = contents.Slice(result.BomByteCount);

                        List<Match> matches = SearchFileUtf8(filePath, contents);
                        Pool.Return(buffer);
                        return matches;
                    }
                    else
                    {
                        Pool.Return(buffer);
                    }
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
