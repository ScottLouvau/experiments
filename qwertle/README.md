## QWERTLE Optimizing Code

This is Rust code designed to optimize the game (QWERTLE)[https://qwertle.friedman.in/]. In QWERTLE, like Wordle, you have six tries to guess a five letter word. Instead of a green, yellow, or black response to each guess letter, you get a gradient color telling you how far the answer letter is from the guess letter on a QWERTY keyboard.

I used 'best' mode to find the best guess word. Among the answers, that's PAPAL, with 2,117 distinct possible responses.

For PAPAL, run 'word' mode to generate a table of possible letters for each distance from each guess letter. It also lists the 'response score' for every possible Wordle answer.

If stumped during play (or just to cheat egregiously), run 'qwertle answer_options papal \<score>` and the code will find and return the answers with the closest distance scores to the one you see in the puzzle.

### Usage
```
  qwertle <mode> <args>

MODES:
  best                              Find guess with the most distinct responses
  word <guess>                      For a guess, response for each answer, cluster vector, and letter guess table.
  letter_options <guess> <score>    (lo) For a guess and score, show the possible letters at each position.
  answer_options <guess> <score>    (ao) For a guess and score, show possible answers closest to the score.
```

### Build

Local:
- Install Rust 
- cargo build -r

Docker:
- Install Docker
- docker build -t scottlouvau/qwertle .
(See 'test' script)