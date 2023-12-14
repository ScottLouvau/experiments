use std::{time::Instant, error::Error};
use datetime_parse::variations::*;

fn time<T>(name: &str, f: impl Fn() -> Result<T, Box<dyn Error>>) -> Result<(), Box<dyn Error>> {
    let mut iterations = 0;
    let start = Instant::now();
    
    for _ in 0..10 {
        f()?;

        iterations += 1;
        if start.elapsed().as_millis() > 1500u128 { break; }
    }
    
    let duration: u128 = start.elapsed().as_millis() / iterations;
    println!("| {duration:>5} | {name:30} |");

    Ok(())
}

fn run_all() -> Result<(), Box<dyn Error>> {
    let file_path = "./Sample.DatesOnly.log";
    
    println!("|    ms | Rust                           |");
    println!("| ----- | ------------------------------ |");

    
    time("Original", || original(file_path))?;
    time("span", || span(file_path))?;
    // time("parse_custom", || parse_custom(file_path))?;
    // time("blocks_parse_custom", || blocks_parse_custom(file_path))?;

    time("Custom", || custom(file_path))?;
    time("Custom NoErrors", || custom_noerrors(file_path))?;

    time("Naive Rust", || naive_rust(file_path))?;
    time("Naive ReadLine", || naive_readline(file_path))?;

    Ok(())
}

fn main() {
    run_all().unwrap();
}