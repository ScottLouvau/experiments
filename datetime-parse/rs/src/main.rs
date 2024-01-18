use std::{time::Instant, error::Error, io::Write, fs};
use chrono::{DateTime, FixedOffset};
use datetime_parse::variations::*;
use rustc_version_runtime::version;

const DATETIMES_PATH: &str = "../Sample.DatesOnly.log";
const LOG_TO_PATH: &str = "./Rust.log";

fn time<T>(name: &str, parse: impl Fn() -> Result<T, Box<dyn Error>>, check: impl Fn(T) -> u64) -> Result<(), Box<dyn Error>> {
    let mut iterations = 0;
    let start = Instant::now();

    let mut result = parse()?;
    iterations += 1;
    
    for _ in 1..10 {
        if start.elapsed().as_millis() > 1500u128 { break; }

        result = parse()?;
        iterations += 1;
    }
    
    let duration: u128 = start.elapsed().as_millis() / iterations;
    let check_millis = check(result);

    write_line(&format!("| {name:30} | {duration:>5} | {check_millis:>10} |"));

    Ok(())
}

fn sum_datetime(dates: Vec<DateTime<FixedOffset>>) -> u64 {
    let mut sum = 0u64;
    for date in dates {
        sum += date.timestamp_subsec_millis() as u64;
    }
    sum
}

fn sum_custom(dates: Vec<MyDateTime>) -> u64 {
    let mut sum = 0u64;
    for date in dates {
        sum += (date.nanoseconds / 1000000) as u64;
    }
    sum
}

fn write_line(value: &str) {
    println!("{}", value);

    let mut file = std::fs::OpenOptions::new()
        .create(true)
        .append(true)
        .open(LOG_TO_PATH)
        .unwrap();

    write!(file, "{}\n", value).unwrap();
}

fn run_all() -> Result<(), Box<dyn Error>> {
    // Read the input file once to get 'warm' read times
    let _ = fs::read_to_string(DATETIMES_PATH)?;

    // Delete the timings log if it already exists
    if fs::metadata(LOG_TO_PATH).is_ok() {
        fs::remove_file(LOG_TO_PATH)?; 
    }

    write_line("");
    write_line(&format!("| Rust {:25} |    ms | SumMillis  |", version()));
    write_line("| ------------------------------ | ----- | ---------- |");

    time("Rust Naive", || naive_rust(DATETIMES_PATH), sum_datetime)?;
    time("Rust Naive ReadLine", || naive_readline(DATETIMES_PATH), sum_datetime)?;
    time("Rust String Iter, Custom Parse", || string_iterator_custom_parse(DATETIMES_PATH), sum_custom)?;
    time("Rust String, Custom Parse", || string_custom_parse(DATETIMES_PATH), sum_custom)?;
    time("Rust All Bytes, Custom Parse", || bytes_custom_parse(DATETIMES_PATH), sum_custom)?;
    time("BytesAndCustomParse", || blocks_custom_parse(DATETIMES_PATH), sum_custom)?;
    time("Custom_MyParse", || known_length_custom(DATETIMES_PATH), sum_custom)?;
    time("Custom_NoErrors", || custom_noerrors(DATETIMES_PATH), sum_custom)?;

    Ok(())
}

fn main() {
    run_all().unwrap();
}