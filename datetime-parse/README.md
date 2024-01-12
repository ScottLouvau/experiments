# C# vs. Rust DateTimeParse Comparison

I originally wrote the C# version of this code to write a performance tuning [tutorial](https://relentlessoptimizer.com/code/2022/08/10/performance-tuning-from-1.5m-to-50m-datetimes-parsed-per-second-in-c/) for DateTime parsing. I came back after learning some Rust to try to compare the performance of the two languages.

The code parses a text file with 10M DateTimes in "2022-04-14T02:32:53.4028225Z" format.

## Usage
- Run C# version first to generate the sample data, datetime-parse/Sample.DatesOnly.log.

- Use 'dotnet run -c Release -f net8.0' (7.0 | 6.0) to run C# versions.
- Use 'cargo run -r' to run the Rust version.

## Summary
It's not an ideal comparison, because naive C# and Rust implementations end up doing different intermediate work. 

- My original naive Rust code was 3.3x faster than C# DateTime.ParseExact, but 2.0x slower than DateTimeOffset.ParseExact. 
- My fastest tuned implementations are about the same between Rust and C#, but aren't realistic.
- The fastest implementations I'd use in production are 1.5x ('span') or 2.3x ('custom') faster in Rust.

## Discussion

In C#, the most well known method you could use is DateTime.Parse, but it's not fair to compare because it has to detect the format of each string and it has to convert the DateTime to local time.

Since I'm working with a fixed format and I want the original UTC value, I started with DateTime.ParseExact. I was surprised that most of the runtime is time zone adjustment, even though the arguments I pass tell the function that the text to parse and the output I want will both be UTC. You can see this when I ask for a local DateTime instead and the speed doubles.

In the newer DateTimeOffset.ParseExact, that issue seems to have been fixed. It's 4x faster than the DateTime.ParseExact version. It's very impressive that C# can convert the file to UTF-16, create a string instance per line, and do the parsing at this speed overall.

C# has also gotten quite a bit faster between .NET 6.0, 7.0, and 8.0. The DateTimeOffset version, for example, went from 833 to 785 to 515 ms across runtime versions. There is a similar magnitude of speedup in all but the fastest implementation I wrote.

<New Rust: Custom Parse first.>

In Rust, the main problem with my naive versions is that they read the file as a single block, which I would not want to do with a huge file. The naive version to read by line (BufReader::lines) is 30% slower than that. There may be a well known read-by-line API in Rust that isn't slower than reading the file whole, but I didn't find it when searching online.

After considering alternative built-ins for reading the file and parsing the DateTimes, the best next steps diverge. In C#, using Span to avoid creating a string per line is the biggest improvement, taking about 40% off the runtime. In Rust, the best next step is to write my own DateTime.Parse function hardcoded to handle the format.

If I create my own Rust code to iterate over file lines by reading bytes into a buffer, finding newlines, and then returning array slices, the 

Rust's chrono::DateTime::parse_from_rfc3339 is less optimized than C#'s DateTimeOffset.ParseExact, taking ~750ms to parse a constant 10M times. 

In the 'Custom' implementations in each language, I read blocks of bytes, split at a known length, and parse 



Once I start getting to custom implementations to avoid wasted work - reading the file in blocks of bytes and parsing in my own code to work with the slices - Rust is again a 



Major Performance Components:
- UTF-8 to UTF-16 conversion (C# only)
- UTF-8 validation
- Finding newlines
- Allocating per line
- Identifying DateTime format or parsing format string
- Time Zone Correction


## TODO
- Make Dockerfiles to make it easy to run across platforms.

## Current Results
M1 MacBook Pro, 16GB RAM, 512GB SSD, 8-core CPU, 14-core GPU; MacOS 14.2.1

|    ms | Rust 1.74.0                    | SumMillis  |
| ----- | ------------------------------ | ---------- |
|  1029 | Naive Rust                     | 4995071171 |
|  1359 | Naive ReadLine                 | 4995071171 |
|  1031 | Original                       | 4995071171 |
|   209 | span                           | 4995071171 |
|   116 | Custom                         | 4995071171 |
|   102 | Custom NoErrors                | 4995071171 |


|    ms | .NET 8.0.0                     | SumMillis  |
| ----- | ------------------------------ | ---------- |
| 3,455 | My_Real_First                  | 4995071171 |
| 2,043 | Naive_CS                       | 4995071171 |
| 3,613 | Naive_RustNaiveClosest         | 4995071171 |
| 1,092 | Naive_ParseExactButNotUtc      | 4995071171 |
|   515 | Naive_DateTimeOffset           | 4995071171 |
|   314 | Span                           | 4995071171 |
|   268 | Custom                         | 4995071171 |
|   202 | Custom_MyParse                 | 4995071171 |
|    97 | Custom_NoErrors                | 4995071171 |


|    ms | .NET 7.0.14                    | SumMillis  |
| ----- | ------------------------------ | ---------- |
| 3,507 | My_Real_First                  | 4995071171 |
| 2,797 | Naive_CS                       | 4995071171 |
| 3,594 | Naive_RustNaiveClosest         | 4995071171 |
| 1,299 | Naive_ParseExactButNotUtc      | 4995071171 |
|   785 | Naive_DateTimeOffset           | 4995071171 |
|   427 | Span                           | 4995071171 |
|   301 | Custom                         | 4995071171 |
|   502 | Custom_MyParse                 | 4995071171 |
|   101 | Custom_NoErrors                | 4995071171 |


|    ms | .NET 6.0.25                    | SumMillis  |
| ----- | ------------------------------ | ---------- |
| 4,133 | My_Real_First                  | 4995071171 |
| 3,477 | Naive_CS                       | 4995071171 |
| 4,150 | Naive_RustNaiveClosest         | 4995071171 |
| 1,608 | Naive_ParseExactButNotUtc      | 4995071171 |
|   833 | Naive_DateTimeOffset           | 4995071171 |
|   536 | Span                           | 4995071171 |
|   386 | Custom                         | 4995071171 |
|   464 | Custom_MyParse                 | 4995071171 |
|   116 | Custom_NoErrors                | 4995071171 |

## Issues

In Rust, the most commonly shown way to read a file by line is creating a separate string per line. 
I think people tuning performance wouldn't do this - they would read blocks and pass string slices around.
However, I don't know of an easy built-in way to do that. Maybe I'm just not looking in the right places for docs.

