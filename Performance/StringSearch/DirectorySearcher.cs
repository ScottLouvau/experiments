using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace StringSearch
{
    public enum FileSearcherMode
    {
        Utf8,
        DotNet,
        Reference,
    }

    // TODO:
    //  - Files > 2 GB? (Want to search ranges, not full file)
    //  - Avoid/Reduce Match allocations?
    //  - Will Kirill want matches to a count limit or matches enumerator (for earlier first results?)

    public class DirectorySearcher
    {
        public FileSearcherMode Mode { get; }
        public bool Multithreaded { get; }
        public bool FilterOnFileExtension { get; }
        public bool FilterOnFirstBytes { get; }

        public DirectorySearcher(FileSearcherMode mode = FileSearcherMode.Utf8, bool multithreaded = true, bool filterOnFileExtension = true, bool filterOnFirstBytes = true)
        {
            Mode = mode;
            Multithreaded = multithreaded;
            FilterOnFileExtension = filterOnFileExtension;
            FilterOnFirstBytes = filterOnFirstBytes;
        }

        private IFileSearcher BuildSearcher(string valueToFind)
        {
            IFileSearcher fileSearcher;

            switch (this.Mode)
            {
                case FileSearcherMode.Utf8:
                    fileSearcher = new Utf8Searcher(valueToFind, FilterOnFirstBytes);
                    break;

                case FileSearcherMode.DotNet:
                    fileSearcher = new DotNetSearcher(valueToFind);
                    break;

                case FileSearcherMode.Reference:
                    fileSearcher = new ReferenceSearcher(valueToFind);
                    break;

                default:
                    throw new NotImplementedException($"FileSearcherMode {Mode} not implemented.");
            }

            return fileSearcher;
        }

        public List<FilePosition> FindMatches(string valueToFind, string directoryToSearch, string searchPattern)
        {
            IFileSearcher fileSearcher = BuildSearcher(valueToFind);

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
            return (extension == "" || extension == ".dll" || extension == ".exe" || extension == ".pdb");
        }

        public List<FilePosition> FindMatches(string valueToFind, string filePath)
        {
            return BuildSearcher(valueToFind).Search(filePath);
        }
    }
}
