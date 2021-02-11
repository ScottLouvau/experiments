// Copyright Scott Louvau, MIT License.

using RoughBench.Attributes;
using System.Runtime.Intrinsics.X86;

namespace Copy
{
    public class CopyNonTemporal : CopyTestBase
    {
        [Benchmark]
        public unsafe void StoreNonTemporalInt(byte* source, byte* target, int lengthInBytes)
        {
            byte* toEnd = &target[lengthInBytes];

            uint* from = (uint*)source;
            uint* to = (uint*)target;
            int length = lengthInBytes / 4;

            for (int i = 0; i < length; ++i)
            {
                Avx.StoreNonTemporal(&to[i], from[i]);
            }

            CopyByByte((byte*)&from[length], (byte*)&to[length], toEnd);
        }

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
    }
}