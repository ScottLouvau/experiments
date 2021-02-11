// Copyright Scott Louvau, MIT License.

using RoughBench.Attributes;
using System.Runtime.Intrinsics.X86;

namespace Copy
{
    public class CopyAvx : CopyTestBase
    {
        [Benchmark]
        public unsafe void Avx128_LoadStore(byte* source, byte* target, int lengthInBytes)
        {
            byte* toEnd = &target[lengthInBytes];

            for (int i = 0; i + 16 <= lengthInBytes; i += 16)
            {
                Avx.Store(target, Avx.LoadVector128(source));

                source += 16;
                target += 16;
            }

            CopyByByte(source, target, toEnd);
        }

        [Benchmark]
        public unsafe void Avx256_LoadStore(byte* source, byte* target, int lengthInBytes)
        {
            byte* toEnd = &target[lengthInBytes];

            for (int i = 0; i + 32 <= lengthInBytes; i += 32)
            {
                Avx2.Store(target, Avx2.LoadVector256(source));

                source += 32;
                target += 32;
            }

            CopyByByte(source, target, toEnd);
        }
    }
}