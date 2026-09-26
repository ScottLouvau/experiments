mod word;

use std::{
    collections::{HashMap, HashSet, VecDeque},
    fs::File, 
    io::{self, BufRead}
};

use anyhow::{Context, Result, anyhow};
use word::Word;


// GOAL:
//  Emit a partial solution tree for https:://poople.io.
//  Starting with the word POOP (the target), build a tree where each child is a word one letter away from the parent.
//  Show each word at only the shallowest position where it may appear in the tree.
//  Show words which may appear under multiple parents only under the one with the largest number of unique descendants.


fn read_words() -> Result<HashSet<Word>> {
    let mut words = HashSet::new();

    let file = File::open("valid.txt")?;
    let reader = io::BufReader::new(file);

    for line in reader.lines() {
        let line = line?;
        let mut parts = line.split(',');
        let word = parts.next().ok_or(anyhow!("Line without comma found"))?;
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

fn main_inner() -> Result<()> {
    let args: Vec<String> = std::env::args().collect();
    let depth_limit = args
        .get(1).ok_or(anyhow!("USAGE: poople <depth_limit>"))?
        .parse::<u8>().context("Could not parse depth limit. Ex: `poople 3`")?;

    let root = Word::new("POOP").unwrap();

    let mut distinct_words = read_words()
        .context("Could not read words.txt list")?;

    distinct_words.remove(&root);

    let word_list: Vec<Word> = distinct_words.into_iter().collect();

    let word_tree = into_word_tree(root, word_list);
    print_tree(&root, 0, depth_limit, &word_tree);

    Ok(())
}

fn main() {
    if let Err(error) = main_inner() {
        eprintln!("{}", error);
        std::process::exit(1);
    }
}