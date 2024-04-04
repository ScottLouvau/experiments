using Xunit;

/*
  Goals:
   - List all playable words
   - List playable words with constraint (length, includes letters, starts with, ends with)
   - Find the shortest (fewest words, then least total length) solution

  Currently I'm filtering to playable words, then trying combinations recursively.
  This is theoretically inefficient but it looks like there are often few chainable words (few playable words starting with the last word's last letter).

  TODO:
   - Reduce recursive time by:
      - Convert each word into the int of included letters.
      - Look for singles and pairs which solve before recursing deeper.

*/

public static class Program 
{
    public static void Usage()
    {
        Console.WriteLine(@"Usage:    letter-boxed ABC DEF GHI JKL <constraints?> <solve?>
    Constraints: 
      s_  (starts with 's')
      _s  (ends with 's')
      _s_ (contains 's')
      #5  (length 5)"
      );
    }

    public static void Main(string[] arguments)
    {
        IEnumerable<string> args = arguments;

        byte[] letter_sides = ParsePuzzleLetters(args);
        args = args.Skip(4);
        if (letter_sides == null)
        {
            Usage();
            return;
        }

        bool solvePuzzle = false;
        Func<string, bool> constraints = word => CanBePlayed(word, letter_sides);

        while(args.Any())
        {
            string arg = args.First().ToLowerInvariant();

            if (arg == "solve") {
                solvePuzzle = true;
                args = args.Skip(1);
            }
            else if (arg.StartsWith("#"))
            {
                int length = int.Parse(arg.Substring(1));
                constraints = And(constraints, word => word.Length == length);
                args = args.Skip(1);
            }
            else if (arg.Length == 3 && arg.StartsWith("_") && arg.EndsWith("_")) {
                char letter = arg[1];
                constraints = And(constraints, word => word.Contains(letter));
                args = args.Skip(1);
            }
            else if (arg.Length == 2 && arg.StartsWith("_")) {
                char letter = arg[1];
                constraints = And(constraints, word => word.EndsWith(letter));
                args = args.Skip(1);
            }
            else if (arg.Length == 2 && arg.EndsWith("_")) {
                char letter = arg[0];
                constraints = And(constraints, word => word.StartsWith(letter));
                args = args.Skip(1);
            }
            else {
                Console.WriteLine("Unknown argument: {0}", arg);
                Usage();
                return;
            }
        }

        // Load Dictionary of valid words
        //string dictionaryPath = @"Words.txt";
        string dictionaryPath = @"words_easy.txt";
        List<string> words = File.ReadAllLines(dictionaryPath).Select(line => line.ToLowerInvariant()).ToList();

        // Static Letter statistics (one-time)
        // AlphabetStats stats = new AlphabetStats(words);
        // stats.Commonality();
        // stats.Chainability();

        // Filter to words that are playable in the current puzzle and match other passed constraints
        List<string> playableWords = words.Where(constraints).ToList();
        foreach (string word in playableWords)
        {
            Console.WriteLine(word);
        }

        Console.WriteLine($"{playableWords.Count} words found.");

        if (solvePuzzle) {
            List<string> solution = Solver.FindBestSolution(letter_sides, playableWords);
            Console.WriteLine("Best Solution: {0}", String.Join(", ", solution));
        }
    }

    // AND together two constraints.
    //  Must be done in a separate function so that 'left' is the constraints before 'right' is added, not whatever the final set of constraints is.
    //  Otherwise you get infinite recursion on left.
    private static Func<string, bool> And(Func<string, bool> left, Func<string, bool> right) {
        return word => left(word) && right(word);
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
    private static byte[] ParsePuzzleLetters(IEnumerable<string> args) {
        byte[] letter_sides = new byte[26];
        byte side = 1;

        foreach (string arg in args.Take(4)) {
            foreach (char letter in arg) {
                byte letter_index = LetterToIndex(letter);
                if (letter_index >= 26) { return null; }

                letter_sides[letter_index] = side;
            }

            side++;
        }

        if (side != 5) { return null; }
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

public class Tests
{
    [Fact]
    public void MedianOfTwoSortedArrays()
    {
    }
}