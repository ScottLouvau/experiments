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

.NET 5.0, Release, Ryzen 4900 HS, 16 GB DDR4-3200, ASUS ROG Zephyrus G14

| Method                      |     Speed | Per 32.0 MB |
| --------------------------- | --------: | ----------: |
| UnsafeCopyBlock             | 13.0 GB/s |     2.40 ms |
| UnsafeCopyBlock 2t          | 17.3 GB/s |     1.80 ms |
| UnsafeCopyBlock 4t          | 18.6 GB/s |     1.68 ms |
| ForByte                     | 2.00 GB/s |     15.6 ms |
| ForByte 2t                  | 3.96 GB/s |     7.88 ms |
| ForByte 4t                  | 7.28 GB/s |     4.29 ms |
| ForByte 8t                  | 9.48 GB/s |     3.30 ms |
| ForByte 16t                 | 9.45 GB/s |     3.31 ms |
| ArrayCopy                   | 9.11 GB/s |     3.43 ms |
| ArrayCopy 2t                | 16.4 GB/s |     1.91 ms |
| ArrayCopy 4t                | 18.3 GB/s |     1.70 ms |
| ArrayCopy 8t                | 19.2 GB/s |     1.62 ms |
| BufferBlockCopy             | 9.11 GB/s |     3.43 ms |
| BufferBlockCopy 2t          | 16.4 GB/s |     1.90 ms |
| BufferBlockCopy 4t          | 18.2 GB/s |     1.72 ms |
| ForUnsafeAsLong             | 8.18 GB/s |     3.82 ms |
| ForUnsafeAsLong 2t          | 10.1 GB/s |     3.10 ms |
| ForUnsafeAsLong 4t          | 10.9 GB/s |     2.87 ms |
| UnsafeCopyBlockUnaligned    | 13.0 GB/s |     2.40 ms |
| UnsafeCopyBlockUnaligned 2t | 17.3 GB/s |     1.80 ms |
| UnsafeCopyBlockUnaligned 4t | 18.7 GB/s |     1.67 ms |
| BufferMemoryCopy            | 9.11 GB/s |     3.43 ms |
| BufferMemoryCopy 2t         | 16.4 GB/s |     1.91 ms |
| BufferMemoryCopy 4t         | 18.3 GB/s |     1.70 ms |
| BufferMemoryCopy 8t         | 19.2 GB/s |     1.63 ms |
| UnsafeFor                   | 8.46 GB/s |     3.69 ms |
| UnsafeFor 2t                | 10.3 GB/s |     3.03 ms |
| UnsafeFor 4t                | 10.9 GB/s |     2.85 ms |
| UnsafeWhile                 | 8.31 GB/s |     3.76 ms |
| UnsafeWhile 2t              | 10.2 GB/s |     3.06 ms |
| UnsafeWhile 4t              | 11.0 GB/s |     2.85 ms |
| Avx128                      | 8.84 GB/s |     3.53 ms |
| Avx128 2t                   | 11.0 GB/s |     2.84 ms |
| Avx128 4t                   | 10.9 GB/s |     2.87 ms |
| Avx256                      | 8.93 GB/s |     3.50 ms |
| Avx256 2t                   | 11.0 GB/s |     2.85 ms |
| Avx256 4t                   | 10.9 GB/s |     2.86 ms |
| StoreNonTemporalInt         | 9.40 GB/s |     3.33 ms |
| StoreNonTemporalInt 2t      | 14.1 GB/s |     2.22 ms |
| StoreNonTemporalInt 4t      | 17.5 GB/s |     1.79 ms |
| StoreNonTemporalInt 8t      | 18.0 GB/s |     1.74 ms |
| StoreNonTemporalLong        | 10.9 GB/s |     2.86 ms |
| StoreNonTemporalLong 2t     | 15.5 GB/s |     2.02 ms |
| StoreNonTemporalLong 4t     | 17.9 GB/s |     1.74 ms |
| StoreNonTemporalLong 8t     | 18.4 GB/s |     1.70 ms |
| StoreNonTemporalAvx128      | 7.56 GB/s |     4.13 ms |
| StoreNonTemporalAvx128 2t   | 12.3 GB/s |     2.55 ms |
| StoreNonTemporalAvx128 4t   | 16.8 GB/s |     1.86 ms |
| StoreNonTemporalAvx128 8t   | 18.2 GB/s |     1.72 ms |
| LoadUStoreU                 | 8.82 GB/s |     3.54 ms |
| LoadUStoreU 2t              | 11.0 GB/s |     2.85 ms |
| LoadUStoreU 4t              | 11.0 GB/s |     2.85 ms |
| Memcpy                      | 11.0 GB/s |     2.85 ms |
| Memcpy 2t                   | 16.2 GB/s |     1.93 ms |
| Memcpy 4t                   | 18.3 GB/s |     1.70 ms |
| Memcpy 8t                   | 19.1 GB/s |     1.63 ms |