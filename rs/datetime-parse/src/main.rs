use std::{time::Instant, error::Error};
use datetime_parse::*;

fn time<T>(name: &str, f: impl Fn() -> Result<T, Box<dyn Error>>) -> Result<(), Box<dyn Error>> {
    let mut iterations = 0;
    let start = Instant::now();
    
    for _ in 0..10 {
        f()?;

        iterations += 1;
        if start.elapsed().as_millis() > 1500u128 { break; }
    }
    
    let duration: u128 = start.elapsed().as_millis() / iterations;
    println!("| {duration:>5} | {name:20} |");

    Ok(())
}

fn run_all() -> Result<(), Box<dyn Error>> {
    let file_path = "./Sample.DatesOnly.log";
    
    println!("|    ms | Name                 |");
    println!("| ----- | -------------------- |");

    time("original", || original(file_path))?;
    time("read_by_line", || read_by_line(file_path))?;
    time("span", || span(file_path))?;
    time("parse_custom", || parse_custom(file_path))?;
    time("blocks_parse_custom", || blocks_parse_custom(file_path))?;

    time("custom", || parse_custom(file_path))?;
    time("custom_noerrors", || parse_noerrors(file_path))?;

    Ok(())
}

fn main() {
    run_all().unwrap();
}