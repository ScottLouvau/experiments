using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace StringSearch
{
    public enum FileSearcherMode
    {
        Utf8,
        Utf8Whole,
        DotNet,
        DotNetUnpositioned,
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
                case FileSearcherMode.DotNet:
                    fileSearcher = new DotNetFileSearcher(valueToFind);
                    break;

                case FileSearcherMode.DotNetUnpositioned:
                    fileSearcher = new DotNetUnpositionedFileSearcher(valueToFind);
                    break;

                case FileSearcherMode.Utf8:
                    fileSearcher = new Utf8FileSearcher(valueToFind);
                    break;

                case FileSearcherMode.Utf8Whole:
                    fileSearcher = new Utf8WholeSearcher(valueToFind);
                    break;

                default:
                    throw new NotImplementedException($"FileSearcherMode {Mode} not implemented.");
            }

            if (this.FilterOnFirstBytes)
            {
                fileSearcher = new FilePrefixFilter(fileSearcher, new DotNetFileSearcher(valueToFind));
            }

            if (this.FilterOnFileExtension)
            {
                fileSearcher = new FileExtensionFilter(fileSearcher);
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
                Parallel.ForEach(filePaths, (path) =>
                {
                    List<FilePosition> fileMatches = fileSearcher.Search(path);

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
                    List<FilePosition> fileMatches = fileSearcher.Search(path);

                    if (fileMatches != null)
                    {
                        result.AddRange(fileMatches);
                    }
                }
            }

            return result;
        }
         
        public List<FilePosition> FindMatches(string valueToFind, string filePath)
        {
            return BuildSearcher(valueToFind).Search(filePath);
        }
    }
}
