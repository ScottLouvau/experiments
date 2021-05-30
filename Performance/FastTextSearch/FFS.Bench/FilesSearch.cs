using RoughBench.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FFS.Bench
{
    public class FilesSearch
    {
        private string SearchForString;
        private byte[] SearchForBytes;

        private string[] FilePaths;
        public long BytesSearched { get; private set; }

        public FilesSearch(string searchFor, string[] filePaths)
        {
            SearchForString = searchFor;
            SearchForBytes = Encoding.UTF8.GetBytes(searchFor);

            FilePaths = filePaths;
        }

        [Benchmark]
        public void ReadAllText_StringIndexOf()
        {
            long bytesSearched = 0;
            List<SearchResult> results = new List<SearchResult>();

            foreach (string filePath in FilePaths)
            {
                string text = File.ReadAllText(filePath);
                Searchers.String_IndexOf(SearchForString, text, filePath, results);
                bytesSearched += text.Length;
            }

            BytesSearched = bytesSearched;
        }

        [Benchmark]
        public void ReadAllText_StringIndexOfOrdinal()
        {
            long bytesSearched = 0;
            List<SearchResult> results = new List<SearchResult>();

            foreach (string filePath in FilePaths)
            {
                string text = File.ReadAllText(filePath);
                Searchers.String_IndexOf_Ordinal(SearchForString, text, filePath, results);
                bytesSearched += text.Length;
            }

            BytesSearched = bytesSearched;
        }

        [Benchmark]
        public void ReadAllBytes_SpanIndexOf()
        {
            long bytesSearched = 0;
            List<SearchResult> results = new List<SearchResult>();

            foreach (string filePath in FilePaths)
            {
                Span<byte> text = File.ReadAllBytes(filePath);
                Searchers.Span_IndexOf(SearchForBytes, text, filePath, results);
                bytesSearched += text.Length;
            }

            BytesSearched = bytesSearched;
        }

        [Benchmark]
        public void ReadAllBytes_FastTextSearchIndexOf()
        {
            long bytesSearched = 0;
            List<SearchResult> results = new List<SearchResult>();

            foreach (string filePath in FilePaths)
            {
                Span<byte> text = File.ReadAllBytes(filePath);
                Searchers.FastTextSearch_IndexOf(SearchForBytes, text, filePath, results);
                bytesSearched += text.Length;
            }

            BytesSearched = bytesSearched;
        }
    }
}
