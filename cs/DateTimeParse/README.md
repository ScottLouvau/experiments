DateTime Parsing Performance sample.

## Usage

```
./build (or "dotnet publish -c Release -a x64")
./DateTimeParse write to write sample data files.
./DateTimeParse all to measure all implementations.
```

## Results

Runtimes, in milliseconds, for DateTime parsing.
NET 6.0, Release build, 10M DateTimes, warm.

| Name              | Ryzen 4800U | Ryzen 4900HS | M1 Pro 6+2 |
| ----------------- | -----------:| ------------:| ----------:|
| Original          | 6,901       | 5,954        | 4,365      |
| AdjustToUniversal | 3,704       | 3,132        | 1,934      |
| ParseExact        | 2,866       | 2,527        | 1,607      |
| Offset            | 1,492       | 1,343        | 793        |
| Span              | 878         | 837          | 453        |
| Span_ParseInParts | 1,080       | 960          | 711        |
| SpanByte          | 592         | 557          | 401        |
| SpanByte_MyParse  | 485         | 435          | 465        |
| SpanByte_Unrolled | 182         | 160          | 113        |
| BinaryTicks       | 261         | 242          | 144        |
| BinaryBulk        | 33          | 34           | 11         |