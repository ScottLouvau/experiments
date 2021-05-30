using FastTextSearch;
using RoughBench.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FFS.Bench
{
    public class InMemorySearch
    {
        public const string TestSearchFor = "Convert";
        public const string TestSearchWithin = @"C:\CodeSnap\bion";
        public const string ConcatenatedPath = "bion.concat";

        private string ConcatenatedString;
        private byte[] ConcatenatedBytes;
        public long BytesSearched => ConcatenatedBytes.Length;

        public InMemorySearch()
        {
            if (!File.Exists(ConcatenatedPath)) { TextFileConcatenator.ConcatenateTextUnder(TestSearchWithin, ConcatenatedPath); }
            ConcatenatedBytes = File.ReadAllBytes(ConcatenatedPath);
            ConcatenatedString = Encoding.UTF8.GetString(ConcatenatedBytes);
        }

        [Benchmark]
        public void String_IndexOf()
        {
            List<SearchResult> results = new List<SearchResult>();
            int startIndex = 0;
            while (true)
            {
                int nextIndex = ConcatenatedString.IndexOf(TestSearchFor, startIndex);
                if (nextIndex == -1) { break; }

                results.Add(new SearchResult("", nextIndex));
                startIndex = nextIndex + 1;
            }
        }

        [Benchmark]
        public void String_IndexOfOrdinal()
        {
            List<SearchResult> results = new List<SearchResult>();
            int startIndex = 0;
            while (true)
            {
                int nextIndex = ConcatenatedString.IndexOf(TestSearchFor, startIndex, StringComparison.Ordinal);
                if (nextIndex == -1) { break; }

                results.Add(new SearchResult("", nextIndex));
                startIndex = nextIndex + 1;
            }
        }

        [Benchmark]
        public void Span_IndexOf()
        {
            List<SearchResult> results = new List<SearchResult>();
            Span<byte> bytesToFind = Encoding.UTF8.GetBytes(TestSearchFor);
            Span<byte> text = ConcatenatedBytes;

            int startIndex = 0;
            while (true)
            {
                int nextIndex = text.Slice(startIndex).IndexOf(bytesToFind);
                if (nextIndex == -1) { break; }

                results.Add(new SearchResult("", startIndex + nextIndex));
                startIndex = startIndex + nextIndex + 1;
            }
        }

        [Benchmark]
        public void FastTextSearch_IndexOf()
        {
            List<SearchResult> results = new List<SearchResult>();
            Span<byte> bytesToFind = Encoding.UTF8.GetBytes(TestSearchFor);
            Span<byte> text = ConcatenatedBytes;

            int startIndex = 0;
            while (true)
            {
                int nextIndex = Vector.IndexOf(bytesToFind, text.Slice(startIndex));
                if (nextIndex == -1) { break; }

                results.Add(new SearchResult("", startIndex + nextIndex));
                startIndex = startIndex + nextIndex + 1;
            }
        }
    }
}
