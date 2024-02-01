use std::collections::HashMap;


pub const LETTER_POSITIONS: [(u16, u16); 26] = [
    (31, 72),
    (283, 136),
    (182, 136),
    (132, 72),
    (108, 8),
    (182, 72),
    (232, 72),
    (283, 72),
    (358, 8),
    (333, 72),
    (384, 72),
    (434, 72),
    (384, 136),
    (333, 136),
    (408, 8),
    (458, 8),
    (8, 8),
    (158, 8),
    (81, 72),
    (208, 8),
    (308, 8),
    (232, 136),
    (58, 8),
    (132, 136),
    (258, 8),
    (81, 36)
];

pub fn distance_between_letters(mut left: char, mut right: char) -> f64 {
    left = left.to_ascii_uppercase();
    right = right.to_ascii_uppercase();

    let (x1, y1) = LETTER_POSITIONS[(left as u8 - b'A') as usize];
    let (x2, y2) = LETTER_POSITIONS[(right as u8 - b'A') as usize];

    let (dx, dy) = (x2 as f64 - x1 as f64, y2 as f64 - y1 as f64);
    (dx * dx + dy * dy).sqrt()
}

pub fn distance_between_letters_quantized(left: char, right: char) -> u8 {
    let dist = distance_between_letters(left, right);
    (dist / 50.0).round() as u8
}

pub fn word_distance(left: &str, right: &str) -> u32 {
    let mut distance = 0u32;

    for (l, r) in left.chars().zip(right.chars()) {
        distance *= 10;
        distance += distance_between_letters_quantized(l, r) as u32;
    }

    distance
}

pub fn score_to_digits(score: u32) -> Vec<u8> {
    let mut digits = Vec::new();
    let mut left = score;

    while left > 0 {
        digits.push((left % 10) as u8);
        left /= 10;
    }

    digits.reverse();
    digits
}

pub fn load_answers<'a>(text: &'a String) -> Vec<&'a str> {
    let mut answers = Vec::new();

    for line in text.lines() {
        answers.push(line);
    }

    answers
}

pub fn word_distance_map<'a>(guess: &str, answers: &[&'a str]) -> HashMap<u32, Vec<&'a str>> {
    let mut map: HashMap<u32, Vec<&str>> = HashMap::new();

    for answer in answers {
        let distance = word_distance(guess, answer);
        let entry = map.entry(distance);
        entry.or_insert(Vec::new()).push(*answer);
    }

    map
}

pub fn map_to_cv(map: &HashMap<u32, Vec<&str>>) -> Vec<u32> {
    let mut cv = Vec::new();

    for (_, answers) in map {
        let count = answers.len();
        while cv.len() < count {
            cv.push(0);
        }

        cv[count - 1] += 1;
    }

    cv
}

pub fn cv_to_string(cv: &[u32]) -> String {
    let mut result = String::new();
    result += "[";

    for (i, count) in cv.iter().enumerate() {
        if i > 0 {
            result += ", ";
        }
        result += &format!("{}", count);
    }

    result += "]";
    result
}

#[cfg(test)]
mod tests {
    use std::fs;
    use super::*;

    #[test]
    fn letter_distances() {
        for l in 'a'..='z' {
            assert_eq!(0, distance_between_letters_quantized(l, l));
        }

        assert_eq!(1, distance_between_letters_quantized('O', 'p'));
        assert_eq!(1, distance_between_letters_quantized('l', 'p'));
        assert_eq!(2, distance_between_letters_quantized('K', 'P'));
        assert_eq!(3, distance_between_letters_quantized('m', 'P'));

        assert_eq!(4, distance_between_letters_quantized('g', 'a'));
    }

    #[test]
    fn word_distances() {
        assert_eq!(0, word_distance("hello", "hello"));
        assert_eq!(42521, word_distance("apple", "vivid"));
    }

    #[test]
    fn score_to_digits_test() {
        assert_eq!(vec![1, 2, 3], score_to_digits(123));
        assert_eq!(vec![1, 0, 0, 0], score_to_digits(1000));
        assert_eq!(vec![3, 5, 4, 6, 9], score_to_digits(35469));
    }

    #[test]
    fn map_and_cv() {
        let text = fs::read_to_string("answers.txt").unwrap();
        let answers = load_answers(&text);
        assert_eq!(2315, answers.len());

        let map = word_distance_map("apple", &answers);
        assert_eq!(2315, map.iter().map(|(_, v)| v.len()).sum::<usize>());

        let cv = map_to_cv(&map);
        assert_eq!(vec![1914, 162, 23, 2], cv);

        let cv = cv_to_string(&cv);
        assert_eq!("[1914, 162, 23, 2]", cv);
    }
}