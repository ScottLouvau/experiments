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

        //private List<FilePosition> SearchFile(string filePath)
        //{
        //    if (this.FilterOnFileExtension)
        //    {
        //        string extension = Path.GetExtension(filePath).ToLowerInvariant();

        //        if (extension == "" || extension == ".dll" || extension == ".exe" || extension == ".zip")
        //        {
        //            return null;
        //        }
        //    }

        //    if (this.FilterOnFirstBytes)
        //    {
        //        FileScanResult result = default;
        //        byte[] buffer = Pool.Rent(PrefixBytesToLoad);

        //        try
        //        {
        //            Span<byte> view = null;

        //            using (FileStream stream = File.OpenRead(filePath))
        //            {
        //                int prefixLength = Math.Min((int)stream.Length, PrefixBytesToLoad);
        //                view = buffer;
        //                view = view.Slice(0, prefixLength);

        //                int prefixLengthRead = stream.Read(view);
        //                view = view.Slice(0, prefixLengthRead);

        //                result = FileTypeScanner.Identify(view.Slice(0, Math.Min(prefixLengthRead, PrefixBytesToCheck)));

        //                if (result.Type == FileTypeDetected.UTF8)
        //                {
        //                    if (stream.Length > view.Length)
        //                    {
        //                        byte[] fullFileBuffer = Pool.Rent((int)stream.Length);

        //                        view.CopyTo(fullFileBuffer);
        //                        Pool.Return(buffer);
        //                        buffer = fullFileBuffer;
        //                        view = fullFileBuffer;

        //                        int remainderRead = stream.Read(view.Slice(prefixLengthRead));
        //                        view = buffer.AsSpan().Slice(0, prefixLengthRead + remainderRead);
        //                    }

        //                    view = view.Slice(result.BomByteCount);

        //                    return SearchFileUtf8(filePath, view);
        //                }
        //            }
        //        }
        //        finally
        //        {
        //            Pool.Return(buffer);
        //        }

        //        if (result.Type == FileTypeDetected.UnicodeOther)
        //        {
        //            return SearchFileDotNet(filePath, File.ReadAllText(filePath));
        //        }
        //        else
        //        {
        //            return null;
        //        }
        //    }

        //    if (this.Mode == FileSearcherMode.DotNetDefault)
        //    {
        //        return SearchFileDotNet(filePath, File.ReadAllText(filePath));
        //    }
        //    else
        //    {
        //        return SearchFileUtf8(filePath, File.ReadAllBytes(filePath));
        //    }
        //}
    }
}
