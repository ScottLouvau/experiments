// Copyright Scott Louvau, MIT License.

using RoughBench.Attributes;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Copy
{
    public class CopyTestsUnrolled : CopyTestBase
    {
        [Benchmark]
        public unsafe void StoreNonTemporalLong(byte* source, byte* target, int lengthInBytes)
        {
            byte* toEnd = &target[lengthInBytes];

            long* from = (long*)source;
            long* to = (long*)target;
            int length = lengthInBytes / 8;

            for (int i = 0; i < length; ++i)
            {
                Sse2.X64.StoreNonTemporal(&to[i], from[i]);
            }

            CopyByByte((byte*)&from[length], (byte*)&to[length], toEnd);
        }

        [Benchmark]
        public unsafe void StoreNonTemporalLong_Unrolled2(byte* source, byte* target, int lengthInBytes)
        {
            long* from = (long*)source;
            long* to = (long*)target;
            long* toEnd = (long*)&target[lengthInBytes];

            while (to + 2 <= toEnd)
            {
                long r0 = *from;
                long r1 = *(from + 1);

                Sse2.X64.StoreNonTemporal(to, r0);
                Sse2.X64.StoreNonTemporal(to + 1, r1);

                from += 2;
                to += 2;
            }

            CopyByByte((byte*)from, (byte*)to, (byte*)toEnd);
        }

        [Benchmark]
        public unsafe void StoreNonTemporalLong_Unrolled4(byte* source, byte* target, int lengthInBytes)
        {
            long* from = (long*)source;
            long* to = (long*)target;
            long* toEnd = (long*)&target[lengthInBytes];

            long* loopEnd = toEnd - 4;

            while (to <= loopEnd)
            {
                long r0 = *from;
                long r1 = *(from + 1);
                long r2 = *(from + 2);
                long r3 = *(from + 3);

                Sse2.X64.StoreNonTemporal(to, r0);
                Sse2.X64.StoreNonTemporal(to + 1, r1);
                Sse2.X64.StoreNonTemporal(to + 2, r2);
                Sse2.X64.StoreNonTemporal(to + 3, r3);

                from += 4;
                to += 4;
            }

            CopyByByte((byte*)from, (byte*)to, (byte*)toEnd);
        }

        [Benchmark]
        public unsafe void StoreNonTemporalLong_Unrolled8(byte* source, byte* target, int lengthInBytes)
        {
            long* from = (long*)source;
            long* to = (long*)target;
            long* toEnd = (long*)&target[lengthInBytes];

            long* loopEnd = toEnd - 8;

            while (to <= loopEnd)
            {
                long r0 = *from;
                long r1 = *(from + 1);
                long r2 = *(from + 2);
                long r3 = *(from + 3);
                long r4 = *(from + 4);
                long r5 = *(from + 5);
                long r6 = *(from + 6);
                long r7 = *(from + 7);

                Sse2.X64.StoreNonTemporal(to, r0);
                Sse2.X64.StoreNonTemporal(to + 1, r1);
                Sse2.X64.StoreNonTemporal(to + 2, r2);
                Sse2.X64.StoreNonTemporal(to + 3, r3);
                Sse2.X64.StoreNonTemporal(to + 4, r4);
                Sse2.X64.StoreNonTemporal(to + 5, r5);
                Sse2.X64.StoreNonTemporal(to + 6, r6);
                Sse2.X64.StoreNonTemporal(to + 7, r7);

                from += 8;
                to += 8;
            }

            CopyByByte((byte*)from, (byte*)to, (byte*)toEnd);
        }

        [Benchmark]
        public unsafe void Avx128_StoreNonTemporal(byte* source, byte* target, int lengthInBytes)
        {
            byte* toEnd = &target[lengthInBytes];
            CopyUntilAligned(ref source, ref target, ref lengthInBytes, 16);

            for (int i = 0; i + 16 <= lengthInBytes; i += 16)
            {
                Avx.StoreAlignedNonTemporal(target, Avx.LoadVector128(source));

                source += 16;
                target += 16;
            }

            CopyByByte(source, target, toEnd);
        }

        [Benchmark]
        public unsafe void Avx128_StoreNonTemporal_Unrolled2(byte* source, byte* target, int lengthInBytes)
        {
            byte* toEnd = &target[lengthInBytes];
            CopyUntilAligned(ref source, ref target, ref lengthInBytes, 16);

            for (int i = 0; i + 32 < lengthInBytes; i += 32)
            {
                var xmm0 = Avx.LoadVector128(source);
                var xmm1 = Avx.LoadVector128(source + 16);

                Avx.StoreAlignedNonTemporal(target, xmm0);
                Avx.StoreAlignedNonTemporal(target + 16, xmm1);

                source += 32;
                target += 32;
            }

            CopyByByte(source, target, toEnd);
        }

        [Benchmark]
        public unsafe void Avx128_StoreNonTemporal_Unrolled4(byte* source, byte* target, int lengthInBytes)
        {
            byte* toEnd = &target[lengthInBytes];
            CopyUntilAligned(ref source, ref target, ref lengthInBytes, 16);

            for (int i = 0; i + 64 < lengthInBytes; i += 64)
            {
                var xmm0 = Avx.LoadVector128(source);
                var xmm1 = Avx.LoadVector128(source + 16);
                var xmm2 = Avx.LoadVector128(source + 32);
                var xmm3 = Avx.LoadVector128(source + 48);

                Avx.StoreAlignedNonTemporal(target, xmm0);
                Avx.StoreAlignedNonTemporal(target + 16, xmm1);
                Avx.StoreAlignedNonTemporal(target + 32, xmm2);
                Avx.StoreAlignedNonTemporal(target + 48, xmm3);

                source += 64;
                target += 64;
            }

            CopyByByte(source, target, toEnd);
        }

        [Benchmark]
        public unsafe void Avx128_StoreNonTemporal_Unrolled8(byte* source, byte* target, int lengthInBytes)
        {
            byte* toEnd = &target[lengthInBytes];
            CopyUntilAligned(ref source, ref target, ref lengthInBytes, 16);

            for (int i = 0; i + 128 < lengthInBytes; i += 128)
            {
                var xmm0 = Avx.LoadVector128(source);
                var xmm1 = Avx.LoadVector128(source + 16);
                var xmm2 = Avx.LoadVector128(source + 32);
                var xmm3 = Avx.LoadVector128(source + 48);
                var xmm4 = Avx.LoadVector128(source + 64);
                var xmm5 = Avx.LoadVector128(source + 80);
                var xmm6 = Avx.LoadVector128(source + 96);
                var xmm7 = Avx.LoadVector128(source + 112);

                Avx.StoreAlignedNonTemporal(target, xmm0);
                Avx.StoreAlignedNonTemporal(target + 16, xmm1);
                Avx.StoreAlignedNonTemporal(target + 32, xmm2);
                Avx.StoreAlignedNonTemporal(target + 48, xmm3);
                Avx.StoreAlignedNonTemporal(target + 64, xmm4);
                Avx.StoreAlignedNonTemporal(target + 80, xmm5);
                Avx.StoreAlignedNonTemporal(target + 96, xmm6);
                Avx.StoreAlignedNonTemporal(target + 112, xmm7);

                source += 128;
                target += 128;
            }

            CopyByByte(source, target, toEnd);
        }

        [Benchmark]
        public unsafe void Avx256_StoreNonTemporal(byte* source, byte* target, int lengthInBytes)
        {
            byte* toEnd = &target[lengthInBytes];
            CopyUntilAligned(ref source, ref target, ref lengthInBytes, 32);

            for (int i = 0; i + 32 < lengthInBytes; i += 32)
            {
                var ymm0 = Avx2.LoadVector256(source);
                Avx2.StoreAlignedNonTemporal(target, ymm0);

                source += 32;
                target += 32;
            }

            CopyByByte(source, target, toEnd);
        }

        [Benchmark]
        public unsafe void Avx256_StoreNonTemporal_Unrolled2(byte* source, byte* target, int lengthInBytes)
        {
            byte* toEnd = &target[lengthInBytes];
            CopyUntilAligned(ref source, ref target, ref lengthInBytes, 32);

            for (int i = 0; i + 64 < lengthInBytes; i += 64)
            {
                var ymm0 = Avx2.LoadVector256(source);
                var ymm1 = Avx2.LoadVector256(source + 32);

                Avx2.StoreAlignedNonTemporal(target, ymm0);
                Avx2.StoreAlignedNonTemporal(target + 32, ymm1);

                source += 64;
                target += 64;
            }

            CopyByByte(source, target, toEnd);
        }

        [Benchmark]
        public unsafe void Avx256_StoreNonTemporal_Unrolled4(byte* source, byte* target, int lengthInBytes)
        {
            byte* toEnd = &target[lengthInBytes];
            CopyUntilAligned(ref source, ref target, ref lengthInBytes, 32);

            for (int i = 0; i + 128 < lengthInBytes; i += 128)
            {
                var ymm0 = Avx2.LoadVector256(source);
                var ymm1 = Avx2.LoadVector256(source + 32);
                var ymm2 = Avx2.LoadVector256(source + 64);
                var ymm3 = Avx2.LoadVector256(source + 96);

                Avx2.StoreAlignedNonTemporal(target, ymm0);
                Avx2.StoreAlignedNonTemporal(target + 32, ymm1);
                Avx2.StoreAlignedNonTemporal(target + 64, ymm2);
                Avx2.StoreAlignedNonTemporal(target + 96, ymm3);

                source += 128;
                target += 128;
            }

            CopyByByte(source, target, toEnd);
        }

        [Benchmark]
        public unsafe void Avx256_StoreNonTemporal_Unrolled8(byte* source, byte* target, int lengthInBytes)
        {
            byte* toEnd = &target[lengthInBytes];
            CopyUntilAligned(ref source, ref target, ref lengthInBytes, 32);

            float* from = (float*)source;
            float* to = (float*)target;
            int lengthInFloats = lengthInBytes / 4;

            for (int i = 0; i + 64 <= lengthInFloats; i += 64)
            {
                Vector256<float> ymm0 = Avx.LoadVector256(from + 0);
                Vector256<float> ymm1 = Avx.LoadVector256(from + 8);
                Vector256<float> ymm2 = Avx.LoadVector256(from + 16);
                Vector256<float> ymm3 = Avx.LoadVector256(from + 24);
                Vector256<float> ymm4 = Avx.LoadVector256(from + 32);
                Vector256<float> ymm5 = Avx.LoadVector256(from + 40);
                Vector256<float> ymm6 = Avx.LoadVector256(from + 48);
                Vector256<float> ymm7 = Avx.LoadVector256(from + 56);

                Avx.StoreAlignedNonTemporal(to + 0, ymm0);
                Avx.StoreAlignedNonTemporal(to + 8, ymm1);
                Avx.StoreAlignedNonTemporal(to + 16, ymm2);
                Avx.StoreAlignedNonTemporal(to + 24, ymm3);
                Avx.StoreAlignedNonTemporal(to + 32, ymm4);
                Avx.StoreAlignedNonTemporal(to + 40, ymm5);
                Avx.StoreAlignedNonTemporal(to + 48, ymm6);
                Avx.StoreAlignedNonTemporal(to + 56, ymm7);

                from += 64;
                to += 64;
            }

            CopyByByte((byte*)from, (byte*)to, toEnd);
        }

        [Benchmark]
        public unsafe void Avx256_StoreNonTemporal_Unrolled16(byte* source, byte* target, int lengthInBytes)
        {
            byte* toEnd = &target[lengthInBytes];
            CopyUntilAligned(ref source, ref target, ref lengthInBytes, 32);

            float* from = (float*)source;
            float* to = (float*)target;
            int lengthInFloats = lengthInBytes / 4;

            for (int i = 0; i + 128 <= lengthInFloats; i += 128)
            {
                Vector256<float> ymm0 = Avx.LoadVector256(from + 0);
                Vector256<float> ymm1 = Avx.LoadVector256(from + 8);
                Vector256<float> ymm2 = Avx.LoadVector256(from + 16);
                Vector256<float> ymm3 = Avx.LoadVector256(from + 24);
                Vector256<float> ymm4 = Avx.LoadVector256(from + 32);
                Vector256<float> ymm5 = Avx.LoadVector256(from + 40);
                Vector256<float> ymm6 = Avx.LoadVector256(from + 48);
                Vector256<float> ymm7 = Avx.LoadVector256(from + 56);
                Vector256<float> ymm8 = Avx.LoadVector256(from + 64);
                Vector256<float> ymm9 = Avx.LoadVector256(from + 72);
                Vector256<float> ymm10 = Avx.LoadVector256(from + 80);
                Vector256<float> ymm11 = Avx.LoadVector256(from + 88);
                Vector256<float> ymm12 = Avx.LoadVector256(from + 96);
                Vector256<float> ymm13 = Avx.LoadVector256(from + 104);
                Vector256<float> ymm14 = Avx.LoadVector256(from + 112);
                Vector256<float> ymm15 = Avx.LoadVector256(from + 120);

                Avx.StoreAlignedNonTemporal(to + 0, ymm0);
                Avx.StoreAlignedNonTemporal(to + 8, ymm1);
                Avx.StoreAlignedNonTemporal(to + 16, ymm2);
                Avx.StoreAlignedNonTemporal(to + 24, ymm3);
                Avx.StoreAlignedNonTemporal(to + 32, ymm4);
                Avx.StoreAlignedNonTemporal(to + 40, ymm5);
                Avx.StoreAlignedNonTemporal(to + 48, ymm6);
                Avx.StoreAlignedNonTemporal(to + 56, ymm7);
                Avx.StoreAlignedNonTemporal(to + 64, ymm8);
                Avx.StoreAlignedNonTemporal(to + 72, ymm9);
                Avx.StoreAlignedNonTemporal(to + 80, ymm10);
                Avx.StoreAlignedNonTemporal(to + 88, ymm11);
                Avx.StoreAlignedNonTemporal(to + 96, ymm12);
                Avx.StoreAlignedNonTemporal(to + 104, ymm13);
                Avx.StoreAlignedNonTemporal(to + 112, ymm14);
                Avx.StoreAlignedNonTemporal(to + 120, ymm15);

                from += 128;
                to += 128;
            }

            CopyByByte((byte*)from, (byte*)to, toEnd);
        }
    }
}