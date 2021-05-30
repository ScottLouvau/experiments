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
}
