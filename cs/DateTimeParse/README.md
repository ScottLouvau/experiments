DateTime Parsing Performance sample.

## Results

Runtimes, in milliseconds, for DateTime parsing.
Apple M1 Pro 8cRelease build, 10M DateTimes, warm.

|    ms | .NET 6.0.25                    |
| ----- | ------------------------------ |
|   829 | Original                       |
|   531 | Span                           |
|   383 | Custom                         |
|   469 | Custom_MyParse                 |
|   118 | Custom_NoErrors                |
| 4,160 | Naive_RustNaiveClosest         |
| 4,448 | Naive_DateTimeParse            |
| 1,623 | Naive_ParseExactButNotUtc      |
| 3,418 | Naive_ParseExactUtcSlow        |
|   807 | Naive_DateTimeOffsetExact      |

|    ms | .NET 8.0.0                     |
| ----- | ------------------------------ |
|   519 | Original                       |
|   305 | Span                           |
|   315 | Custom                         |
|   199 | Custom_MyParse                 |
|    97 | Custom_NoErrors                |
| 3,716 | Naive_RustNaiveClosest         |
| 3,488 | Naive_DateTimeParse            |
| 1,058 | Naive_ParseExactButNotUtc      |
| 2,071 | Naive_ParseExactUtcSlow        |
|   449 | Naive_DateTimeOffsetExact      |