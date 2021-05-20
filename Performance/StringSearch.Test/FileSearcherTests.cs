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
        private Func<string, IFileSearcher> DotNet = (value) => new DotNetSearcher(value);

        [Fact]
        public void Utf8Searcher_Basics()
        {
            FileSearcher_Basics(Utf8);
        }

        [Fact]
        public void DotNetSearcher_Basics()
        {
            FileSearcher_Basics(DotNet);
        }

        [Fact]
        public void SearcherAdhocDebugging()
        {
            string matches = AllMatches("Convert", @"C:\Code\bion\csharp\ScaleDemo\Region2.cs", Utf8);

            // No Asserts. Replace with a sample case and set breakpoint to debug issues encountered.
        }

        private void FileSearcher_Basics(Func<string, IFileSearcher> searcher)
        {
            string helloWorld = Path.Combine(ContentFolderPath, "HelloWorld.cs");

            // Quote in HelloWorld.cs: Multiple matches, matches on same line.
            // Checks that FilePosition updating doesn't have off-by-ones in CharInLine counting.
            string matches = AllMatches("\"", helloWorld, searcher);
            Assert.Equal("(9, 31); (9, 46); (10, 31); (10, 33); (11, 31); (11, 33); (12, 31); (12, 33)", matches);

            // "HelloWorld" in HelloWorld.cs
            matches = AllMatches("HelloWorld", helloWorld, searcher);
            Assert.Equal("(3, 11); (5, 11)", matches);

            // "using" (right at start of file)
            Assert.Equal("(1, 1)", AllMatches("using", helloWorld, searcher));

            // "}" (right at end of file)
            Assert.Equal("(13, 9); (14, 5); (15, 1)", AllMatches("}", helloWorld, searcher));

            // "Consoled" (near match)
            Assert.Equal("", AllMatches("Consoled", helloWorld, searcher));

            // "    Console.WriteLine(\"Hello World!" (longer than 32b value)
            Assert.Equal("(9, 1)", AllMatches("            Console.WriteLine(\"Hello World!", helloWorld, searcher));

            // UTF-16 (Fallback code; must not count BOM in first line match)
            Assert.Equal("(1, 17)", AllMatches("sample", Path.Combine(ContentFolderPath, "Simple.UTF16.LE.txt"), searcher));

            // Non-Text (must sniff file and return early)
            Assert.Equal("", AllMatches("DOS", Path.Combine(ContentFolderPath, "HelloWorld.dll"), searcher));

            // Large File (must shift bytes in buffer and keep tracking position correctly)
            // (2890, 146) matches VS Code result, and is 515,575 bytes into the file.
            Assert.Equal("(2890, 146)", AllMatches("1329455026", Path.Combine(ContentFolderPath, "Sample.csv"), searcher));
        }

        private string AllMatches(string valueToFind, string filePath, Func<string, IFileSearcher> buildSearcher)
        {
            List<FilePosition> matches = buildSearcher(valueToFind).Search(filePath);
            return (matches == null ? "" : string.Join("; ", matches.Select(m => m.LineAndChar)));
        }
    }
}
