
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
