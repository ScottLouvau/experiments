internal class Solver
{
    public readonly List<string> Words;

    public int PuzzleLettersLeft;
    public List<int> WordsInCurrentAnswer;

    public byte BestWordCount;
    public byte BestTotalLength;
    public List<int> WordsInBestAnswer;

    public Solver(List<string> words, byte[] letter_sides)
    {
        this.Words = words;

        for (int i = 0; i < letter_sides.Length; i++)
        {
            if (letter_sides[i] != 0)
            {
                this.PuzzleLettersLeft |= Program.IndexToBit(i);
            }
        }

        this.WordsInCurrentAnswer = new List<int>(5);
        this.BestWordCount = 100;
        this.BestTotalLength = 100;
        this.WordsInBestAnswer = new List<int>(5);
    }

    public static List<string> FindBestSolution(byte[] letter_sides, List<string> words)
    {
        Solver solver = new Solver(words, letter_sides);
        solver.FindNextWordRecursive('\0', 0, 0);
        return solver.WordsInBestAnswer.Select(i => words[i]).ToList();
    }

    private void FindNextWordRecursive(char startingWithLetter, int wordCount, int letterCount)
    {
        // If all puzzle letters were used, score this combination and keep the best
        if (this.PuzzleLettersLeft == 0)
        {
            if (wordCount <= this.BestWordCount || (wordCount == this.BestWordCount && letterCount < this.BestTotalLength))
            {
                this.BestWordCount = (byte)wordCount;
                this.BestTotalLength = (byte)letterCount;

                this.WordsInBestAnswer.Clear();
                foreach (int wordIndex in this.WordsInCurrentAnswer)
                {
                    this.WordsInBestAnswer.Add(wordIndex);
                }

                Console.WriteLine("Best: {0}", String.Join(", ", this.WordsInBestAnswer.Select(i => this.Words[i])));
            }

            return;
        }

        // If this combination is already worse than the best, stop
        if (wordCount > this.BestWordCount || letterCount >= this.BestTotalLength || wordCount >= 5)
        {
            return;
        }

        // Find the next word to add
        int lettersThisWordAdded = 0;

        for (int i = 0; i < this.Words.Count; i++)
        {
            string word = this.Words[i];

            // Only can play word if it continues the current sequence
            if (!word.StartsWith(startingWithLetter) && Char.IsAsciiLetter(startingWithLetter)) { continue; }

            // Mark letters this word added
            lettersThisWordAdded = 0;
            foreach (char letter in word)
            {
                int letter_bit = Program.LetterToBit(letter);
                if ((this.PuzzleLettersLeft & letter_bit) != 0)
                {
                    this.PuzzleLettersLeft &= ~letter_bit;
                    lettersThisWordAdded |= letter_bit;
                }
            }

            // Only play word if it adds at least one new letter
            if (lettersThisWordAdded == 0) { continue; }

            // Recurse
            this.WordsInCurrentAnswer.Add(i);
            this.FindNextWordRecursive(word[word.Length - 1], wordCount + 1, letterCount + word.Length);
            this.WordsInCurrentAnswer.RemoveAt(this.WordsInCurrentAnswer.Count - 1);

            // Unmark letters this word added
            this.PuzzleLettersLeft |= lettersThisWordAdded;
        }
    }
}