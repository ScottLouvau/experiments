
public static class Program 
{
    public static void Usage()
    {
        Console.WriteLine("Usage: letter-boxed ABC DEF GHI JKL");
    }

    public static void Main(string[] args)
    {
        if (args.Length != 4)
        {
            Usage();
            return;
        }

        byte[] letter_sides = ParsePuzzleLetters(args);
        if (letter_sides == null)
        {
            Usage();
            return;
        }

        // Load Dictionary of valid words
        //string dictionaryPath = @"Words.txt";
        string dictionaryPath = @"words_easy.txt";
        List<string> words = File.ReadAllLines(dictionaryPath).Select(line => line.ToLowerInvariant()).ToList();

        // Filter to words that are playable in the current puzzle
        List<string> playableWords = words.Where(word => CanBePlayed(word, letter_sides)).ToList();

        // Sort playable words by the rarity of puzzle letters included, chainability, and length
        AlphabetStats stats = new AlphabetStats();
        foreach (string word in playableWords)
        {
            stats.CountWord(word);
        }

        //playableWords.Sort((a, b) => stats.Score(b, letter_sides).CompareTo(stats.Score(a, letter_sides)));

        foreach (string word in playableWords)
        {
            Console.WriteLine(word);
        }

        Console.WriteLine($"{playableWords.Count} words found.");

        List<string> solution = Solver.FindBestSolution(letter_sides, playableWords);
        Console.WriteLine("Best Solution: {0}", String.Join(", ", solution));
    }

    // Return whether a word can be played on the current puzzle
    private static bool CanBePlayed(string word, byte[] letter_sides)
    {
        // Ensure the word is long enough
        if (word.Length < 3) { return false; }

        // Ensure the word only uses letters in the puzzle, with no consecutive letters from the same side
        byte lastSide = 0;
        foreach (char letter in word)
        {
            byte letter_index = LetterToIndex(letter);
            if (letter_index >= 26) { return false; }

            byte side = letter_sides[letter_index];
            if (side == 0 || side == lastSide)
            {
                return false;
            }

            lastSide = side;
        }

        return true;
    }

    // Convert arguments into an array of which side each letter appears on (zero for letters not in the puzzle)
    private static byte[] ParsePuzzleLetters(string[] args) {
        byte[] letter_sides = new byte[26];
        byte side = 1;

        foreach (string arg in args) {
            foreach (char letter in arg) {
                byte letter_index = LetterToIndex(letter);
                letter_sides[letter_index] = side;
            }

            side++;
        }

        return letter_sides;
    }

    internal static byte LetterToIndex(char letter)
    {
        if (!Char.IsAsciiLetter(letter))
        {
            return 26;
            //throw new ArgumentOutOfRangeException(nameof(letter));
        }

        return (byte)(Char.ToLowerInvariant(letter) - 'a');
    }

    internal static int LetterToBit(char letter)
    {
        if (!Char.IsAsciiLetter(letter))
        {
            return 0;
        }

        return 1 << (Char.ToLowerInvariant(letter) - 'a');
    }

    internal static int IndexToBit(int letterIndex)
    {
        return 1 << letterIndex;
    }
}