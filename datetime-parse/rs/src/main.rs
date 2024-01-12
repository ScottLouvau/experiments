use std::{time::Instant, error::Error};
use chrono::{DateTime, FixedOffset};
use datetime_parse::variations::*;

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

    println!("| {duration:>5} | {name:30} | {check_millis:>10} |");

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

fn run_all() -> Result<(), Box<dyn Error>> {
    let file_path = "../Sample.DatesOnly.log";
    
    println!("|    ms | Rust                           | SumMillis  |");
    println!("| ----- | ------------------------------ | ---------- |");

    time("Naive Rust", || naive_rust(file_path), sum_datetime)?;
    time("Naive ReadLine", || naive_readline(file_path), sum_datetime)?;
    time("String Iterator, Custom Parse", || string_iterator_custom_parse(file_path), sum_custom)?;
    time("String, Custom Parse", || string_custom_parse(file_path), sum_custom)?;
    time("Bytes, Custom Parse", || bytes_custom_parse(file_path), sum_custom)?;
    time("Block Read, Custom Parse", || blocks_custom_parse(file_path), sum_custom)?;
    time("Split at Length, Custom Parse", || known_length_custom(file_path), sum_custom)?;
    time("Split at Length, No Validation", || custom_noerrors(file_path), sum_custom)?;

    Ok(())
}

fn main() {
    run_all().unwrap();
}