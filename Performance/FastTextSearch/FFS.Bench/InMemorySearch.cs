using RoughBench.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace FFS.Bench
{
    public class InMemorySearch
    {
        private string SearchForString;
        private byte[] SearchForBytes;

        private Func<string> GetString;
        private Func<byte[]> GetBytes;

        public long BytesSearched { get; private set; }

        public InMemorySearch(string searchFor, Func<string> getString, Func<byte[]> getBytes)
        {
            SearchForString = searchFor;
            SearchForBytes = Encoding.UTF8.GetBytes(searchFor);

            GetString = getString;
            GetBytes = getBytes;
        }

        [Benchmark]
        public void String_IndexOf()
        {
            string text = GetString();
            Searchers.String_IndexOf(SearchForString, text, null, new List<SearchResult>());
            BytesSearched = text.Length;
        }

        [Benchmark]
        public void String_IndexOfOrdinal()
        {
            string text = GetString();
            Searchers.String_IndexOf_Ordinal(SearchForString, text, null, new List<SearchResult>());
            BytesSearched = text.Length;
        }

        [Benchmark]
        public void Span_IndexOf()
        {
            byte[] bytes = GetBytes();
            Searchers.Span_IndexOf(SearchForBytes, bytes, null, new List<SearchResult>());
            BytesSearched = bytes.Length;
        }

        [Benchmark]
        public void FastTextSearch_IndexOf()
        {
            byte[] bytes = GetBytes();
            Searchers.FastTextSearch_IndexOf(SearchForBytes, bytes, null, new List<SearchResult>());
            BytesSearched = bytes.Length;
        }
    }
}
