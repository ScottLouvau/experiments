﻿using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace FastTextSearch
{
    // TODO:
    //  - Files > 2 GB? (Want to search ranges, not full file)
    //  - Avoid/Reduce Match allocations?
    //  - Will Kirill want matches to a count limit or matches enumerator (for earlier first results?)

    public class DirectorySearcher
    {
        public FileSearcher Searcher { get; }
        public bool Multithreaded { get; }
        public bool FilterOnFileExtension { get; }
        public bool FilterOnFirstBytes { get; }

        public DirectorySearcher(FileSearcher searcher = FileSearcher.Utf8, bool multithreaded = true, bool filterOnFileExtension = true, bool filterOnFirstBytes = true)
        {
            Searcher = searcher;
            Multithreaded = multithreaded;
            FilterOnFileExtension = filterOnFileExtension;
            FilterOnFirstBytes = filterOnFirstBytes;
        }

        public List<FilePosition> FindMatches(string valueToFind, string directoryToSearch, string searchPattern)
        {
            IFileSearcher fileSearcher = FileSearcherFactory.Build(valueToFind, this.FilterOnFirstBytes);

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

            return result;
        }

        private void SearchFile(string path, IFileSearcher fileSearcher, List<FilePosition> result)
        {
            if (FilterOnFileExtension && IsExcluded(path)) { return; }

            try
            {
                List<FilePosition> fileMatches = fileSearcher.Search(path);

                if (fileMatches != null)
                {
                    lock (result)
                    {
                        result.AddRange(fileMatches);
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
            return FileSearcherFactory.Build(valueToFind, this.FilterOnFirstBytes).Search(filePath);
        }
    }
}