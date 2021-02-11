# C# Copy Performance

I've recently been working on some decompression code (for an LZ4-like algorithm) and was surprised by relatively slow decompression speeds from my code. The logic is really very simple - copy some new bytes from here, then copy some repeated bytes from earlier. This made me wonder whether C# can achieve the same data copy speeds as tuned C++ code and benchmarks do, and, if so, what code to use to do the copying. 

So, I coded up a bunch of different implementations of byte copying, set up my measurement infrastructure, and got to work... 



### Benchmarks

The AIDA64 benchmark reports the memory copy bandwidth of my laptop, an ASUS ROG Zephyrus G14, at about [**39.6 GB/s**](https://rog.asus.com/us/articles/reviews/rog-zephyrus-g14-lab-report-2). Importantly, AIDA64 counts **[the sum](https://forums.aida64.com/topic/3708-memory-bench-questions/)** of the input and output size in this measurement, so this really means just under 20 GB of data could be copied from one place in memory to another in one second. In my numbers below, I'll only could the amount of data copied once, as this makes more sense to me. =)

SiSoft Sandra reports memory bandwidth during assignment at about **35.8 GB/s**, also counting both the source and destination size to estimate overall memory bandwidth.

I suspect that the difference between these numbers is due to AIDA64 reporting the peak speeds seen during a short period, while Sandra is reporting an average over the whole benchmark. 



### Setup

For this demo, I've built a minimal C# console app which runs a variety of copy method implementations for a given measurement time. I spend five seconds for each implementation and thread count copying 64 MB from one byte[] to another. The code runs the copy in a tight for loop, measuring the elapsed time to figure out how many iterations to run between each clock time check. This is similar to the way Benchmark.NET runs. You can clone [the code](https://github.com/ScottLouvau/experiments) quickly to try it on your hardware.

I'm testing on a 2020 ASUS ROG Zephyrus G14 laptop with a Ryzen 4900HS CPU and 16 GB of DDR4-3200 RAM, and a 2015 home-built desktop with an i7-7500 and 32 GB of DDR4-2400. I decided to run the test with the current .NET 5.0, .NET Core 3.1, 2.1, and 1.1 runtimes available as of February 2021. All runs are on Release bits, the laptop is plugged in, and I'm running in the highest performance power and cooling mode.



### Results

On .NET 5.0 or .NET Core 3.1, all of the built-in copy methods perform similarly (Array.Copy, Buffer.BlockCopy, Buffer.MemoryCopy, AsSpan.Copy, and Unsafe.CopyBlock). These methods all achieved about 12.7 GB/s on one thread and just under 19.0 GB/s using all eight cores. If I run these methods for only one second, rather than five, I sometimes get 20.0 GB/s, matching AIDA64. I suspect the AIDA benchmark uses the peak speed it sees during a multi-second test.

On the older .NET Core 2.1 and 1.1 runtimes, I got the most consistent performance from Array.Copy. You may want to prefer that method in code which may be run under older runtimes.

Hand-coded loops were slower. A for loop over the byte array is much slower, at 2.0 GB/s. Copying eight bytes at a time by using Unsafe.As to interpret the byte array as a long array quadruples the speed to about 7.8 GB/s. 

The hand coded loops also have a lower speed limit when multiple threads are used, topping out at a little under 11 GB/s. This is because the built-ins use "non-temporal stores", which tell the CPU to write the results directly to memory rather than writing them to the CPU caches first. This avoids the cost of an extra read of the data about to be written to get the rest of the cache line properly initialized. If your code is writing much less than the cache size and will immediately use the written value, non-temporal stores aren't helpful. However, if you are doing a large copy and won't be using the first bytes until you're done copying the last ones, it's a significant benefit.

[Non-Temporal long]

[AVX]



### Measurements 

#### ASUS ROG Zephyrus G14: Laptop, AMD Ryzen 4900 HS, 16 GB DDR4-3200

Running Copy Performance Tests [.NET 5.0.2 on ZEPHYRUS]...

| Method                               | Speed [1T] | Speed [2T] | Speed [4T] | Speed [8T] | Speed [16T] |
| ------------------------------------ | ---------: | ---------: | ---------: | ---------: | ----------: |
| ArrayCopy                            |  12.7 GB/s |  16.9 GB/s |  18.2 GB/s |  18.9 GB/s |   18.0 GB/s |
| BufferBlockCopy                      |  12.7 GB/s |  16.9 GB/s |  18.2 GB/s |  18.9 GB/s |   18.0 GB/s |
| AsSpanCopy                           |  12.7 GB/s |  16.9 GB/s |  18.2 GB/s |  18.6 GB/s |   18.0 GB/s |
| UnsafeCopyBlock                      |  12.7 GB/s |  17.0 GB/s |  18.1 GB/s |  19.1 GB/s |   18.0 GB/s |
| UnsafeCopyBlockUnaligned             |  12.7 GB/s |  17.0 GB/s |  18.1 GB/s |  19.0 GB/s |   18.0 GB/s |
| ForByte                              |  1.95 GB/s |  3.79 GB/s |  7.04 GB/s |  10.3 GB/s |   9.59 GB/s |
| ForUnsafeAsLong                      |  7.83 GB/s |  10.3 GB/s |  10.5 GB/s |  10.5 GB/s |   9.70 GB/s |
| BufferMemoryCopy                     |  12.7 GB/s |  17.0 GB/s |  18.2 GB/s |  19.0 GB/s |   18.0 GB/s |
| Memcpy                               |  11.5 GB/s |  16.3 GB/s |  18.0 GB/s |  19.0 GB/s |   18.2 GB/s |
| UnsafeForLong                        |  7.43 GB/s |  9.89 GB/s |  10.5 GB/s |  10.5 GB/s |   9.73 GB/s |
| UnsafeWhileLong                      |  7.41 GB/s |  10.1 GB/s |  10.4 GB/s |  10.5 GB/s |   9.76 GB/s |
| Avx128_LoadStore                     |  8.81 GB/s |  10.6 GB/s |  10.8 GB/s |  10.5 GB/s |   9.64 GB/s |
| Avx256_LoadStore                     |  8.73 GB/s |  10.6 GB/s |  10.5 GB/s |  10.5 GB/s |   9.66 GB/s |
| StoreNonTemporalInt                  |  9.01 GB/s |  13.8 GB/s |  17.3 GB/s |  18.1 GB/s |   17.4 GB/s |
| StoreNonTemporalLong                 |  10.8 GB/s |  15.4 GB/s |  17.8 GB/s |  18.5 GB/s |   17.5 GB/s |
| StoreNonTemporalLong_Unrolled2       |  11.0 GB/s |  15.3 GB/s |  17.8 GB/s |  18.4 GB/s |   17.5 GB/s |
| StoreNonTemporalLong_Unrolled4       |  11.0 GB/s |  15.3 GB/s |  17.8 GB/s |  18.4 GB/s |   17.6 GB/s |
| Avx128_StoreNonTemporal              |  7.19 GB/s |  12.8 GB/s |  17.6 GB/s |  18.2 GB/s |   17.5 GB/s |
| Avx128_StoreNonTemporal_Unrolled2    |  9.65 GB/s |  14.9 GB/s |  17.7 GB/s |  18.4 GB/s |   17.6 GB/s |
| Avx128_StoreNonTemporal_Unrolled2v2  |  9.90 GB/s |  14.7 GB/s |  17.8 GB/s |  18.3 GB/s |   17.4 GB/s |
| Avx128_StoreNonTemporal_Unrolled4v2  |  10.7 GB/s |  15.1 GB/s |  17.8 GB/s |  18.4 GB/s |   17.6 GB/s |
| Avx256_StoreNonTemporal              |  10.3 GB/s |  14.1 GB/s |  17.0 GB/s |  18.4 GB/s |   17.5 GB/s |
| Avx256_StoreNonTemporal_Unrolled2v2  |  11.0 GB/s |  15.2 GB/s |  17.6 GB/s |  18.4 GB/s |   17.6 GB/s |
| Avx256_StoreNonTemporal_Unrolled4v2  |  11.3 GB/s |  15.3 GB/s |  17.7 GB/s |  18.3 GB/s |   17.5 GB/s |
| Avx256_StoreNonTemporal_Unrolled8v2  |  12.5 GB/s |  16.6 GB/s |  17.7 GB/s |  18.4 GB/s |   17.6 GB/s |
| Avx256_StoreNonTemporal_Unrolled16v2 |  12.5 GB/s |  16.8 GB/s |  17.7 GB/s |  18.4 GB/s |   17.6 GB/s |


Running Copy Performance Tests [.NET Core 3.1.11 on ZEPHYRUS]...

| Method                               | Speed [1T] | Speed [2T] | Speed [4T] | Speed [8T] | Speed [16T] |
| ------------------------------------ | ---------: | ---------: | ---------: | ---------: | ----------: |
| ArrayCopy                            |  12.7 GB/s |  16.9 GB/s |  18.1 GB/s |  18.9 GB/s |   18.0 GB/s |
| BufferBlockCopy                      |  12.7 GB/s |  16.9 GB/s |  18.2 GB/s |  18.9 GB/s |   18.0 GB/s |
| AsSpanCopy                           |  12.7 GB/s |  16.9 GB/s |  18.2 GB/s |  18.9 GB/s |   18.0 GB/s |
| UnsafeCopyBlock                      |  12.7 GB/s |  17.0 GB/s |  18.1 GB/s |  19.0 GB/s |   18.0 GB/s |
| UnsafeCopyBlockUnaligned             |  12.7 GB/s |  17.0 GB/s |  18.2 GB/s |  19.0 GB/s |   18.0 GB/s |
| ForByte                              |  2.00 GB/s |  3.92 GB/s |  7.39 GB/s |  9.99 GB/s |   9.59 GB/s |
| ForUnsafeAsLong                      |  7.86 GB/s |  10.1 GB/s |  10.5 GB/s |  10.5 GB/s |   9.65 GB/s |
| BufferMemoryCopy                     |  12.7 GB/s |  16.9 GB/s |  18.2 GB/s |  18.9 GB/s |   18.0 GB/s |
| Memcpy                               |  11.5 GB/s |  16.8 GB/s |  18.0 GB/s |  18.9 GB/s |   18.2 GB/s |
| UnsafeForLong                        |  7.92 GB/s |  10.3 GB/s |  10.5 GB/s |  10.5 GB/s |   9.69 GB/s |
| UnsafeWhileLong                      |  7.96 GB/s |  10.4 GB/s |  10.5 GB/s |  10.5 GB/s |   9.70 GB/s |
| Avx128_LoadStore                     |  8.75 GB/s |  10.6 GB/s |  10.5 GB/s |  10.5 GB/s |   9.65 GB/s |
| Avx256_LoadStore                     |  8.92 GB/s |  10.7 GB/s |  10.6 GB/s |  10.5 GB/s |   9.67 GB/s |
| StoreNonTemporalInt                  |  9.17 GB/s |  13.7 GB/s |  17.2 GB/s |  17.9 GB/s |   17.4 GB/s |
| StoreNonTemporalLong                 |  10.8 GB/s |  15.2 GB/s |  17.8 GB/s |  18.4 GB/s |   17.5 GB/s |
| StoreNonTemporalLong_Unrolled2       |  11.0 GB/s |  15.2 GB/s |  17.8 GB/s |  18.4 GB/s |   17.6 GB/s |
| StoreNonTemporalLong_Unrolled4       |  10.9 GB/s |  15.2 GB/s |  17.7 GB/s |  18.4 GB/s |   17.5 GB/s |
| Avx128_StoreNonTemporal              |  7.02 GB/s |  13.0 GB/s |  17.1 GB/s |  18.1 GB/s |   17.5 GB/s |
| Avx128_StoreNonTemporal_Unrolled2    |  9.50 GB/s |  14.8 GB/s |  17.7 GB/s |  18.3 GB/s |   17.5 GB/s |
| Avx128_StoreNonTemporal_Unrolled2v2  |  9.53 GB/s |  13.3 GB/s |  17.0 GB/s |  18.3 GB/s |   17.4 GB/s |
| Avx128_StoreNonTemporal_Unrolled4v2  |  10.7 GB/s |  14.9 GB/s |  17.7 GB/s |  18.4 GB/s |   17.5 GB/s |
| Avx256_StoreNonTemporal              |  10.3 GB/s |  15.2 GB/s |  17.6 GB/s |  18.3 GB/s |   17.5 GB/s |
| Avx256_StoreNonTemporal_Unrolled2v2  |  11.1 GB/s |  15.6 GB/s |  17.7 GB/s |  18.4 GB/s |   17.6 GB/s |
| Avx256_StoreNonTemporal_Unrolled4v2  |  11.2 GB/s |  15.9 GB/s |  17.7 GB/s |  18.3 GB/s |   17.6 GB/s |
| Avx256_StoreNonTemporal_Unrolled8v2  |  12.5 GB/s |  16.7 GB/s |  17.5 GB/s |  18.3 GB/s |   17.5 GB/s |
| Avx256_StoreNonTemporal_Unrolled16v2 |  12.4 GB/s |  16.7 GB/s |  17.7 GB/s |  18.3 GB/s |   17.5 GB/s |

Running Copy Performance Tests [.NET Core 4.6.29518.01 (2.1) on ZEPHYRUS]...

| Method                   | Speed [1T] | Speed [2T] | Speed [4T] | Speed [8T] | Speed [16T] |
| ------------------------ | ---------: | ---------: | ---------: | ---------: | ----------: |
| ArrayCopy                |  12.7 GB/s |  17.1 GB/s |  18.2 GB/s |  19.0 GB/s |   18.0 GB/s |
| BufferBlockCopy          |  6.93 GB/s |  9.92 GB/s |  11.2 GB/s |  11.0 GB/s |   10.5 GB/s |
| UnsafeCopyBlock          |  6.96 GB/s |  9.89 GB/s |  11.3 GB/s |  11.1 GB/s |   10.5 GB/s |
| UnsafeCopyBlockUnaligned |  6.95 GB/s |  9.92 GB/s |  11.3 GB/s |  11.1 GB/s |   10.5 GB/s |
| ForByte                  |  1.99 GB/s |  3.94 GB/s |  7.45 GB/s |  10.2 GB/s |   9.16 GB/s |
| ForUnsafeAsLong          |  7.72 GB/s |  10.0 GB/s |  10.6 GB/s |  10.5 GB/s |   9.70 GB/s |
| BufferMemoryCopy         |  12.7 GB/s |  17.0 GB/s |  18.2 GB/s |  19.0 GB/s |   18.0 GB/s |
| Memcpy                   |  11.8 GB/s |  16.7 GB/s |  18.1 GB/s |  19.0 GB/s |   18.1 GB/s |
| UnsafeForLong            |  7.88 GB/s |  10.3 GB/s |  10.4 GB/s |  10.5 GB/s |   9.71 GB/s |
| UnsafeWhileLong          |  7.85 GB/s |  10.3 GB/s |  10.3 GB/s |  10.5 GB/s |   9.72 GB/s |

Running Copy Performance Tests [.NET Core 4.6.27618.02 (1.1) on ZEPHYRUS]...

| Method                   | Speed [1T] | Speed [2T] | Speed [4T] | Speed [8T] | Speed [16T] |
| ------------------------ | ---------: | ---------: | ---------: | ---------: | ----------: |
| ArrayCopy                |  12.7 GB/s |  17.0 GB/s |  17.7 GB/s |  18.7 GB/s |   17.8 GB/s |
| BufferBlockCopy          |  12.7 GB/s |  17.1 GB/s |  18.2 GB/s |  18.7 GB/s |   17.8 GB/s |
| UnsafeCopyBlock          |  10.9 GB/s |  15.2 GB/s |  17.4 GB/s |  18.1 GB/s |   17.7 GB/s |
| UnsafeCopyBlockUnaligned |  11.0 GB/s |  15.2 GB/s |  17.3 GB/s |  18.1 GB/s |   17.7 GB/s |
| ForByte                  |  2.00 GB/s |  3.98 GB/s |  7.18 GB/s |  9.83 GB/s |   9.64 GB/s |
| ForUnsafeAsLong          |  7.81 GB/s |  10.2 GB/s |  10.6 GB/s |  10.5 GB/s |   9.69 GB/s |
| BufferMemoryCopy         |  12.7 GB/s |  17.1 GB/s |  18.2 GB/s |  18.6 GB/s |   17.9 GB/s |
| Memcpy                   |  11.8 GB/s |  16.5 GB/s |  18.0 GB/s |  18.8 GB/s |   18.0 GB/s |
| UnsafeForLong            |  7.94 GB/s |  10.2 GB/s |  10.5 GB/s |  10.5 GB/s |   9.74 GB/s |
| UnsafeWhileLong          |  7.85 GB/s |  10.3 GB/s |  10.5 GB/s |  10.5 GB/s |   9.56 GB/s |

##### 

#### "Beast": Desktop, Intel i7-7500, 32 GB DDR4-2400

Running Copy Performance Tests [.NET 5.0.2 on BEAST]...

| Method                               | Speed [1T] | Speed [2T] | Speed [4T] |
| ------------------------------------ | ---------: | ---------: | ---------: |
| ArrayCopy                            |  9.45 GB/s |  11.5 GB/s |  12.0 GB/s |
| BufferBlockCopy                      |  9.51 GB/s |  11.6 GB/s |  12.0 GB/s |
| AsSpanCopy                           |  9.47 GB/s |  11.6 GB/s |  12.0 GB/s |
| UnsafeCopyBlock                      |  9.68 GB/s |  11.8 GB/s |  12.0 GB/s |
| UnsafeCopyBlockUnaligned             |  9.71 GB/s |  11.8 GB/s |  12.0 GB/s |
| ForByte                              |  1.70 GB/s |  3.41 GB/s |  6.69 GB/s |
| ForUnsafeAsLong                      |  8.63 GB/s |  10.4 GB/s |  9.79 GB/s |
| BufferMemoryCopy                     |  9.67 GB/s |  11.9 GB/s |  12.1 GB/s |
| Memcpy                               |  14.1 GB/s |  14.6 GB/s |  13.3 GB/s |
| UnsafeForLong                        |  7.45 GB/s |  10.3 GB/s |  9.76 GB/s |
| UnsafeWhileLong                      |  9.02 GB/s |  10.4 GB/s |  9.84 GB/s |
| Avx128_LoadStore                     |  9.91 GB/s |  10.7 GB/s |  9.98 GB/s |
| Avx256_LoadStore                     |  10.2 GB/s |  10.6 GB/s |  10.0 GB/s |
| StoreNonTemporalInt                  |  8.65 GB/s |  14.1 GB/s |  14.4 GB/s |
| StoreNonTemporalLong                 |  13.0 GB/s |  15.0 GB/s |  14.2 GB/s |
| StoreNonTemporalLong_Unrolled2       |  13.6 GB/s |  14.8 GB/s |  13.9 GB/s |
| StoreNonTemporalLong_Unrolled4       |  13.7 GB/s |  15.1 GB/s |  14.1 GB/s |
| Avx128_StoreNonTemporal              |  6.91 GB/s |  13.6 GB/s |  14.5 GB/s |
| Avx128_StoreNonTemporal_Unrolled2    |  10.8 GB/s |  15.2 GB/s |  14.4 GB/s |
| Avx128_StoreNonTemporal_Unrolled2v2  |  10.7 GB/s |  15.2 GB/s |  14.4 GB/s |
| Avx128_StoreNonTemporal_Unrolled4v2  |  12.6 GB/s |  15.2 GB/s |  14.4 GB/s |
| Avx256_StoreNonTemporal              |  12.4 GB/s |  15.3 GB/s |  14.3 GB/s |
| Avx256_StoreNonTemporal_Unrolled2v2  |  14.5 GB/s |  15.3 GB/s |  14.3 GB/s |
| Avx256_StoreNonTemporal_Unrolled4v2  |  14.7 GB/s |  15.3 GB/s |  14.3 GB/s |
| Avx256_StoreNonTemporal_Unrolled8v2  |  14.4 GB/s |  15.0 GB/s |  14.0 GB/s |
| Avx256_StoreNonTemporal_Unrolled16v2 |  14.4 GB/s |  15.0 GB/s |  14.0 GB/s |

Running Copy Performance Tests [.NET Core 3.1.11 on BEAST]...

| Method                               | Speed [1T] | Speed [2T] | Speed [4T] |
| ------------------------------------ | ---------: | ---------: | ---------: |
| ArrayCopy                            |  9.59 GB/s |  11.5 GB/s |  12.0 GB/s |
| BufferBlockCopy                      |  9.60 GB/s |  11.6 GB/s |  11.9 GB/s |
| AsSpanCopy                           |  9.59 GB/s |  11.6 GB/s |  12.0 GB/s |
| UnsafeCopyBlock                      |  9.72 GB/s |  11.8 GB/s |  12.1 GB/s |
| UnsafeCopyBlockUnaligned             |  9.70 GB/s |  11.8 GB/s |  12.1 GB/s |
| ForByte                              |  1.93 GB/s |  3.81 GB/s |  7.42 GB/s |
| ForUnsafeAsLong                      |  8.95 GB/s |  10.4 GB/s |  9.82 GB/s |
| BufferMemoryCopy                     |  9.78 GB/s |  11.9 GB/s |  12.2 GB/s |
| Memcpy                               |  14.1 GB/s |  14.6 GB/s |  13.3 GB/s |
| UnsafeForLong                        |  8.69 GB/s |  10.4 GB/s |  9.79 GB/s |
| UnsafeWhileLong                      |  8.20 GB/s |  10.4 GB/s |  9.82 GB/s |
| Avx128_LoadStore                     |  9.98 GB/s |  10.7 GB/s |  10.0 GB/s |
| Avx256_LoadStore                     |  10.4 GB/s |  10.7 GB/s |  10.1 GB/s |
| StoreNonTemporalInt                  |  8.66 GB/s |  14.2 GB/s |  14.5 GB/s |
| StoreNonTemporalLong                 |  13.1 GB/s |  15.2 GB/s |  14.3 GB/s |
| StoreNonTemporalLong_Unrolled2       |  13.8 GB/s |  14.9 GB/s |  14.1 GB/s |
| StoreNonTemporalLong_Unrolled4       |  13.8 GB/s |  14.7 GB/s |  14.1 GB/s |
| Avx128_StoreNonTemporal              |  6.89 GB/s |  13.4 GB/s |  13.4 GB/s |
| Avx128_StoreNonTemporal_Unrolled2    |  10.0 GB/s |  14.4 GB/s |  14.4 GB/s |
| Avx128_StoreNonTemporal_Unrolled2v2  |  10.6 GB/s |  15.1 GB/s |  14.4 GB/s |
| Avx128_StoreNonTemporal_Unrolled4v2  |  12.6 GB/s |  15.2 GB/s |  14.3 GB/s |
| Avx256_StoreNonTemporal              |  12.4 GB/s |  15.2 GB/s |  14.3 GB/s |
| Avx256_StoreNonTemporal_Unrolled2v2  |  14.4 GB/s |  15.3 GB/s |  14.3 GB/s |
| Avx256_StoreNonTemporal_Unrolled4v2  |  14.6 GB/s |  15.3 GB/s |  14.3 GB/s |
| Avx256_StoreNonTemporal_Unrolled8v2  |  14.5 GB/s |  15.0 GB/s |  14.0 GB/s |
| Avx256_StoreNonTemporal_Unrolled16v2 |  14.4 GB/s |  15.0 GB/s |  14.0 GB/s |

Running Copy Performance Tests [.NET Core 4.6.29518.01 (2.1) on BEAST]...

| Method                   | Speed [1T] | Speed [2T] | Speed [4T] |
| ------------------------ | ---------: | ---------: | ---------: |
| ArrayCopy                |  9.33 GB/s |  11.3 GB/s |  11.9 GB/s |
| BufferBlockCopy          |  9.47 GB/s |  11.4 GB/s |  11.9 GB/s |
| UnsafeCopyBlock          |  9.60 GB/s |  11.8 GB/s |  12.1 GB/s |
| UnsafeCopyBlockUnaligned |  9.61 GB/s |  11.8 GB/s |  11.9 GB/s |
| ForByte                  |  1.71 GB/s |  3.41 GB/s |  6.62 GB/s |
| ForUnsafeAsLong          |  9.37 GB/s |  10.6 GB/s |  9.86 GB/s |
| BufferMemoryCopy         |  9.64 GB/s |  11.7 GB/s |  12.1 GB/s |
| Memcpy                   |  14.0 GB/s |  14.3 GB/s |  13.2 GB/s |
| UnsafeForLong            |  9.37 GB/s |  10.6 GB/s |  9.85 GB/s |
| UnsafeWhileLong          |  9.65 GB/s |  10.7 GB/s |  9.97 GB/s |

Running Copy Performance Tests [.NET Core 4.6.27618.02 (1.1) on BEAST]...

| Method                   | Speed [1T] | Speed [2T] | Speed [4T] |
| ------------------------ | ---------: | ---------: | ---------: |
| ArrayCopy                |  9.46 GB/s |  11.4 GB/s |  11.7 GB/s |
| BufferBlockCopy          |  9.49 GB/s |  11.4 GB/s |  11.7 GB/s |
| UnsafeCopyBlock          |  10.5 GB/s |  14.3 GB/s |  13.5 GB/s |
| UnsafeCopyBlockUnaligned |  10.5 GB/s |  14.3 GB/s |  13.6 GB/s |
| ForByte                  |  1.71 GB/s |  3.41 GB/s |  6.51 GB/s |
| ForUnsafeAsLong          |  9.42 GB/s |  10.5 GB/s |  9.76 GB/s |
| BufferMemoryCopy         |  9.62 GB/s |  11.6 GB/s |  11.9 GB/s |
| Memcpy                   |  14.0 GB/s |  14.4 GB/s |  13.1 GB/s |
| UnsafeForLong            |  9.42 GB/s |  10.4 GB/s |  9.66 GB/s |
| UnsafeWhileLong          |  9.67 GB/s |  10.5 GB/s |  9.79 GB/s |



### Quick Q&A

**Do some .NET built-ins perform better than others?**
With .NET 5.0, it doesn't matter. 
Array.Copy had the most consistent high performance across the runtimes and machines I tested.
Array.Copy and Buffer.MemoryCopy were dramatically faster than other built-ins on my AMD Laptop on .NET Core 2.1.
Unsafe.CopyBlock was faster than other built-ins on my Intel Desktop on .NET Core 1.1.

**What makes the biggest difference in maximum copy bandwidth?**
All copies are significantly faster when on a second thread. Once multiple threads are used, copies which use a "non-temporal store", which avoids reading the value-to-store into a cache line, have a much higher overall bandwidth ceiling. (19.0 GB/s versus 10.5 GB/s on my AMD laptop, and 15.2 GB/s versus 10.7 GB/s on my Intel desktop).

**Does loop unrolling matter?**
Unrolling AVX copies at least once helped single threaded copies. Once half or more of my cores were busy, there was a minimal difference.

**Does AVX2 (Avx256) perform better than AVX (Avx128)?**
Yes, but only single threaded or two-threaded and not unrolled.

**Did any hand coded loops beat the built-in methods?**
On my Intel Desktop, unrolled AVX loops beat the built-ins relatively significantly, at 15.2 GB/s versus 11.6 GB/s with two threads.

**Do the newer .NET runtimes help?**
When using the built-in methods, many were optimized significantly for .NET Core 3.1 compared to the .NET Core 2.1 and 1.1 runtimes. .NET 5.0 performance seems the same as the .NET Core 3.1 runtime in my tests.

