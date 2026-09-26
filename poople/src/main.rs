use std::{collections::{HashMap, HashSet, VecDeque}, error::Error, fmt::{Debug, Write}, fs::File, io::{self, BufRead}};

// GOAL:
//  Emit a partial solution tree for https:://poople.io.
//  Starting with the word POOP (the target), build a tree where each child is a word one letter away from the parent.
//  Show each word at only the shallowest position where it may appear in the tree.
//  Show words which may appear under multiple parents only under the one with the largest number of unique descendants.


#[derive(Clone, Copy, Hash, PartialOrd, Ord)]
pub struct Word {
    letters: [u8; 4]
}

impl Word {
    pub fn new(text: &str) -> Result<Word, Box<dyn Error>> {
        let mut word = [b' '; 4];
        for (i, l) in text.bytes().take(4).enumerate() {
            word[i] = l;
        }

        if text.len() != 4 {
            Err(format!("Word \"{}\" was the wrong length.", text).into())
        } else {
            Ok(Word { letters: word })
        }
    }

    pub fn distance_from(&self, other: &Word) -> u8 {
        let mut distance = 0;

        for i in 0..4 {
            if self.letters[i] != other.letters[i] {
                distance += 1;
            }
        }

        distance
    }
}

impl std::fmt::Display for Word {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        for c in self.letters {
            f.write_char(c as char)?;
        }

        Ok(())
    }
}

impl Debug for Word {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("Word").field("letters", &self.letters).finish()
    }
}


impl PartialEq for Word {
    fn eq(&self, other: &Self) -> bool {
        for i in 0..4 {
            if self.letters[i] != other.letters[i] {
                return false;
            }
        }

        true
    }
}

impl Eq for Word {} 

fn read_words() -> Result<HashSet<Word>, Box<dyn Error>> {
    let mut words = HashSet::new();

    let file = File::open("valid.txt")?;
    let reader = io::BufReader::new(file);

    for line in reader.lines() {
        let line = line?;
        let mut parts = line.split(',');
        let word = parts.next().ok_or("Line without comma found")?;
        let word = Word::new(word)?;
        words.insert(word);
    }

    Ok(words)
}

fn into_word_tree(root: Word, mut words: Vec<Word>) -> HashMap<Word, Vec<Word>> {
    let mut map = HashMap::new();

    let mut pending = VecDeque::new();
    pending.push_back(root);
    
    while let Some(next) = pending.pop_front() {
        let children = extract_children(&next, &mut words);
        for child in children.iter() {
            pending.push_back(child.clone());
        }

        map.insert(next, children);
    }

    let _c2 = map.get(&root);
    map
}

fn extract_children(under: &Word, words: &mut Vec<Word>) -> Vec<Word> {
    let children: Vec<Word> = words.extract_if(..,|&mut word| under.distance_from(&word) == 1).collect();
    children
}

fn print_tree(current: &Word, indent: u8, depth_limit: u8, tree: &HashMap<Word, Vec<Word>>) {
    for _ in 0..indent {
        print!("{}", '\t');
    }

    println!("{}", current);

    if indent < depth_limit {
        if let Some(children) = tree.get(current) {
            for child in children.iter() {
                print_tree(child, indent + 1, depth_limit, tree);
            }
        }
    }
}

fn main() {
    let root = Word::new("POOP").unwrap();

    let mut distinct_words = read_words().expect("Error");
    distinct_words.remove(&root);

    let word_list: Vec<Word> = distinct_words.into_iter().collect();

    let word_tree = into_word_tree(root, word_list);
    let depth_limit = 30;
    print_tree(&root, 0, depth_limit, &word_tree);
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::hash::{BuildHasher, RandomState};

    #[test]
    fn word_basics() {
        // New and Display
        let source = "POOP";
        let poop = Word::new(&source).unwrap();
        let text = &format!("{}", poop);
        assert_eq!(source, text);

        let state = RandomState::new();
        let p2 = Word::new("POOP").unwrap();
        assert_eq!(state.hash_one(&poop), state.hash_one(&p2));
        assert_eq!(poop, p2);

        // Too short, too long
        assert!(Word::new("POP").is_err());
        assert!(Word::new("POOPY").is_err());

        // Distance
        let goop = Word::new("GOOP").unwrap();
        assert_eq!(poop.distance_from(&goop), 1);
        assert_ne!(poop, goop);

        let good = Word::new("GOOD").unwrap();
        assert_eq!(poop.distance_from(&good), 2);
        assert_eq!(goop.distance_from(&good), 1);
    }

    #[test]
    fn word_hash() {
        let mut map = HashMap::new();
        let poop = Word::new("POOP").unwrap();
        let goop = Word::new("GOOP").unwrap();

        map.insert(poop, 1);
        map.insert(goop, 2);

        assert_eq!(map.get(&poop), Some(&1));
        assert_eq!(map.get(&goop), Some(&2));

        let p2 = Word::new("POOP").unwrap();
        assert_eq!(map.get(&p2), Some(&1));
    }
}