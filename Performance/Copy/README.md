# C# Byte Copy Performance

This project contains a set of simple methods to copy from one byte array to another in C# to measure the performance of different approaches. My goal was to get as close to the full memory bandwidth reported by memory benchmarks as possible.

### Conclusions

I wasn't able to get the full copy memory bandwidth reported for my computer, which should be **40 GB/s** according to AIDA64.

The fastest single threaded copy, at 13 GB/s, was **Unsafe.CopyBlock()**.

The next tier, at around 9 GB/s, includes Array.Copy(), Buffer.BlockCopy(), Buffer.MemoryCopy(), and P/Invoke to kernel32 memcpy().

Hand-coded for and while loops over long arrays are similar, at 8 GB/s. Using unsafe code to potentially avoid extra bounds checking wasn't any faster. Using SSE and AVX instructions to copy more than 64-bit values per operation also didn't seem to help.

As might be expected, a for loop over the byte array without casting is much slower, at 2 GB/s.

Using non-temporal stores should be faster, because they should not need to read the destination memory into a memory cache first. Unsafe code using these variants was not drastically faster single-threaded, at 8-11 GB/s. 

When running multi-threaded, the options which use non-temporal stores can hit about 18 GB/s, while the options that don't top out at around 11 GB/s. Two threads achieve close to peak performance, and more than four threads didn't help for any method.

### Measurements 

.NET 5.0, Release, Plugged In, 'Best Performance' power profile.

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
| ForByte                     | 1.77 GB/s |     17.6 ms |
| ForByte 2t                  | 3.55 GB/s |     8.81 ms |
| ForByte 4t                  | 6.79 GB/s |     4.60 ms |
| ArrayCopy                   | 8.95 GB/s |     3.49 ms |
| ArrayCopy 2t                | 10.3 GB/s |     3.03 ms |
| ArrayCopy 4t                | 11.2 GB/s |     2.80 ms |
| BufferBlockCopy             | 8.91 GB/s |     3.51 ms |
| BufferBlockCopy 2t          | 10.1 GB/s |     3.11 ms |
| BufferBlockCopy 4t          | 11.1 GB/s |     2.82 ms |
| ForUnsafeAsLong             | 7.90 GB/s |     3.96 ms |
| ForUnsafeAsLong 2t          | 9.01 GB/s |     3.47 ms |
| ForUnsafeAsLong 4t          | 8.85 GB/s |     3.53 ms |
| UnsafeCopyBlock             | 9.19 GB/s |     3.40 ms |
| UnsafeCopyBlock 2t          | 10.6 GB/s |     2.95 ms |
| UnsafeCopyBlock 4t          | 11.3 GB/s |     2.76 ms |
| UnsafeCopyBlockUnaligned    | 9.33 GB/s |     3.35 ms |
| UnsafeCopyBlockUnaligned 2t | 11.1 GB/s |     2.81 ms |
| UnsafeCopyBlockUnaligned 4t | 11.4 GB/s |     2.74 ms |
| BufferMemoryCopy            | 9.15 GB/s |     3.41 ms |
| BufferMemoryCopy 2t         | 11.0 GB/s |     2.84 ms |
| BufferMemoryCopy 4t         | 11.3 GB/s |     2.77 ms |
| UnsafeForLong               | 9.16 GB/s |     3.41 ms |
| UnsafeForLong 2t            | 9.65 GB/s |     3.24 ms |
| UnsafeWhileLong             | 9.03 GB/s |     3.46 ms |
| UnsafeWhileLong 2t          | 9.62 GB/s |     3.25 ms |
| LoadUStoreU                 | 9.81 GB/s |     3.19 ms |
| LoadUStoreU 2t              | 9.86 GB/s |     3.17 ms |
| Avx128                      | 9.84 GB/s |     3.18 ms |
| Avx128 2t                   | 9.88 GB/s |     3.16 ms |
| Avx256                      | 9.78 GB/s |     3.20 ms |
| Avx256 2t                   | 9.93 GB/s |     3.15 ms |
| StoreNonTemporalInt         | 8.74 GB/s |     3.58 ms |
| StoreNonTemporalInt 2t      | 14.3 GB/s |     2.19 ms |
| StoreNonTemporalInt 4t      | 13.8 GB/s |     2.27 ms |
| StoreNonTemporalLong        | 13.3 GB/s |     2.36 ms |
| StoreNonTemporalLong 2t     | 14.4 GB/s |     2.17 ms |
| StoreNonTemporalAvx128      | 6.99 GB/s |     4.47 ms |
| StoreNonTemporalAvx128 2t   | 13.4 GB/s |     2.33 ms |
| StoreNonTemporalAvx128 4t   | 13.6 GB/s |     2.29 ms |
| Memcpy                      | 13.4 GB/s |     2.33 ms |
| Memcpy 2t                   | 13.7 GB/s |     2.28 ms |