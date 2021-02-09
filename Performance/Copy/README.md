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

| Method [+ threads]          |     Speed | Per 32.0 MB |
| --------------------------- | --------: | ----------: |
| ForByte                     | 2.10 GB/s |     14.9 ms |
| ForByte 2t                  | 4.08 GB/s |     7.65 ms |
| ForByte 4t                  | 7.69 GB/s |     4.06 ms |
| ForByte 8t                  | 9.91 GB/s |     3.15 ms |
| ForByte 16t                 | 9.79 GB/s |     3.19 ms |
| ArrayCopy                   | 9.12 GB/s |     3.42 ms |
| ArrayCopy 2t                | 16.4 GB/s |     1.90 ms |
| ArrayCopy 4t                | 18.5 GB/s |     1.69 ms |
| ArrayCopy 8t                | 19.5 GB/s |     1.60 ms |
| BufferBlockCopy             | 9.13 GB/s |     3.42 ms |
| BufferBlockCopy 2t          | 16.4 GB/s |     1.91 ms |
| BufferBlockCopy 4t          | 18.5 GB/s |     1.69 ms |
| BufferBlockCopy 8t          | 19.5 GB/s |     1.60 ms |
| ForUnsafeAsLong             | 8.40 GB/s |     3.72 ms |
| ForUnsafeAsLong 2t          | 10.4 GB/s |     3.02 ms |
| ForUnsafeAsLong 4t          | 11.0 GB/s |     2.84 ms |
| AsSpanCopy                  | 9.12 GB/s |     3.42 ms |
| AsSpanCopy 2t               | 16.4 GB/s |     1.90 ms |
| AsSpanCopy 4t               | 18.6 GB/s |     1.68 ms |
| AsSpanCopy 8t               | 19.5 GB/s |     1.60 ms |
| UnsafeCopyBlock             | 13.2 GB/s |     2.36 ms |
| UnsafeCopyBlock 2t          | 17.5 GB/s |     1.79 ms |
| UnsafeCopyBlock 4t          | 18.8 GB/s |     1.66 ms |
| UnsafeCopyBlockUnaligned    | 13.2 GB/s |     2.36 ms |
| UnsafeCopyBlockUnaligned 2t | 17.4 GB/s |     1.79 ms |
| UnsafeCopyBlockUnaligned 4t | 18.8 GB/s |     1.66 ms |
| BufferMemoryCopy            | 9.13 GB/s |     3.42 ms |
| BufferMemoryCopy 2t         | 16.4 GB/s |     1.91 ms |
| BufferMemoryCopy 4t         | 18.6 GB/s |     1.68 ms |
| BufferMemoryCopy 8t         | 19.6 GB/s |     1.59 ms |
| UnsafeForLong               | 8.54 GB/s |     3.66 ms |
| UnsafeForLong 2t            | 10.9 GB/s |     2.87 ms |
| UnsafeForLong 4t            | 11.2 GB/s |     2.79 ms |
| UnsafeWhileLong             | 8.48 GB/s |     3.69 ms |
| UnsafeWhileLong 2t          | 10.9 GB/s |     2.86 ms |
| UnsafeWhileLong 4t          | 11.1 GB/s |     2.83 ms |
| LoadUStoreU                 | 8.97 GB/s |     3.48 ms |
| LoadUStoreU 2t              | 11.2 GB/s |     2.79 ms |
| LoadUStoreU 4t              | 11.0 GB/s |     2.84 ms |
| Avx128                      | 8.96 GB/s |     3.49 ms |
| Avx128 2t                   | 11.2 GB/s |     2.78 ms |
| Avx128 4t                   | 11.2 GB/s |     2.79 ms |
| Avx256                      | 9.07 GB/s |     3.45 ms |
| Avx256 2t                   | 11.2 GB/s |     2.79 ms |
| Avx256 4t                   | 11.1 GB/s |     2.83 ms |
| StoreNonTemporalInt         | 9.46 GB/s |     3.30 ms |
| StoreNonTemporalInt 2t      | 14.5 GB/s |     2.15 ms |
| StoreNonTemporalInt 4t      | 17.6 GB/s |     1.78 ms |
| StoreNonTemporalInt 8t      | 18.2 GB/s |     1.72 ms |
| StoreNonTemporalLong        | 11.0 GB/s |     2.85 ms |
| StoreNonTemporalLong 2t     | 15.8 GB/s |     1.98 ms |
| StoreNonTemporalLong 4t     | 18.0 GB/s |     1.74 ms |
| StoreNonTemporalLong 8t     | 18.7 GB/s |     1.67 ms |
| StoreNonTemporalAvx128      | 7.44 GB/s |     4.20 ms |
| StoreNonTemporalAvx128 2t   | 13.6 GB/s |     2.29 ms |
| StoreNonTemporalAvx128 4t   | 17.0 GB/s |     1.84 ms |
| StoreNonTemporalAvx128 8t   | 18.4 GB/s |     1.69 ms |
| Memcpy                      | 11.0 GB/s |     2.85 ms |
| Memcpy 2t                   | 16.5 GB/s |     1.90 ms |
| Memcpy 4t                   | 18.5 GB/s |     1.69 ms |
| Memcpy 8t                   | 19.5 GB/s |     1.60 ms |



#### Desktop: i7-7500, 32 GB DDR4-2400

| Method [+ threads]          |     Speed | Per 32.0 MB |
| --------------------------- | --------: | ----------: |
| AsSpanCopy                  | 9.50 GB/s |     3.29 ms |
| AsSpanCopy 2t               | 11.8 GB/s |     2.65 ms |
| AsSpanCopy 4t               | 12.1 GB/s |     2.58 ms |
| ForByte                     | 1.77 GB/s |     17.6 ms |
| ForByte 2t                  | 3.55 GB/s |     8.80 ms |
| ForByte 4t                  | 6.76 GB/s |     4.62 ms |
| ArrayCopy                   | 9.55 GB/s |     3.27 ms |
| ArrayCopy 2t                | 11.9 GB/s |     2.63 ms |
| ArrayCopy 4t                | 12.2 GB/s |     2.56 ms |
| BufferBlockCopy             | 9.56 GB/s |     3.27 ms |
| BufferBlockCopy 2t          | 11.9 GB/s |     2.63 ms |
| BufferBlockCopy 4t          | 12.2 GB/s |     2.56 ms |
| ForUnsafeAsLong             | 8.34 GB/s |     3.75 ms |
| ForUnsafeAsLong 2t          | 9.85 GB/s |     3.17 ms |
| ForUnsafeAsLong 4t          | 9.66 GB/s |     3.24 ms |
| UnsafeCopyBlock             | 9.85 GB/s |     3.17 ms |
| UnsafeCopyBlock 2t          | 12.2 GB/s |     2.56 ms |
| UnsafeCopyBlock 4t          | 12.5 GB/s |     2.50 ms |
| UnsafeCopyBlockUnaligned    | 9.86 GB/s |     3.17 ms |
| UnsafeCopyBlockUnaligned 2t | 12.2 GB/s |     2.56 ms |
| UnsafeCopyBlockUnaligned 4t | 12.5 GB/s |     2.50 ms |
| BufferMemoryCopy            | 9.71 GB/s |     3.22 ms |
| BufferMemoryCopy 2t         | 12.1 GB/s |     2.57 ms |
| BufferMemoryCopy 4t         | 12.4 GB/s |     2.51 ms |
| UnsafeForLong               | 9.78 GB/s |     3.19 ms |
| UnsafeForLong 2t            | 11.1 GB/s |     2.81 ms |
| UnsafeForLong 4t            | 10.2 GB/s |     3.07 ms |
| UnsafeWhileLong             | 9.61 GB/s |     3.25 ms |
| UnsafeWhileLong 2t          | 11.0 GB/s |     2.84 ms |
| UnsafeWhileLong 4t          | 10.1 GB/s |     3.09 ms |
| LoadUStoreU                 | 11.0 GB/s |     2.84 ms |
| LoadUStoreU 2t              | 11.4 GB/s |     2.75 ms |
| Avx128                      | 11.0 GB/s |     2.84 ms |
| Avx128 2t                   | 11.4 GB/s |     2.75 ms |
| Avx256                      | 10.9 GB/s |     2.86 ms |
| Avx256 2t                   | 11.4 GB/s |     2.73 ms |
| StoreNonTemporalInt         | 8.96 GB/s |     3.49 ms |
| StoreNonTemporalInt 2t      | 15.6 GB/s |     2.00 ms |
| StoreNonTemporalInt 4t      | 15.0 GB/s |     2.09 ms |
| StoreNonTemporalLong        | 14.3 GB/s |     2.19 ms |
| StoreNonTemporalLong 2t     | 16.0 GB/s |     1.96 ms |
| StoreNonTemporalLong 4t     | 15.0 GB/s |     2.09 ms |
| StoreNonTemporalAvx128      | 7.04 GB/s |     4.44 ms |
| StoreNonTemporalAvx128 2t   | 13.9 GB/s |     2.24 ms |
| StoreNonTemporalAvx128 4t   | 14.9 GB/s |     2.09 ms |
| Memcpy                      | 14.4 GB/s |     2.17 ms |
| Memcpy 2t                   | 15.2 GB/s |     2.06 ms |