use std::{error::Error, fmt::Write, fs::File, io::{self, BufRead}};

// GOAL:
//  Emit a partial solution tree for https:://poople.io.
//  Starting with the word POOP (the target), build a tree where each child is a word one letter away from the parent.
//  Show each word at only the shallowest position where it may appear in the tree.
//  Show words which may appear under multiple parents only under the one with the largest number of unique descendants.


pub struct Word {
    letters: [char; 4]
}

impl Word {
    pub fn new(text: &str) -> Result<Word, Box<dyn Error>> {
        let mut word = [' '; 4];
        for (i, l) in text.chars().take(4).enumerate() {
            word[i] = l;
        }

        if word[3] == ' ' {
            Err(format!("Word \"{}\" was too short.", text).into())
        } else {
            Ok(Word { letters: word })
        }
    }

    pub fn write(&self) {
        // for c in self.letters {
        //     print!("{}", c);
        // }

        println!("{:?}", self.letters);
    }
}

impl std::fmt::Display for Word {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        for c in self.letters {
            f.write_char(c)?;
        }

        Ok(())
    }
}


// // Read file by lines (efficiently; in blocks, as iterator on &str)
// pub fn read_by_lines(file_path: &str) -> io::Result<io::Lines<io::BufReader<fs::File>>> {
//     let file = File::open(file_path)?;
//     Ok(io::BufReader::new(file).lines())
// }

// // Parse a line (trim, convert to int, collect into Vec)
// fn parse_line(line: &str) -> Result<Vec<i32>, Box<dyn Error>> {
//     Ok(line
//         .split_ascii_whitespace()
//         .map(|l| l.parse::<i32>().expect("Input number didn't parse"))
//         .collect()
//     )
// }

fn main_inner() -> Result<(), Box<dyn Error>> {
    let file = File::open("valid.txt")?;
    let reader = io::BufReader::new(file);

    for line in reader.lines() {
        let line = line?;
        let mut parts = line.split(',');
        let word = parts.next().ok_or("Line without comma found")?;
        let word = Word::new(word)?;
        println!("{}", word);
    }

    Ok(())
}

fn main() {
    main_inner().expect("Error");
}
