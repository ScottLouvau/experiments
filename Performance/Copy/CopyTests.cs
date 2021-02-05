using RoughBench.Attributes;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;

namespace Copy
{
    public class CopyTests
    {
        public const int Length = 32 * 1024 * 1024;
        private byte[] Source;
        private byte[] Target;

        public CopyTests()
        {
            Source = new byte[Length];
            Target = new byte[Length];

            Random r = new Random();
            r.NextBytes(Source);
        }

        // Standard signature for wrappable copy implementations
        public delegate void CopyKernel(byte[] source, byte[] target, int index, int lengthInBytes);

        // Standard signature for unsafe code copy implementations (factor out the pinning and pointer offsets for start index)
        public unsafe delegate void UnsafeCopyKernel(byte* source, byte* target, int lengthInBytes);

        // Convert an unsafe kernel to a normal one (implement the pinning and pointer offsets)
        public unsafe CopyKernel Wrap(UnsafeCopyKernel kernel)
        {
            return (source, target, index, lengthInBytes) =>
            {
                fixed (byte* pSource = source)
                fixed (byte* pTarget = target)
                {
                    kernel(pSource + index, pTarget + index, lengthInBytes);
                }
            };
        }

        // Parallelize a kernel
        public CopyKernel Parallelize(CopyKernel kernel, int partitions)
        {
            if (partitions == 1) { return kernel; }

            return (source, target, index, lengthInBytes) =>
            {
                int slice = lengthInBytes / partitions;
                Parallel.For(0, partitions, (i) =>
                {
                    kernel(source, target, i * slice, slice);
                });
            };
        }

        // Run a particular kernel on the sample arrays
        public void Run(CopyKernel kernel)
        {
            kernel(Source, Target, 0, Length);
        }

        // Reset target between operations
        public void Clear()
        {
            Array.Clear(Target, 0, Length);
        }

        // Verify target matches source after a benchmark
        public bool VerifyIdentical()
        {
            for (int i = 0; i < Length; ++i)
            {
                if (Target[i] != Source[i])
                {
                    return false;
                }
            }

            return true;
        }

        // Methods which require target pointer alignment can run this first to copy any unaligned prefix
        protected unsafe void CopyUntilAligned(ref byte* source, ref byte* target, ref int lengthInBytes)
        {
            // Copy bytes manually until target is 64-byte aligned
            while (((long)target & 0x4F) != 0)
            {
                *target++ = *source++;
                lengthInBytes--;
            }
        }

        // ==============================================================================================

        // ~2 GB/s

        [Benchmark]
        public void ForByte(byte[] source, byte[] target, int index, int lengthInBytes)
        {
            for (int i = index; i < index + lengthInBytes; ++i)
            {
                target[i] = source[i];
            }
        }


        // ~ 8 GB/s

        [Benchmark]
        public void ArrayCopy(byte[] source, byte[] target, int index, int lengthInBytes)
        {
            Array.Copy(source, index, target, index, lengthInBytes);
        }

        [Benchmark]
        public void BufferBlockCopy(byte[] source, byte[] target, int index, int lengthInBytes)
        {
            Buffer.BlockCopy(source, index, target, index, lengthInBytes);
        }

        [Benchmark]
        public unsafe void BufferMemoryCopy(byte* source, byte* target, int lengthInBytes)
        {
            Buffer.MemoryCopy(source, target, lengthInBytes, lengthInBytes);
        }

        [Benchmark]
        public void ForUnsafeAsLong(byte[] source, byte[] target, int index, int lengthInBytes)
        {
            ulong[] from = Unsafe.As<ulong[]>(source);
            ulong[] to = Unsafe.As<ulong[]>(target);
            int start = index / 8;
            int length = lengthInBytes / 8;

            for (int i = start; i < start + length; ++i)
            {
                to[i] = from[i];
            }
        }

        [Benchmark]
        public unsafe void UnsafeForLong(byte* source, byte* target, int lengthInBytes)
        {
            ulong* from = (ulong*)source;
            ulong* to = (ulong*)target;
            int length = lengthInBytes / 8;

            for (int i = 0; i < length; ++i)
            {
                to[i] = from[i];
            }
        }

        [Benchmark]
        public unsafe void UnsafeWhileLong(byte* source, byte* target, int lengthInBytes)
        {
            ulong* from = (ulong*)source;
            ulong* fromEnd = (ulong*)(&source[lengthInBytes - 1]);
            ulong* to = (ulong*)target;

            while (from < fromEnd)
            {
                *to++ = *from++;
            }
        }

        [Benchmark]
        public unsafe void LoadUStoreU(byte* source, byte* target, int lengthInBytes)
        {
            // Recommendation from Peter Cordes in https://stackoverflow.com/questions/39153868/vectorized-memcpy-that-beats-intel-fast-memcpy
            float* from = (float*)source;
            float* to = (float*)target;

            for (int i = 0; i < lengthInBytes; i += 16)
            {
                Sse2.Store(to, Sse2.LoadVector128(from));

                from += 4;
                to += 4;
            }
        }

        [Benchmark]
        public unsafe void Avx128(byte* source, byte* target, int lengthInBytes)
        {
            for (int i = 0; i < lengthInBytes; i += 16)
            {
                Avx.Store(target, Avx.LoadDquVector128(source));

                source += 16;
                target += 16;
            }
        }

        [Benchmark]
        public unsafe void Avx256(byte* source, byte* target, int lengthInBytes)
        {
            for (int i = 0; i < lengthInBytes; i += 32)
            {
                Avx2.Store(target, Avx2.LoadDquVector256(source));

                source += 32;
                target += 32;
            }
        }

        [Benchmark]
        public unsafe void StoreNonTemporalInt(byte* source, byte* target, int lengthInBytes)
        {
            uint* from = (uint*)source;
            uint* to = (uint*)target;
            int length = lengthInBytes / 4;

            for (int i = 0; i < length; ++i)
            {
                Avx.StoreNonTemporal(&to[i], from[i]);
            }
        }

        [Benchmark]
        public unsafe void StoreNonTemporalLong(byte* source, byte* target, int lengthInBytes)
        {
            ulong* from = (ulong*)source;
            ulong* to = (ulong*)target;
            int length = lengthInBytes / 8;

            for (int i = 0; i < length; ++i)
            {
                Sse2.X64.StoreNonTemporal(&to[i], from[i]);
            }
        }

        [Benchmark]
        public unsafe void StoreNonTemporalAvx128(byte* source, byte* target, int lengthInBytes)
        {
            CopyUntilAligned(ref source, ref target, ref lengthInBytes);

            for (int i = 0; i < lengthInBytes; i += 16)
            {
                Avx.StoreAlignedNonTemporal(target, Avx.LoadVector128(source));

                source += 16;
                target += 16;
            }
        }


        // ~11 GB/s

        [Benchmark]
        public unsafe void Memcpy(byte* source, byte* target, int lengthInBytes)
        {
            memcpy((IntPtr)target, (IntPtr)source, lengthInBytes);
        }

        [DllImport("msvcrt.dll", SetLastError = false)]
        public static extern IntPtr memcpy(IntPtr dest, IntPtr src, int count);


        // ~13 GB/s

        [Benchmark]
        public void UnsafeCopyBlock(byte[] source, byte[] target, int index, int lengthInBytes)
        {
            Unsafe.CopyBlock(ref source[index], ref target[index], (uint)lengthInBytes);
        }

        [Benchmark]
        public void UnsafeCopyBlockUnaligned(byte[] source, byte[] target, int index, int lengthInBytes)
        {
            Unsafe.CopyBlockUnaligned(ref source[index], ref target[index], (uint)lengthInBytes);
        }
    }
}