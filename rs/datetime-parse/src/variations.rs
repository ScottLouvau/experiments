use std::{fs::{self, File}, error::Error, io::{BufReader, BufRead, Read}, str};
use chrono::{DateTime, FixedOffset};
use crate::{parse, file_iterators::file_foreach_line};

// ---- Contenders ----

// pub fn original(file_path: &str) -> Result<Vec<DateTime<FixedOffset>>, Box<dyn Error>> {
//     let mut result = Vec::new();

//     self::file_foreach_line_str(file_path, &mut |line| {
//         let dt = DateTime::parse_from_rfc3339(line).expect("DateTime Parse Error");
//         result.push(dt);
//     })?;

//     Ok(result)
// }

pub fn original(file_path: &str) -> Result<Vec<DateTime<FixedOffset>>, Box<dyn Error>> {
        let mut result = Vec::new();

    file_foreach_line(file_path, &mut |line| {
        let str = str::from_utf8(line).expect("UTF8 Parse Error");
        let dt = DateTime::parse_from_rfc3339(str).expect("DateTime Parse Error");
        result.push(dt);
    })?;

    Ok(result)
}

// // Weird for Rust, but this is reading by line, validating UTF-8, then parsing with the most common 'ParseExact' equivalent.
// pub fn original(file_path: &str) -> Result<Vec<DateTime<FixedOffset>>, Box<dyn Error>> {
//     let mut result = Vec::new();

//     let file = File::open(file_path)?;
//     let mut reader = BufReader::with_capacity(29 * 4096, file);
    
//     loop {
//         let buffer = reader.fill_buf()?;
//         let length_read = buffer.len();
//         if length_read == 0 { break; }

//         let mut length_left = 0;
//         for line in buffer.split_inclusive(|c| *c == b'\n') {
//             if line.last().is_some_and(|c| *c == b'\n') {
//                 let line = &line[0..line.len()-1];
//                 let str = str::from_utf8(line).expect("UTF8 Parse Error");
//                 let dt = DateTime::parse_from_rfc3339(str).expect("DateTime Parse Error");
//                 result.push(dt);
//             } else {
//                 length_left = line.len();
//                 break;
//             }
//         };

//         let length_parsed = length_read - length_left;
//         reader.consume(length_parsed);
//     }

//     Ok(result)
// }


// // 350 ms
// pub fn span(file_path: &str) -> Result<Vec<MyDateTime>, Box<dyn Error>> {
//     let contents = fs::read(file_path)?;
//     Ok(contents
//         .split(|c| *c == b'\n')
//         .map(|line| MyDateTime::parse_validating(line))
//         .flat_map(|d| d)
//         .collect()
//     )
// }

// // // 190 ms
pub fn span(file_path: &str) -> Result<Vec<MyDateTime>, Box<dyn Error>> {
    let mut result = Vec::new();

    let file = File::open(file_path)?;
    let mut reader = BufReader::with_capacity(29 * 4096, file);
    
    loop {
        let buffer = reader.fill_buf()?;
        let length_read = buffer.len();
        if length_read == 0 { break; }

        let mut length_left = 0;
        for line in buffer.split_inclusive(|c| *c == b'\n') {
            if line.last().is_some_and(|c| *c == b'\n') {
            let dt = MyDateTime::parse_validating(line).expect("DateTime Parse Error");
                result.push(dt);
            } else {
                length_left = line.len();
                break;
            }
        };

        let length_parsed = length_read - length_left;
        reader.consume(length_parsed);
    }

    Ok(result)
}

// 280 ms
pub fn parse_custom(file_path: &str) -> Result<Vec<MyDateTime>, Box<dyn Error>> {
    let contents = fs::read(file_path)?;
    Ok(contents
        .split(|c| *c == b'\n')
        .map(|line| MyDateTime::parse_validating(line))
        .flat_map(|d| d)
        .collect()
    )
}

// 190 ms
pub fn blocks_parse_custom(file_path: &str) -> Result<Vec<MyDateTime>, Box<dyn Error>> {
    let mut result = Vec::new();

    let file = File::open(file_path)?;
    let mut reader = BufReader::with_capacity(29 * 4096, file);
    
    loop {
        let buffer = reader.fill_buf()?;
        let length_read = buffer.len();
        if length_read == 0 { break; }

        let mut length_left = 0;
        for line in buffer.split_inclusive(|c| *c == b'\n') {
            if line.last().is_some_and(|c| *c == b'\n') {
                let dt = MyDateTime::parse_validating(line).expect("DateTime Parse Error");
                result.push(dt);
            } else {
                length_left = line.len();
                break;
            }
        };

        let length_parsed = length_read - length_left;
        reader.consume(length_parsed);
    }

    Ok(result)
}

// 105 ms
pub fn custom(file_path: &str) -> Result<Vec<MyDateTime>, Box<dyn Error>> {
    let mut result = Vec::new();

    let file = File::open(file_path)?;
    let mut reader = BufReader::with_capacity(29 * 4096, file);

    loop {
        let mut buffer = reader.fill_buf()?;
        let length_read = buffer.len();
        if length_read < 29 { break; }

        while buffer.len() >= 29 {
            let dt = MyDateTime::parse_validating(&buffer[0..28]).expect("DateTime Parse Error");
            result.push(dt);

            buffer = &buffer[29..];
        }

        let length_parsed = length_read - buffer.len();
        reader.consume(length_parsed);
    }

    Ok(result)
}

pub fn custom_noerrors(file_path: &str)  -> Result<Vec<MyDateTime>, Box<dyn Error>> {
    let mut result = Vec::new();

    let file = File::open(file_path)?;
    let mut reader = BufReader::with_capacity(29 * 4096, file);

    loop {
        let mut buffer = reader.fill_buf()?;
        let length_read = buffer.len();
        if length_read < 29 { break; }

        while buffer.len() >= 29 {
            let dt = MyDateTime::parse_noerrors(&buffer[0..28]).expect("DateTime Parse Error");
            result.push(dt);

            buffer = &buffer[29..];
        }

        let length_parsed = length_read - buffer.len();
        reader.consume(length_parsed);
    }

    Ok(result)
}

pub struct MyDateTime {
    pub year: u16,
    pub month: u16,
    pub day: u16,
    pub hour: u16,
    pub minute: u16,
    pub second: u16,
    pub microseconds: u32,
}

impl MyDateTime {
    //  Uses 'O' DateTime format, which is 28 bytes (30 with \r\n)
    //    2022-04-14T02:32:53.4028225Z
    //    **** ** ** ** ** ** *******
    //    0123456789012345678901234567

    pub fn parse_validating(value: &[u8]) -> Option<MyDateTime> {
        if value.len() < 28 { return None; }

        let year = parse::u16(&value[0..=3])?;
        let month = parse::u8(&value[5..=6])? as u16;
        let day = parse::u8(&value[8..=9])? as u16;
        let hour = parse::u8(&value[11..=12])? as u16;
        let minute = parse::u8(&value[14..=15])? as u16;
        let second = parse::u8(&value[17..=18])? as u16;
        let microseconds = parse::u32(&value[20..=26])?;  

        Some(MyDateTime { year, month, day, hour, minute, second, microseconds })
    }

    pub fn parse_noerrors(value: &[u8]) -> Option<MyDateTime> {
        if value.len() != 28 { return None; }

        let year = parse::u16_ne(&value[0..=3]);
        let month = parse::u8_2ne(&value[5..=6]) as u16;
        let day = parse::u8_2ne(&value[8..=9]) as u16;
        let hour = parse::u8_2ne(&value[11..=12]) as u16;
        let minute = parse::u8_2ne(&value[14..=15]) as u16;
        let second = parse::u8_2ne(&value[17..=18]) as u16;
        let microseconds = parse::u32_ne(&value[20..=26]);

        Some(MyDateTime { year: year, month, day, hour, minute, second, microseconds })
    }

    pub fn parse_unrolled(t: &[u8]) -> Option<MyDateTime> {
        const ZERO: u8 = b'0';
        if t.len() != 28 { return None; }

        let year: u16 = 1000 * t[0] as u16 + 100 * t[1] as u16 + 10 * t[2] as u16 + t[3] as u16 - 1111 * ZERO as u16;
        let month = 10 * t[5] as u16 + t[6] as u16 - 11 * ZERO as u16;
        let day = 10 * t[8] as u16 + t[9] as u16 - 11 * ZERO as u16;
        let hour = 10 * t[11] as u16 + t[12] as u16 - 11 * ZERO as u16;
        let minute = 10 * t[14] as u16 + t[15] as u16 - 11 * ZERO as u16;
        let second = 10 * t[17] as u16 + t[18] as u16 - 11 * ZERO as u16;

        let microseconds = 
            1000000 * t[20] as u32 
            + 100000 * t[21] as u32 
            + 10000 * t[22] as u32 
            + 1000 * t[23] as u32 
            + 100 * t[24] as u32 
            + 10 * t[25] as u32 
            + t[26] as u32 
            - 1111111 * ZERO as u32;

        Some(MyDateTime { year: year, month, day, hour, minute, second, microseconds })
    }
}

// ---- Naive Implementations ----

// Most likely Stack Overflow answer; read the file into one huge string, then parse with the most common library.
pub fn naive_rust(file_path: &str) -> Result<Vec<DateTime<FixedOffset>>, Box<dyn Error>> {
    let contents = fs::read_to_string(file_path)?;
    Ok(contents
        .lines()
        .map(|line| DateTime::parse_from_rfc3339(line))
        .flat_map(|d| d)
        .collect())
}

// Most common read file by line sample; slower than the previous one.
pub fn naive_readline(file_path: &str) -> Result<Vec<DateTime<FixedOffset>>, Box<dyn Error>> {
    let file = File::open(file_path)?;
    let reader = BufReader::new(file);
    Ok(reader
        .lines()
        .flat_map(|l| l)
        .map(|line| DateTime::parse_from_rfc3339(&line))
        .flat_map(|d| d)
        .collect()
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parse_datetime() {
        let dt = MyDateTime::parse_validating("2022-04-14T02:32:53.4028225Z".as_bytes()).unwrap();
        assert_eq!(2022, dt.year);
        assert_eq!(4, dt.month);
        assert_eq!(14, dt.day);
        assert_eq!(2, dt.hour);
        assert_eq!(32, dt.minute);
        assert_eq!(53, dt.second);
        assert_eq!(4028225, dt.microseconds);
    }
}
