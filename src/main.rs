mod letter_distances;

fn main() {
    let mut set = Vec::new();

    let left = 'p';
    for right in 0u8..26 {
        let right = (right + b'A') as char;
        let dist = letter_distances::distance_between_letters(left, right);
        set.push((left, right, dist));
    }

    set.sort_by(|a, b| a.2.partial_cmp(&b.2).unwrap());
    for (left, right, dist) in set {
        println!("{left}{right} => {dist:.0}");
    }
}
