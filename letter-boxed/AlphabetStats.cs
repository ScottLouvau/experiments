public struct LetterStats
{
    public int WordsWithLetter;
    public int WordsStartingWithLetter;
    public int WordsEndingWithLetter;
}

public class AlphabetStats
{
    public readonly LetterStats[] Letters = new LetterStats[26];
    public int WordCount { get; private set; }

    public AlphabetStats(IEnumerable<string> words)
    {
        foreach (string word in words)
        {
            CountWord(word);
        }
    }

    public void CountWord(string word)
    {
        if (word.Length < 3 || word.Contains("'")) { return; }

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

    // List letters by how often they appear across all valid words.
    public void Commonality()
    {
        List<Tuple<char, int>> commonality = new List<Tuple<char, int>>();
        for (int i = 0; i < 26; i++)
        {
            char letter = (char)('a' + i);
            int score = this.Letters[i].WordsWithLetter;
            commonality.Add(Tuple.Create(letter, score));
        }

        commonality.Sort((l, r) => r.Item2.CompareTo(l.Item2));

        Console.WriteLine("Commonality:");
        foreach (var item in commonality)
        {
            Console.WriteLine("{0}: {1}", item.Item1, item.Item2);
        }
        Console.WriteLine();
    }

    // List letters by how 'chainable' they are.
    // This is the lesser of how many words start with or end with the letter.
    public void Chainability()
    {
        List<Tuple<char, int>> chainability = new List<Tuple<char, int>>();
        for (int i = 0; i < 26; i++)
        {
            char letter = (char)('a' + i);
            int score = Math.Min(this.Letters[i].WordsStartingWithLetter, this.Letters[i].WordsEndingWithLetter);
            chainability.Add(Tuple.Create(letter, score));
        }

        chainability.Sort((l, r) => r.Item2.CompareTo(l.Item2));

        Console.WriteLine("Chainability:");
        foreach (var item in chainability)
        {
            Console.WriteLine("{0}: {1}", item.Item1, item.Item2);
        }
        Console.WriteLine();
    }
}