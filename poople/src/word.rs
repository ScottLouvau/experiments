use std::fmt::{Debug, Write};
use anyhow::{Result, anyhow};

#[derive(Clone, Copy, Hash, PartialOrd, Ord)]
pub struct Word {
    letters: [u8; 4]
}

impl Word {
    // Create a new Word from text
    pub fn new(text: &str) -> Result<Word> {
        let mut word = [b' '; 4];
        for (i, l) in text.bytes().take(4).enumerate() {
            word[i] = l;
        }

        if text.len() != 4 {
            Err(anyhow!("Word \"{}\" was the wrong length.", text).into())
        } else {
            Ok(Word { letters: word })
        }
    }

    // Compute distance between words
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



#[cfg(test)]
mod tests {
    use super::*;
    use std::collections::HashMap;
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