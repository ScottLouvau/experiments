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

        byte[] letter_sides = ParseLetterSides(args);
        if (letter_sides == null)
        {
            Usage();
            return;
        }

        List<string> words = File.ReadAllLines("Words.txt").Select(line => line.ToLowerInvariant()).ToList();
        List<string> playableWords = words.Where(word => CanBePlayed(word, letter_sides)).ToList();

        foreach (string word in playableWords)
        {
            Console.WriteLine(word);
        }

        Console.WriteLine($"{playableWords.Count} words found.");
    }

    // Return whether a word can be played on the current puzzle
    private static bool CanBePlayed(string word, byte[] letter_sides)
    {
        byte lastSide = 0;

        foreach (char letter in word)
        {
            byte letter_index = (byte)(Char.ToLowerInvariant(letter) - 'a');
            if (letter_index >= 26)
            {
                return false;
            }

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
    private static byte[] ParseLetterSides(string[] args) {
        byte[] letter_sides = new byte[26];
        byte side = 1;

        foreach (string arg in args) {
            foreach (char letter in arg) {
                byte letter_index = (byte)(Char.ToLowerInvariant(letter) - 'a');
                if (letter_index >= 26) {
                    return null;
                }

                letter_sides[letter_index] = side;
            }

            side++;
        }

        return letter_sides;
    }
}