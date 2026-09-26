use std::{error::Error, fs::File, io::{self, BufRead}};

// pub struct Word {
//     letters: [char; 4]
// }

// impl Word {

// }


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
        println!("{}", word);
    }

    Ok(())
}

fn main() {
    main_inner().expect("Error");
}
