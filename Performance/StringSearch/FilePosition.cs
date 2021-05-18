namespace StringSearch
{
    public struct FilePosition
    {
        public string FilePath;

        public long ByteOffset;
        public long CharOffset;
        
        public int LineNumber;
        public int CharInLine;

        public override string ToString()
        {
            if (LineNumber > 0)
            {
                return $"{FilePath} ({LineNumber:n0}, {CharInLine:n0})";
            }
            else if(ByteOffset == 0 && CharOffset > 0)
            {
                return $"{FilePath} @char: {CharOffset:n0}";
            }
            else
            {
                return $"{FilePath} @byte: {ByteOffset:n0}";
            }
        }
    }
}