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

| Method                                | Speed [1T] | Speed [2T] | Speed [4T] | Speed [8T] | Speed [16T] |
| ------------------------------------- | ---------: | ---------: | ---------: | ---------: | ----------: |
| ArrayCopy                             |  12.7 GB/s |  17.3 GB/s |  18.2 GB/s |  18.9 GB/s |   17.9 GB/s |
| BufferBlockCopy                       |  12.7 GB/s |  17.0 GB/s |  18.2 GB/s |  18.8 GB/s |   17.9 GB/s |
| AsSpanCopy                            |  12.8 GB/s |  16.9 GB/s |  18.2 GB/s |  18.8 GB/s |   17.9 GB/s |
| UnsafeCopyBlock                       |  12.6 GB/s |  16.9 GB/s |  18.0 GB/s |  18.8 GB/s |   17.9 GB/s |
| UnsafeCopyBlockUnaligned              |  12.6 GB/s |  17.0 GB/s |  18.1 GB/s |  18.6 GB/s |   17.9 GB/s |
| ForByte                               |  1.93 GB/s |  3.73 GB/s |  6.69 GB/s |  9.53 GB/s |   9.42 GB/s |
| ForUnsafeAsLong                       |  7.71 GB/s |  10.3 GB/s |  10.5 GB/s |  10.5 GB/s |   9.67 GB/s |
| BufferMemoryCopy                      |  12.7 GB/s |  16.8 GB/s |  17.9 GB/s |  18.6 GB/s |   17.8 GB/s |
| Memcpy                                |  11.8 GB/s |  16.4 GB/s |  18.1 GB/s |  18.9 GB/s |   18.0 GB/s |
| UnsafeForLong                         |  7.80 GB/s |  10.2 GB/s |  10.2 GB/s |  10.3 GB/s |   9.57 GB/s |
| UnsafeWhileLong                       |  6.79 GB/s |  8.91 GB/s |  9.00 GB/s |  9.62 GB/s |   9.41 GB/s |
| LoadUStoreU                           |  7.96 GB/s |  9.33 GB/s |  10.2 GB/s |  10.1 GB/s |   9.61 GB/s |
| Avx128                                |  8.80 GB/s |  10.6 GB/s |  10.6 GB/s |  10.4 GB/s |   9.56 GB/s |
| Avx256                                |  8.68 GB/s |  10.6 GB/s |  10.4 GB/s |  10.5 GB/s |   9.70 GB/s |
| StoreNonTemporalInt                   |  7.21 GB/s |  12.5 GB/s |  16.9 GB/s |  17.7 GB/s |   17.3 GB/s |
| StoreNonTemporalLong                  |  11.1 GB/s |  14.9 GB/s |  17.7 GB/s |  18.2 GB/s |   17.5 GB/s |
| StoreNonTemporalAvx128                |  7.22 GB/s |  13.0 GB/s |  17.3 GB/s |  18.1 GB/s |   17.4 GB/s |
| StoreNonTemporalAvx256UnrolledAligned |  12.7 GB/s |  16.7 GB/s |  17.7 GB/s |  18.1 GB/s |   17.5 GB/s |



#### Desktop: i7-7500, 32 GB DDR4-2400