// Copyright (c) Scott Louvau. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FastTextSearch
{
    // TODO:
    //  - Avoid/Reduce Match allocations?
    //  - Will Kirill want matches to a count limit or matches enumerator (for earlier first results?)

    public class DirectorySearcher
    {
        public FileSearcher Searcher { get; }
        public bool Multithreaded { get; }
        public bool FilterOnFileExtension { get; }
        public bool SniffFile { get; }

        public int FilesFound { get; private set; }
        public int FilesSearched => _filesSearched;
        private int _filesSearched;

        public long TotalBytesRead { get; private set; }
        public long TotalBytesToSearch => _totalBytesToSearch;
        private long _totalBytesToSearch;

        public DirectorySearcher(FileSearcher searcher = FileSearcher.Utf8, bool multithreaded = true, bool filterOnFileExtension = true, bool sniffFile = true)
        {
            Searcher = searcher;
            Multithreaded = multithreaded;
            FilterOnFileExtension = filterOnFileExtension;
            SniffFile = sniffFile;
        }

        public List<FilePosition> FindMatches(string valueToFind, string directoryToSearch, string searchPattern)
        {
            IFileSearcher fileSearcher = FileSearcherFactory.Build(this.Searcher, valueToFind, this.SniffFile);

            List<FilePosition> result = new List<FilePosition>();
            string[] filePaths = Directory.GetFiles(directoryToSearch, searchPattern, SearchOption.AllDirectories);

            if (this.Multithreaded)
            {
                Parallel.ForEach(filePaths, (path) => SearchFile(path, fileSearcher, result));
            }
            else
            {
                foreach (string path in filePaths)
                {
                    SearchFile(path, fileSearcher, result);
                }
            }

            FilesFound += filePaths.Length;
            TotalBytesRead += fileSearcher.TotalBytesRead;

            return result;
        }

        private void SearchFile(string path, IFileSearcher fileSearcher, List<FilePosition> result)
        {
            if (FilterOnFileExtension && IsExcluded(path)) { return; }
            Interlocked.Increment(ref _filesSearched);

            try
            {
                using (Stream stream = File.OpenRead(path))
                {
                    Interlocked.Add(ref _totalBytesToSearch, stream.Length);
                    List<FilePosition> fileMatches = fileSearcher.Search(stream, path);

                    if (fileMatches != null)
                    {
                        lock (result)
                        {
                            result.AddRange(fileMatches);
                        }
                    }
                }
            }
            catch (IOException)
            {
                // Quietly ignore files we fail to read.
            }
        }

        public bool IsExcluded(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            return (extension == "" || extension == ".dll" || extension == ".exe" || extension == ".pdb" || extension == ".v2");
        }

        public List<FilePosition> FindMatches(string valueToFind, string filePath)
        {
            return FileSearcherFactory.Build(this.Searcher, valueToFind, this.SniffFile).Search(File.OpenRead(filePath), filePath);
        }
    }
}
