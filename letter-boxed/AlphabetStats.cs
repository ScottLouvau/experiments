internal struct LetterStats
{
    public ushort WordsWithLetter;
    public ushort WordsStartingWithLetter;
    public ushort WordsEndingWithLetter;
}

internal class AlphabetStats
{
    public readonly LetterStats[] Letters = new LetterStats[26];
    public int WordCount { get; private set; }

    public void CountWord(string word)
    {
        for (int i = 0; i < word.Length; i++)
        {
            int letter_index = Program.LetterToIndex(word[i]);
            Letters[letter_index].WordsWithLetter++;

            if (i == 0)
            {
                Letters[letter_index].WordsStartingWithLetter++;
            }

            if (i == word.Length - 1)
            {
                Letters[letter_index].WordsEndingWithLetter++;
            }
        }

        WordCount++;
    }

    public int Score(string word, byte[] letter_sides)
    {
        int score = 0;

        for (int i = 0; i < word.Length; i++)
        {
            byte letter_index = Program.LetterToIndex(word[i]);
            byte side = letter_sides[letter_index];
            if (side != 0)
            {
                score += this.WordCount / Letters[letter_index].WordsWithLetter;
            }

            // if (i == 0)
            // {
            //     score += Letters[letter_index].WordsStartingWithLetter;
            // }

            // if (i == word.Length - 1)
            // {
            //     score += Letters[letter_index].WordsEndingWithLetter;
            // }
        }

        return score;
    }
}