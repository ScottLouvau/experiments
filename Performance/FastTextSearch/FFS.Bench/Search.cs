using FastTextSearch;
using RoughBench.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FFS.Bench
{ 
    public class Search
    {
        public const string TestSearchFor = "Convert";
        public const string TestSearchWithin = @"C:\CodeSnap\bion";

        public static long BytesSearched()
        {
            return (new DirectoryInfo(TestSearchWithin).GetFiles("*.*", SearchOption.AllDirectories)).Sum((fi) => fi.Length);
        }

        [Benchmark]
        public void ReadText_IndexOf()
        {
            List<SearchResult> results = new List<SearchResult>();

            foreach (string filePath in Directory.GetFiles(TestSearchWithin, "*.*", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(filePath);

                int startIndex = 0;
                while (true)
                {
                    int nextIndex = text.IndexOf(TestSearchFor, startIndex);
                    if (nextIndex == -1) { break; }

                    results.Add(new SearchResult(filePath, nextIndex));
                    startIndex = nextIndex + 1;
                }
            }
        }

        [Benchmark]
        public void ReadText_IndexOfOrdinal()
        {
            List<SearchResult> results = new List<SearchResult>();

            foreach (string filePath in Directory.GetFiles(TestSearchWithin, "*.*", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(filePath);

                int startIndex = 0;
                while (true)
                {
                    int nextIndex = text.IndexOf(TestSearchFor, startIndex, StringComparison.Ordinal);
                    if (nextIndex == -1) { break; }

                    results.Add(new SearchResult(filePath, nextIndex));
                    startIndex = nextIndex + 1;
                }
            }
        }

        [Benchmark]
        public void ReadBytes_SpanIndexOf()
        {
            List<SearchResult> results = new List<SearchResult>();
            byte[] bytesToFind = Encoding.UTF8.GetBytes(TestSearchFor);

            foreach (string filePath in Directory.GetFiles(TestSearchWithin, "*.*", SearchOption.AllDirectories))
            {
                Span<byte> text = File.ReadAllBytes(filePath);

                int startIndex = 0;
                while (true)
                {
                    int nextIndex = text.Slice(startIndex).IndexOf(bytesToFind);
                    if (nextIndex == -1) { break; }

                    results.Add(new SearchResult(filePath, startIndex + nextIndex));
                    startIndex = startIndex + nextIndex + 1;
                }
            }
        }

        public long FilteredBytesSearched { get; private set; }

        [Benchmark]
        public void FastTextSearch()
        {
            DirectorySearcher ds = new DirectorySearcher(FileSearcher.Utf8, multithreaded: false, filterOnFileExtension: true, sniffFile: true);
            ds.FindMatches(TestSearchFor, TestSearchWithin, "*.*");
            FilteredBytesSearched = ds.TotalBytesRead;
        }
    }
}
