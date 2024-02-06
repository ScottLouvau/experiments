use std::{fs::File, error::Error, io::Read, str};

// TODO: Need to test that this is actually working correctly.
// TODO: Must not read after a read returns zero bytes (even if there are reused bytes and action won't consume them)
// TODO: Should grow buffer if action wouldn't consume any bytes.
// Can I take file by traits? Should I take buffer? Can I wrap in a struct?

pub fn file_foreach_line(file_path: &str, action: &mut dyn FnMut(&[u8])) -> Result<(), Box<dyn Error>> {
    let mut file = File::open(file_path).unwrap();
    let mut buffer = [0u8; 64 * 1024];
    let mut length_reused = 0;

    loop {
        let length_valid = file.read(&mut buffer[length_reused..])? + length_reused;
        if length_valid == 0 { break; }

        let mut length_left = 0;
        for line in buffer[0..length_valid].split_inclusive(|c| *c == b'\n') {
            if line.last().is_some_and(|c| *c == b'\n') {
                let line = &line[0..line.len()-1];
                
                action(line);
            } else {
                length_left = line.len();
                break;
            }
        };

        if length_left > 0 {
            buffer.copy_within(length_valid - length_left..length_valid, 0);
            length_reused = length_left;
        } else {
            length_reused = 0;
        }
    }

    Ok(())
}

pub fn file_foreach_line_str(file_path: &str, action: &mut dyn FnMut(&str)) -> Result<(), Box<dyn Error>> {
    self::file_foreach_line(file_path, &mut |line| {
        let line = str::from_utf8(line).expect("UTF8 Parse Error");
        action(line);
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    // #[test]
    // fn parse_datetime() {

    // }
}
