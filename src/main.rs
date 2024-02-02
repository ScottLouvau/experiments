use std::{env, fs, process::ExitCode};
use crate::letter_distances::*;

mod letter_distances;

// Goals:
//  - Compute letter distances accurately.
//  - Show letter options for guess and distances (qwertle options apple 46294)
//  - Compute distances between guess and all answer options
//  - Show answer buckets for a given guess.
//  - ? Draw keyboard with possible letters colored?

const USAGE: &str = "USAGE: 
  qwertle <mode> <args>

MODES:
  letter <letter>                   Show distance of each letter from <letter>
  word <guess>                      Show answer distances from guess
  dist <left> <right>               Show distance between two words
  best                              Find guess with the most distinct responses
  cv <guess>                        Show cluster vector of guess
  letter_options <guess> <score>    Show letter possibles for score
  answer_options <guess> <score>    Show possible answers for score";


fn main() -> ExitCode {
    let args: Vec<String> = env::args().collect();
    let mut args = &args[1..];

    if args.len() < 1 {
        return print_usage("No mode provided.");
    }

    let text = fs::read_to_string("answers.txt").unwrap();
    let text = text.to_ascii_lowercase();
    let answers = load_answers(&text);

    let mode = args.first().unwrap().to_ascii_lowercase();
    let mode = mode.as_str();
    args = &args[1..];

    match mode {
        "letter" => {
            if args.len() == 0 {
                return print_usage("letter 'letter' not provided.");
            }

            let from: char = args[0].chars().next().unwrap();
            for (_, to, distance_round, distance_exact) in distances_from_letter(from) {
                println!("{from}{to} -> {distance_round} ({distance_exact:.0})");
            }
        }

        "word" => {
            if args.len() == 0 {
                return print_usage("word 'word' not provided.");
            }

            let guess = args[0].to_ascii_lowercase();
            println!("Answer Distances from '{guess}':\n");

            let set = distances_from_guess(&guess, &answers);
            for (answer, distance) in set.iter() {
                println!("{distance:05} -> {answer}");
            }

            let distinct_distances = distinct_distances(&set);
            println!("\n{distinct_distances} distinct distances.");
        }

        "dist" => {
            if args.len() != 2 {
                return print_usage("dist 'left' 'right' not provided.");
            }

            let left = args[0].to_ascii_lowercase();
            let right = args[1].to_ascii_lowercase();
            let distance = word_distance(&left, &right);
            println!("Distance ('{left}', '{right}') -> {distance}");
        }

        "best" => {
            let mut best = None;

            // let text = fs::read_to_string("guesses.txt").unwrap();
            // let text = text.to_ascii_lowercase();
            // let guesses = load_answers(&text);
            let guesses = &answers;

            for guess in guesses.iter() {
                let set = distances_from_guess(&guess, &answers);
                let distinct_distances = distinct_distances(&set);

                if distinct_distances >= 2100 {
                    println!("{distinct_distances}: {guess}");
                }

                if distinct_distances > best.unwrap_or(("", 0)).1 {
                    best = Some((&guess, distinct_distances));
                }
            }

            if let Some((guess, distinct_distances)) = best {
                println!("Best: {guess} ({distinct_distances} distinct distances).");
            }
        }

        "cv" => {
            if args.len() == 0 {
                return print_usage("cv 'word' not provided.");
            }

            let guess = args[0].to_ascii_lowercase();
            let map = letter_distances::word_distance_map(&guess, &answers);
            let cv = map_to_cv(&map);
            let cv = cv_to_string(&cv);
            println!("{}", cv);
        }

        "lo" | "letter_options" => {
            if args.len() < 2 {
                return print_usage("letter_options 'guess' 'score' not provided.");
            }

            let guess = args[0].to_ascii_lowercase();
            let score = args[1].parse::<u32>().unwrap();

            let frequencies = letter_frequencies(&answers);
            let options = letter_options(&guess, score, &frequencies);
            println!("{}", options);
        }

        "ao" | "answer_options" => {
            if args.len() < 2 {
                return print_usage("answer_options 'guess' 'score' not provided.");
            }

            let guess = args[0].to_ascii_lowercase();
            let score = args[1].parse::<u32>().unwrap();
            let within: u32 = args.get(2).map(|s| s.parse().unwrap()).unwrap_or(2);

            let options = answer_options(&guess, score, &answers, within);
            for (distance, word, score) in options {
                println!("{distance}: {word} ({score:05})");
            }
        }

        _ => {
            return print_usage(&format!("Unknown mode: {}", mode));
        }
    }

    ExitCode::SUCCESS
}

fn print_usage(error: &str) -> ExitCode {
    println!("ERROR:\n  {}", error);
    println!("\n{}", USAGE);
    ExitCode::FAILURE
}

fn distances_from_letter(letter: char) -> Vec<(char, char, u8, f64)>{
    let mut set = Vec::new();

    for right in 0u8..26 {
        let right = (right + b'A') as char;
        let distance_round = letter_distances::distance_between_letters_quantized(letter, right);
        let distance_exact = letter_distances::distance_between_letters(letter, right);
        set.push((letter, right, distance_round, distance_exact));
    }

    set.sort_by(|a, b| a.3.partial_cmp(&b.3).unwrap());
    set
}

fn distances_from_guess<'a>(guess: &str, answers: &Vec<&'a str>) -> Vec<(&'a str, u32)> {
    let mut set = Vec::new();

    for answer in answers {
        let distance = word_distance(guess, *answer);
        set.push((*answer, distance));
    }

    set.sort_by(|a, b| a.1.partial_cmp(&b.1).unwrap());
    set
}

fn distinct_distances(set: &Vec<(&str, u32)>) -> u32 {
    let mut last_distance = 0;
    let mut distinct_distances = 0;

    for (_, distance) in set {
        if *distance > last_distance {
            last_distance = *distance;
            distinct_distances += 1;
        } else if *distance < last_distance {
            panic!("Distances must be sorted!");
        }
    }

    distinct_distances
}