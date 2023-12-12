use std::{fs::{self, File}, error::Error, io::{BufReader, BufRead, Read}, str};
use chrono::{DateTime, FixedOffset};

mod parse;

// C# 6.0 speeds (Apple M1 Pro 8 core)
// =========
// | ms        | Name                                      | Speedup    | DateTimes/s |
// | --------- | ----------------------------------------- | ---------- | ----------- |
// | 6,901     | Original                                  | 1.00x      | 1,450,000   |
// | 3,704     | AdjustToUniversal                         | 1.86x      | 2,700,000   |
// | 2,866     | ParseExact (output not UTC)               | 2.41x      | 3,490,000   |
// | **1,492** | **DateTimeOffset**                        | **4.62x**  | 6,720,000   |
// | **878**   | **Span<char> + DateTimeOffset**           | **7.86x**  | 11,390,000  |
// | 1,080     | Span<char> + int.TryParse parts           | **6.39x**  | 9,260,000   |
// | **592**   | **Span<byte> + Utf8Parser**               | **11.66x** | 16,900,000  |
// | 485       | Span<byte> + handwritten parse            | 14.23x     | 20,600,000  |
// | 182       | Span<byte> + unrolled parse (no checking) | 37.91x     | 54,900,000  |


// |    ms | C# 8                 |
// | ----- | -------------------- |
// |   518 | Original             |
// |   282 | Span                 |
// |   265 | Custom               |
// |   200 | Custom_MyParse       |
// |    97 | Custom_NoErrors      |


// Methods:
//  - Original: ReadLines, DateTimeOffset.ParseExact, List.Add
//  - Span: ReadBytes, Span, Utf8Parser, List.Add
//  - Custom: ReadBytes, Split at Known Length, Utf8Parser Pieces, List.Add
//  - Custom_NoErrors: Custom with no error detection

// 1,050 ms
pub fn original(file_path: &str) -> Result<Vec<DateTime<FixedOffset>>, Box<dyn Error>> {
    let contents = fs::read_to_string(file_path)?;
    Ok(contents
        .lines()
        .map(|line| DateTime::parse_from_rfc3339(line))
        .flat_map(|d| d)
        .collect())
}

// Blocks?
// 1,300 ms
pub fn read_by_line(file_path: &str) -> Result<Vec<DateTime<FixedOffset>>, Box<dyn Error>> {
    let file = File::open(file_path)?;
    let reader = BufReader::with_capacity(29 * 4096, file);
    Ok(reader
        .lines()
        .flat_map(|l| l)
        .map(|line| DateTime::parse_from_rfc3339(&line))
        .flat_map(|d| d)
        .collect()
    )
}

// 350 ms
pub fn span(file_path: &str) -> Result<Vec<DateTime<FixedOffset>>, Box<dyn Error>> {
    let contents = fs::read(file_path)?;
    Ok(contents
        .split(|c| *c == b'\n')
        .map(|line| str::from_utf8(line))
        .flat_map(|l| l)
        .map(|line| DateTime::parse_from_rfc2822(line))// MyDateTime::parse_validating(line))
        .flat_map(|d| d)
        .collect()
    )
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
pub fn parse_known_split(file_path: &str) -> Result<Vec<MyDateTime>, Box<dyn Error>> {
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

pub fn parse_noerrors(file_path: &str)  -> Result<Vec<MyDateTime>, Box<dyn Error>> {
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
    pub month: u8,
    pub day: u8,
    pub hour: u8,
    pub minute: u8,
    pub second: u8,
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
        let month = parse::u8(&value[5..=6])?;
        let day = parse::u8(&value[8..=9])?;
        let hour = parse::u8(&value[11..=12])?;
        let minute = parse::u8(&value[14..=15])?;
        let second = parse::u8(&value[17..=18])?;
        let microseconds = parse::u32(&value[20..=26])?;  

        Some(MyDateTime { year, month, day, hour, minute, second, microseconds })
    }

    pub fn parse_noerrors(value: &[u8]) -> Option<MyDateTime> {
        if value.len() != 28 { return None; }

        let year = parse::u16_ne(&value[0..=3]);
        let month = parse::u8_2ne(&value[5..=6]);
        let day = parse::u8_2ne(&value[8..=9]);
        let hour = parse::u8_2ne(&value[11..=12]);
        let minute = parse::u8_2ne(&value[14..=15]);
        let second = parse::u8_2ne(&value[17..=18]);
        let microseconds = parse::u32_ne(&value[20..=26]);

        Some(MyDateTime { year: year, month, day, hour, minute, second, microseconds })
    }
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


// Vec::with_capacity(contents.len() / 29);  [No improvement]

// Not faster than DateTime::parse::from_rfc3339
// fn parse::nativedatetime_direct(value: &str) -> Result<NaiveDateTime, Box<dyn Error>> {
//     if value.len() != 28 { return Err(String::from("Value not 28 bytes long").into()); }

//     let year = value[0..=3].parse::<i32>()?;
//     let month = value[5..=6].parse::<u32>()?;
//     let day = value[8..=9].parse::<u32>()?;
//     let hour = value[11..=12].parse::<u32>()?;
//     let minute = value[14..=15].parse::<u32>()?;
//     let second = value[17..=18].parse::<u32>()?;
//     let microseconds = value[20..=26].parse::<u32>()?;

//     let date = NaiveDate::from_ymd_opt(year, month, day).ok_or("Invalid date")?;
//     let date_time = date.and_hms_micro_opt(hour, minute, second, microseconds).ok_or("Invalid time")?;

//     Ok(date_time)
// }