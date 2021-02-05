# C# Byte Copy Performance

I wanted to know the fastest way to copy from one byte array to another in C#, and to see if I could match the benchmark-reported memory bandwidth of my computer. 

The AIDA64 benchmark reports the memory copy bandwidth of my ASUS ROG Zephyrus G14 at about [**39.6 GB/s**](https://rog.asus.com/us/articles/reviews/rog-zephyrus-g14-lab-report-2). Importantly, AIDA64 counts [the sum](https://forums.aida64.com/topic/3708-memory-bench-questions/) of the read and write size in this measurement, so this really means about 20 GB of data can be copied in one second. My numbers below only count the copied data size once, as this makes more sense to me as a programmer.

### Conclusions

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
| ForByte                     | 2.07 GB/s |     15.1 ms |
| ForByte 2t                  | 4.06 GB/s |     7.69 ms |
| ForByte 4t                  | 7.63 GB/s |     4.10 ms |
| ForByte 8t                  | 9.77 GB/s |     3.20 ms |
| ForByte 16t                 | 9.67 GB/s |     3.23 ms |
| ArrayCopy                   | 9.10 GB/s |     3.44 ms |
| ArrayCopy 2t                | 16.2 GB/s |     1.93 ms |
| ArrayCopy 4t                | 18.4 GB/s |     1.70 ms |
| ArrayCopy 8t                | 19.3 GB/s |     1.62 ms |
| BufferBlockCopy             | 9.11 GB/s |     3.43 ms |
| BufferBlockCopy 2t          | 16.3 GB/s |     1.92 ms |
| BufferBlockCopy 4t          | 18.5 GB/s |     1.69 ms |
| BufferBlockCopy 8t          | 19.3 GB/s |     1.62 ms |
| ForUnsafeAsLong             | 8.32 GB/s |     3.75 ms |
| ForUnsafeAsLong 2t          | 10.7 GB/s |     2.93 ms |
| ForUnsafeAsLong 4t          | 11.0 GB/s |     2.83 ms |
| UnsafeCopyBlock             | 12.0 GB/s |     2.61 ms |
| UnsafeCopyBlock 2t          | 17.6 GB/s |     1.78 ms |
| UnsafeCopyBlock 4t          | 18.8 GB/s |     1.66 ms |
| UnsafeCopyBlockUnaligned    | 11.8 GB/s |     2.66 ms |
| UnsafeCopyBlockUnaligned 2t | 17.2 GB/s |     1.82 ms |
| UnsafeCopyBlockUnaligned 4t | 18.7 GB/s |     1.67 ms |
| BufferMemoryCopy            | 9.10 GB/s |     3.44 ms |
| BufferMemoryCopy 2t         | 16.3 GB/s |     1.92 ms |
| BufferMemoryCopy 4t         | 18.3 GB/s |     1.71 ms |
| BufferMemoryCopy 8t         | 19.3 GB/s |     1.62 ms |
| UnsafeForLong               | 8.45 GB/s |     3.70 ms |
| UnsafeForLong 2t            | 10.9 GB/s |     2.86 ms |
| UnsafeForLong 4t            | 11.1 GB/s |     2.82 ms |
| UnsafeWhileLong             | 8.41 GB/s |     3.72 ms |
| UnsafeWhileLong 2t          | 11.0 GB/s |     2.85 ms |
| UnsafeWhileLong 4t          | 11.1 GB/s |     2.81 ms |
| LoadUStoreU                 | 8.92 GB/s |     3.50 ms |
| LoadUStoreU 2t              | 11.3 GB/s |     2.77 ms |
| LoadUStoreU 4t              | 11.1 GB/s |     2.81 ms |
| Avx128                      | 8.93 GB/s |     3.50 ms |
| Avx128 2t                   | 11.3 GB/s |     2.77 ms |
| Avx128 4t                   | 11.2 GB/s |     2.79 ms |
| Avx256                      | 9.00 GB/s |     3.47 ms |
| Avx256 2t                   | 11.3 GB/s |     2.77 ms |
| Avx256 4t                   | 11.4 GB/s |     2.75 ms |
| StoreNonTemporalInt         | 9.52 GB/s |     3.28 ms |
| StoreNonTemporalInt 2t      | 14.4 GB/s |     2.17 ms |
| StoreNonTemporalInt 4t      | 17.5 GB/s |     1.78 ms |
| StoreNonTemporalInt 8t      | 18.0 GB/s |     1.73 ms |
| StoreNonTemporalLong        | 11.0 GB/s |     2.85 ms |
| StoreNonTemporalLong 2t     | 15.5 GB/s |     2.02 ms |
| StoreNonTemporalLong 4t     | 18.0 GB/s |     1.74 ms |
| StoreNonTemporalLong 8t     | 18.5 GB/s |     1.69 ms |
| StoreNonTemporalAvx128      | 7.62 GB/s |     4.10 ms |
| StoreNonTemporalAvx128 2t   | 13.8 GB/s |     2.27 ms |
| StoreNonTemporalAvx128 4t   | 17.8 GB/s |     1.76 ms |
| StoreNonTemporalAvx128 8t   | 18.2 GB/s |     1.71 ms |
| Memcpy                      | 10.9 GB/s |     2.86 ms |
| Memcpy 2t                   | 16.4 GB/s |     1.90 ms |
| Memcpy 4t                   | 18.4 GB/s |     1.70 ms |
| Memcpy 8t                   | 19.0 GB/s |     1.64 ms |

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