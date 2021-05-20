using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace StringSearch.Test
{
    public class FileSearcherTests : TestBase
    {
        private Func<string, IFileSearcher> Utf8 = (value) => new Utf8Searcher(value);

        [Fact]
        public void Utf8Searcher_Basics()
        {
            FileSearcher_Basics(Utf8);
        }

        [Fact]
        public void SearcherAdhocDebugging()
        {
            string matches = AllMatches("Convert", @"C:\Code\bion\csharp\ScaleDemo\Region2.cs", Utf8);

            // No Asserts. Replace with a sample case and set breakpoint to debug issues encountered.
        }

        private void FileSearcher_Basics(Func<string, IFileSearcher> buildSearcher)
        {
            string sampleFilePath = Path.Combine(ContentFolderPath, "HelloWorld.cs");

            // Quote in HelloWorld: Multiple matches, matches on same line.
            // Checks that FilePosition updating doesn't have off-by-ones in CharInLine counting.
            string matches = AllMatches("\"", sampleFilePath, buildSearcher);
            Assert.Equal("(9, 31); (9, 46); (10, 31); (10, 33); (11, 31); (11, 33); (12, 31); (12, 33)", matches);
        }

        private string AllMatches(string valueToFind, string filePath, Func<string, IFileSearcher> buildSearcher)
        {
            List<FilePosition> matches = buildSearcher(valueToFind).Search(filePath);
            return string.Join("; ", matches.Select(m => m.LineAndChar));
        }
    }
}
