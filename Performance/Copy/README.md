# C# Byte Copy Performance

Can you copy bytes in C# at the full speed of memory benchmarks like AIDA64? Which copy methods have the best performance, and how many parallel threads do you need? Can handwritten loops be as fast? 

### Setup

The AIDA64 benchmark reports the memory copy bandwidth of my laptop, an ASUS ROG Zephyrus G14, at about [**39.6 GB/s**](https://rog.asus.com/us/articles/reviews/rog-zephyrus-g14-lab-report-2). Importantly, AIDA64 counts **[the sum](https://forums.aida64.com/topic/3708-memory-bench-questions/)** of the input and output size in this measurement, so this really means just under 20 GB of data could be copied from one place in memory to another in one second. In my numbers below, I'll only could the amount of data copied once, as this makes more sense to me. =)

For this demo, I've built a minimal .NET 5.0 console app. I'm copying 32 MB of random data from one byte[] to another, measuring for two seconds per implementation. In one accidental run with the .NET Core 3.1 runtime, I saw similar numbers. You can clone [the code](https://github.com/ScottLouvau/experiments) quickly to try it yourself on other hardware and runtimes.

### Results

TL;DR: Yes, you can match benchmark performance from C#. On my machine, four threads of **Unsafe.CopyBlock()** performs best.

The fastest single threaded copy, at 13 GB/s, was **Unsafe.CopyBlock()**.

The next tier, at around 9 GB/s, includes Array.Copy(), Buffer.BlockCopy(), Buffer.MemoryCopy(), and P/Invoke to kernel32 memcpy().

Hand-coded for and while loops over long arrays are similar, at 8 GB/s. Using unsafe code to potentially avoid extra bounds checking wasn't any faster. Using SSE and AVX instructions to copy more than 64-bit values per operation also didn't seem to help.

As might be expected, a for loop over the byte array without casting is much slower, at 2 GB/s.

Using non-temporal stores should be faster, because they should not need to read the destination memory into a memory cache first. Unsafe code using these variants was not drastically faster single-threaded, at 8-11 GB/s. 

When running multi-threaded, the options which use non-temporal stores can hit about 18 GB/s, while the options that don't top out at around 11 GB/s. Two threads achieve close to peak performance, and more than four threads didn't help for any method.

### Measurements 

.NET 5.0, Release, Plugged In, 'Best Performance' power profile. These numbers count the size of the data copied once (not once for the read and once for the write), so ideally these numbers will be half of the AIDA64 memory copy score.

#### ASUS ROG Zephyrus G14: Ryzen 4900 HS, 16 GB DDR4-3200

| Method                               | Speed [1T] | Speed [2T] | Speed [4T] | Speed [8T] | Speed [16T] |
| ------------------------------------ | ---------: | ---------: | ---------: | ---------: | ----------: |
| ArrayCopy                            |  12.6 GB/s |  16.9 GB/s |  18.2 GB/s |  18.8 GB/s |   18.0 GB/s |
| BufferBlockCopy                      |  12.7 GB/s |  16.9 GB/s |  18.2 GB/s |  18.8 GB/s |   18.0 GB/s |
| AsSpanCopy                           |  12.6 GB/s |  17.0 GB/s |  18.2 GB/s |  18.8 GB/s |   18.0 GB/s |
| UnsafeCopyBlock                      |  12.7 GB/s |  17.1 GB/s |  18.0 GB/s |  18.9 GB/s |   18.0 GB/s |
| UnsafeCopyBlockUnaligned             |  12.7 GB/s |  17.1 GB/s |  18.1 GB/s |  18.8 GB/s |   18.0 GB/s |
| ForByte                              |  1.99 GB/s |  3.82 GB/s |  7.28 GB/s |  9.94 GB/s |   9.51 GB/s |
| ForUnsafeAsLong                      |  7.78 GB/s |  10.4 GB/s |  10.4 GB/s |  10.5 GB/s |   9.69 GB/s |
| BufferMemoryCopy                     |  12.7 GB/s |  17.0 GB/s |  18.2 GB/s |  18.8 GB/s |   18.0 GB/s |
| Memcpy                               |  11.5 GB/s |  16.5 GB/s |  18.0 GB/s |  18.8 GB/s |   18.1 GB/s |
| UnsafeForLong                        |  7.50 GB/s |  9.78 GB/s | 10.00 GB/s |  9.45 GB/s |   9.33 GB/s |
| UnsafeWhileLong                      |  7.02 GB/s |  9.85 GB/s |  10.4 GB/s |  10.5 GB/s |   9.74 GB/s |
| Avx128_LoadStore                     |  8.78 GB/s |  10.6 GB/s |  10.4 GB/s |  10.2 GB/s |   9.61 GB/s |
| Avx256_LoadStore                     |  8.89 GB/s |  10.8 GB/s |  10.5 GB/s |  10.5 GB/s |   9.65 GB/s |
| StoreNonTemporalInt                  |  7.28 GB/s |  12.3 GB/s |  16.9 GB/s |  17.8 GB/s |   17.2 GB/s |
| StoreNonTemporalLong                 |  10.8 GB/s |  15.1 GB/s |  17.8 GB/s |  18.4 GB/s |   17.5 GB/s |
| StoreNonTemporalLong_Unrolled2       |  11.0 GB/s |  15.4 GB/s |  17.7 GB/s |  18.4 GB/s |   17.6 GB/s |
| StoreNonTemporalLong_Unrolled4       |  11.0 GB/s |  15.3 GB/s |  17.7 GB/s |  18.4 GB/s |   17.6 GB/s |
| Avx128_StoreNonTemporal              |  7.18 GB/s |  12.5 GB/s |  17.4 GB/s |  18.1 GB/s |   17.4 GB/s |
| Avx128_StoreNonTemporal_Unrolled2    |  9.78 GB/s |  14.9 GB/s |  17.7 GB/s |  18.3 GB/s |   17.5 GB/s |
| Avx128_StoreNonTemporal_Unrolled2v2  |  9.70 GB/s |  14.6 GB/s |  17.7 GB/s |  18.3 GB/s |   17.4 GB/s |
| Avx128_StoreNonTemporal_Unrolled4v2  |  10.6 GB/s |  15.2 GB/s |  17.7 GB/s |  18.3 GB/s |   17.5 GB/s |
| Avx256_StoreNonTemporal              |  9.72 GB/s |  15.2 GB/s |  17.7 GB/s |  18.3 GB/s |   17.5 GB/s |
| Avx256_StoreNonTemporal_Unrolled2v2  |  11.1 GB/s |  15.5 GB/s |  17.7 GB/s |  18.3 GB/s |   17.6 GB/s |
| Avx256_StoreNonTemporal_Unrolled4v2  |  11.1 GB/s |  15.6 GB/s |  17.5 GB/s |  18.2 GB/s |   17.6 GB/s |
| Avx256_StoreNonTemporal_Unrolled8v2  |  12.5 GB/s |  16.7 GB/s |  17.7 GB/s |  18.3 GB/s |   17.5 GB/s |
| Avx256_StoreNonTemporal_Unrolled16v2 |  12.5 GB/s |  16.7 GB/s |  17.6 GB/s |  18.3 GB/s |   17.5 GB/s |

#### Desktop: i7-7500, 32 GB DDR4-2400