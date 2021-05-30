using FastTextSearch;
using System;
using System.Collections.Generic;

namespace FFS.Bench
{
    public class SearchResult
    {
        public string FilePath { get; set; }
        public int ByteIndex { get; set; }

        public SearchResult(string filePath, int byteIndex)
        {
            FilePath = filePath;
            ByteIndex = byteIndex;
        }
    }

    public class Searchers
    {
        public static void String_IndexOf(string searchFor, string text, string filePath, List<SearchResult> results)
        {
            int startIndex = 0;
            while (true)
            {
                int nextIndex = text.IndexOf(searchFor, startIndex);
                if (nextIndex == -1) { break; }

                results.Add(new SearchResult(filePath, nextIndex));
                startIndex = nextIndex + 1;
            }
        }

        public static void String_IndexOf_Ordinal(string searchFor, string text, string filePath, List<SearchResult> results)
        {
            int startIndex = 0;
            while (true)
            {
                int nextIndex = text.IndexOf(searchFor, startIndex, StringComparison.Ordinal);
                if (nextIndex == -1) { break; }

                results.Add(new SearchResult(filePath, nextIndex));
                startIndex = nextIndex + 1;
            }
        }

        public static void Span_IndexOf(ReadOnlySpan<byte> searchFor, ReadOnlySpan<byte> text, string filePath, List<SearchResult> results)
        {
            int startIndex = 0;
            while (true)
            {
                int nextIndex = text.Slice(startIndex).IndexOf(searchFor);
                if (nextIndex == -1) { break; }

                results.Add(new SearchResult(filePath, startIndex + nextIndex));
                startIndex = startIndex + nextIndex + 1;
            }
        }

        public static void FastTextSearch_IndexOf(ReadOnlySpan<byte> searchFor, ReadOnlySpan<byte> text, string filePath, List<SearchResult> results)
        {
            int startIndex = 0;
            while (true)
            {
                int nextIndex = Vector.IndexOf(searchFor, text.Slice(startIndex));
                if (nextIndex == -1) { break; }

                results.Add(new SearchResult(filePath, startIndex + nextIndex));
                startIndex = startIndex + nextIndex + 1;
            }
        }
    }
}
