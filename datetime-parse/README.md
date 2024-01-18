# C# vs. Rust DateTimeParse Comparison

I originally wrote the C# version of this code to write a performance tuning [tutorial](https://relentlessoptimizer.com/code/2022/08/10/performance-tuning-from-1.5m-to-50m-datetimes-parsed-per-second-in-c/) for DateTime parsing. I came back after learning some Rust to try to compare the performance of the two languages.

The code parses a text file with 10M DateTimes in "2022-04-14T02:32:53.4028225Z" format.

## Usage
- Run C# version first to generate the sample data, datetime-parse/Sample.DatesOnly.log.

- Use 'dotnet run -c Release -f net8.0' (7.0 | 6.0) to run C# versions.
- Use 'cargo run -r' to run the Rust version.

You can extend the runtime and iteration limit in the Time() methods in each codebase to get more accurate numbers, but I chose relatively short runtimes because I iterated on the code many times and didn't want to wait minutes to see the results.

My C# and Rust versions both output Markdown-style tables with the results. I've given variations that are reasonably equivalent the same name in both languages. Others are logical steps from naive to optimized but not really directly comparable.

## Summary

It's hard to give a simple answer about whether Rust or C# was faster in this comparison.

If you try a naive implementation, Rust's chrono::DateTime::parse_from_rfc3339 was 2x faster than C#'s DateTime.ParseExact but 2x slower than C#'s DateTimeOffset.ParseExact, so your experience depends on whether you know about the newer DateTimeOffset type. It's possible that there is a faster naive Rust implementation, but I don't have much experience with Rust yet, so I don't know of any.

Near the end of my optimizations, Rust pulled distinctly ahead. Reading the file in blocks of bytes, looking for newlines, and parsing in custom code took 210 ms in Rust versus 300 ms in C#. Avoiding looking for newlines by splitting at a known length took 115 ms in Rust vs. 200 ms in in C#. However, my not-safe-for-production, fully unrolled, no error checking version came out very similar, at 95 ms for each.

My conclusion was that I'm likely to be able to get Rust to run distinctly faster in tuned, custom code. The built-in APIs I used in this example aren't as well tuned as C#'s newer types, which eliminated a lot of the benefit.

### Merged Results
M1 MacBook Pro, 16GB RAM, 512GB SSD, 8-core CPU, 14-core GPU
MacOS 14.2.1. Rust 1.74.0, .NET 8.0.0, .NET 7.0.14, .NET 6.0.25

| Variation                      | Rust  | NET 8 | NET 7 | NET 6 |
| ------------------------------ | ----- | ----- | ----- | ----- |
| DateTimeParse                  |       | 3,314 | 3,445 | 4,117 |
| DateTimeParseExact             |       | 2,087 | 2,768 | 3,462 |
| Rust Naive                     | 1,017 | 3,504 | 3,513 | 4,104 |
| Rust Naive ReadLine            | 1,316 |       |       |       |
| Rust String Iter, Custom Parse |   427 |       |       |       |
| Rust String, Custom Parse      |   401 |       |       |       |
| DateTimeParseExactNotUtc       |       | 1,098 | 1,352 | 1,535 |
| DateTimeOffsetParseExact       |       |   510 |   897 |   846 |
| SpanOfChar                     |       |   313 |   424 |   538 |
| Rust All Bytes, Custom Parse   |   297 |       |       |       |
| BytesAndCustomParse            |   206 |   300 |   328 |   500 |
| KnownLengthSplitAndCustomParse |       |   255 |   296 |   376 |
| Custom_MyParse                 |   115 |   200 |   490 |   462 |
| Custom_NoErrors                |    95 |    95 |   100 |   116 |

## Details

In normal performance tuning, I start with a correct, well known way to do something. I profile it to find the aspects of the work that are slowest, and experiment with alternative built-in implementation options to see what's fastest. After I've exhausted the available options, I look at custom implementations to avoid doing any work that doesn't absolutely have to be done to solve the problem. At the end, I'll often cut things that ARE required to understand the potential gains if I can figure out how to loosen the problem requirements.

In this comparison, I started out doing my normal tuning approach with both C# and Rust, but the optimization work leads in different directions - the straightforward implementations have different bottlenecks. I've tried to add some apples-to-apples implementation options here to get more direct comparisons between the languages.

### C#
In C#, the most well known method you could use is DateTime.Parse, but it's not fair to compare because it has to detect the format of each string and it has to convert the DateTime to local time. I've included it to show how much faster the next-best C# option is.

Since I'm working with a fixed format and I want the original UTC value, the reasonable starting point is DateTime.ParseExact. I was surprised that most of the runtime is time zone adjustment, even though the arguments I pass tell the function that the text to parse and the output I want will both be UTC. You can see this when I ask for a local DateTime instead and the speed doubles.

In trying variations, I then looked at the newer DateTimeOffset.ParseExact, and found it was 4x faster (**~500ms**) - clearly, it isn't doing any time zone work. It's very impressive that C# can convert the file to UTF-16, create a string instance per line, and do the parsing at this speed overall.

I measured just File.ReadLines (~250 ms) and File.ReadAllBytes (~50 ms). This shows me that File.ReadLines is fast enough for DateTime.ParseExact, but DateTimeOffset could be faster without creating a string per line (and even more so without converting to UTF-16).

My next implementation, Span, reads the file in blocks but maintains the UTF-16 conversion. I use Spans to avoid a string per line, and fortunately there's a DateTimeOffset.ParseExact overload for that. It runs in **310 ms**.

Next, let's try reading as bytes. I didn't see a DateTime parsing option for Span<byte>, so at this point I have to parse the numbers in the DateTime separately and construct it. Utf8Parser does provide a built-in parse. This gets down to **300 ms**, which is a smaller gain than I would've expected. It shows that the .NET UTF-8 to UTF-16 conversion is very fast.

Next, let's split at known length rather than newlines. This should get the I/O part to be as fast as my ReadAllBytes experiment. Taking out the newline searches gets the runtime to **250 ms**.

I then tried my own number parsing function. It's likely different from the Utf8Parser one because it tracks whether digits are out of range but doesn't stop the loop early if so, avoiding a conditional branch in the inner loop. Interestingly, this version is slower than the previous one in .NET 6.0 and 7.0, but faster in .NET 8.0 at **~200 ms**.

Finally, my last variant eliminates error handling, unrolls the digit loops to parse exactly a known number of digits for each part, and inlines the parsing function overall. I see **95 ms** for this version. I don't think this is safe for real use, but it demonstrates how much of our current runtime is spent on safety and a generic digit parsing loop.

C# has also gotten quite a bit faster between .NET 6.0 and 8.0. The DateTimeOffset version, for example, went from ~850 to ~510 ms. My second-to-last implementation was 460 ms in .NET 6.0 and is 200 ms in .NET 8.0. It's a huge improvement.

### Rust

In Rust, my naive version took about **1,000 ms** to get through the 10 M DateTimes. That's about twice as fast as DateTime.ParseExact and half as fast as DateTimeOffset.ParseExact. 

My first concern, though, is that Rust is reading the whole file at once, which I wouldn't want to do for a huge file. I tried using BufReader::lines to read by line, but it's 30% slower than the original. I'll have to figure out my own way to read the file in smaller parts.

I measured the DateTime parsing alone by parsing a constant 10M times, and it took **750 ms**. That means replacing the default parsing is the next step.

I next created my own parsing function, using str.parse<type>. This version is down to **410 ms**, a drastic improvement. I tried to create an iterator version to factor the code, but it's slower at 450 ms despite being the same code on both sides of the factoring boundary.

Next, I switch to reading as bytes and using a custom number parsing function, as I did in C#. This version runs in **300 ms**, another significant improvement.

I still need to switch from reading the whole file to reading in blocks, and now that I'm working in bytes that's easier. Swapping to blocks brings the runtime down to **210 ms**.

Next I split at known boundaries rather than looking for newlines (something the last three C# versions also did) and find that this version is drastically faster at **115 ms**. This is the last version I would reasonably use. It's nearly identical in terms of code and work done to the second-to-last C# implementation - read bytes in blocks, split at known lengths, parse with a custom function.

Finally, I write one comparable to the last C# implementation, with fully unrolled loops and no error checking. This version is **95 ms**.


Major Performance Components:
- UTF-8 to UTF-16 conversion (C# only)
- UTF-8 validation
- Finding newlines
- Creating a string (or slice) per line
- Allocating per line
- Identifying DateTime format or parsing format string
- Time Zone Correction

## TODO
- Make Dockerfiles to make it easy to run across platforms.
- Try on x64.
- Analyze assembly for the fastest versions to look for key differences.

## Current Results
M1 MacBook Pro, 16GB RAM, 512GB SSD, 8-core CPU, 14-core GPU
MacOS 14.2.1

|    ms | Rust 1.74.0                    | SumMillis  |
| ----- | ------------------------------ | ---------- |
|  1017 | Naive Rust                     | 4995071171 |
|  1316 | Naive Rust ReadLine            | 4995071171 |
|   427 | Rust String Iter, Custom Parse | 4995071171 |
|   401 | Rust String, Custom Parse      | 4995071171 |
|   297 | Rust All Bytes, Custom Parse   | 4995071171 |
|   206 | BytesAndCustomParse            | 4995071171 |
|   115 | Custom_MyParse                 | 4995071171 |
|    95 | Custom_NoErrors                | 4995071171 |


|    ms | .NET 8.0.0                     | SumMillis  |
| ----- | ------------------------------ | ---------- |
| 3,314 | DateTimeParse                  | 4995071171 |
| 2,087 | DateTimeParseExact             | 4995071171 |
| 3,504 | RustNaiveClosest               | 4995071171 |
| 1,098 | DateTimeParseExactNotUtc       | 4995071171 |
|   510 | DateTimeOffsetParseExact       | 4995071171 |
|   313 | SpanOfChar                     | 4995071171 |
|   300 | BytesAndCustomParse            | 4995071171 |
|   255 | KnownLengthSplitAndCustomParse | 4995071171 |
|   200 | Custom_MyParse                 | 4995071171 |
|    95 | Custom_NoErrors                | 4995071171 |


|    ms | .NET 7.0.14                    | SumMillis  |
| ----- | ------------------------------ | ---------- |
| 3,445 | DateTimeParse                  | 4995071171 |
| 2,768 | DateTimeParseExact             | 4995071171 |
| 3,513 | RustNaiveClosest               | 4995071171 |
| 1,352 | DateTimeParseExactNotUtc       | 4995071171 |
|   897 | DateTimeOffsetParseExact       | 4995071171 |
|   424 | SpanOfChar                     | 4995071171 |
|   328 | BytesAndCustomParse            | 4995071171 |
|   296 | KnownLengthSplitAndCustomParse | 4995071171 |
|   490 | Custom_MyParse                 | 4995071171 |
|   100 | Custom_NoErrors                | 4995071171 |


|    ms | .NET 6.0.25                    | SumMillis  |
| ----- | ------------------------------ | ---------- |
| 4,117 | DateTimeParse                  | 4995071171 |
| 3,462 | DateTimeParseExact             | 4995071171 |
| 4,104 | RustNaiveClosest               | 4995071171 |
| 1,535 | DateTimeParseExactNotUtc       | 4995071171 |
|   846 | DateTimeOffsetParseExact       | 4995071171 |
|   538 | SpanOfChar                     | 4995071171 |
|   500 | BytesAndCustomParse            | 4995071171 |
|   376 | KnownLengthSplitAndCustomParse | 4995071171 |
|   462 | Custom_MyParse                 | 4995071171 |
|   116 | Custom_NoErrors                | 4995071171 |

## Issues

In Rust, the most commonly shown way to read a file by line is creating a separate string per line. 
I think people tuning performance wouldn't do this - they would read blocks and pass string slices around.
However, I don't know of an easy built-in way to do that. Maybe I'm just not looking in the right places for docs.

