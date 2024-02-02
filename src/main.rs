use std::collections::BinaryHeap;
use std::{env, process::ExitCode};
use crate::letter_distances::*;
use crate::answers::*;

mod answers;
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

    let mode = args[0].to_ascii_lowercase();
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

            let guess = &args[0];
            println!("Answer Distances from '{guess}':\n");

            let map = word_distance_map(&guess, ANSWERS);
            let distances = map.keys().collect::<BinaryHeap<_>>();

            // Iterate over map in increasing distance order
            for distance in distances.iter() {
                let answers = map.get(distance).unwrap();
                for answer in answers {
                    println!("{distance:05} -> {answer}");
                }
            }

            let cv = map_to_cv(&map);
            let cv = cv_to_string(&cv);

            println!("\n {} distinct responses.\n CV: {}", distances.len(), cv);
        }

        "dist" => {
            if args.len() != 2 {
                return print_usage("dist 'left' 'right' not provided.");
            }

            let left = &args[0];
            let right = &args[1];
            let distance = word_distance(&left, &right);
            println!("Distance ('{left}', '{right}') -> {distance}");
        }

        "best" => {
            let mut best = None;

            for guess in ANSWERS.iter() {
                let map = word_distance_map(guess, ANSWERS);
                let distinct_distances = map.len();

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
            let map = letter_distances::word_distance_map(&guess, &ANSWERS);
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

            let frequencies = letter_frequencies(ANSWERS);
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

            let options = answer_options(&guess, score, ANSWERS, within);
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