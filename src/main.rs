use std::{env, process::ExitCode};
use crate::letter_distances::*;
use crate::answers::*;

mod answers;
mod letter_distances;

const USAGE: &str = "USAGE: 
  qwertle <mode> <args>

MODES:
  best                              Find guess with the most distinct responses
  word <guess>                      For a guess, response for each answer, cluster vector, and letter guess table.
  letter_options <guess> <score>    (lo) For a guess and score, show the possible letters at each position.
  answer_options <guess> <score>    (ao) For a guess and score, show possible answers closest to the score.";

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

        "word" => {
            if args.len() < 1 {
                return print_usage("word 'word' not provided.");
            }

            let guess = &args[0];
            println!("Answer Distances from '{guess}':\n");

            let map = word_distance_map(&guess, ANSWERS);
            let mut distances = map.keys().collect::<Vec<_>>();
            distances.sort();

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

            let table = letter_table(&guess, &ANSWERS);
            println!("\n{}", table);
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