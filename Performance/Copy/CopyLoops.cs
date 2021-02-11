// Copyright Scott Louvau, MIT License.

using RoughBench.Attributes;
using System.Runtime.CompilerServices;

namespace Copy
{
    public class CopyLoops : CopyTestBase
    {
        [Benchmark]
        public void ForByte(byte[] source, byte[] target, int index, int lengthInBytes)
        {
            for (int i = index; i < index + lengthInBytes; ++i)
            {
                target[i] = source[i];
            }
        }

        [Benchmark]
        public void ForUnsafeAsLong(byte[] source, byte[] target, int index, int lengthInBytes)
        {
            long[] from = Unsafe.As<long[]>(source);
            long[] to = Unsafe.As<long[]>(target);

            CopyUntilAligned(source, target, ref index, ref lengthInBytes, 8);

            int start = index / 8;
            int length = lengthInBytes / 8;

            for (int i = start; i < start + length; ++i)
            {
                to[i] = from[i];
            }

            CopyByByte(source, target, index + length * 8, index + lengthInBytes);
        }

        [Benchmark]
        public unsafe void UnsafeForLong(byte* source, byte* target, int lengthInBytes)
        {
            long* from = (long*)source;
            long* to = (long*)target;
            long* toEnd = (long*)(&target[lengthInBytes]);

            int length = lengthInBytes / 8;

            for (int i = 0; i < length; ++i)
            {
                *to++ = *from++;
            }

            CopyByByte((byte*)from, (byte*)to, (byte*)toEnd);
        }

        [Benchmark]
        public unsafe void UnsafeWhileLong(byte* source, byte* target, int lengthInBytes)
        {
            long* from = (long*)source;
            long* to = (long*)target;
            long* toEnd = (long*)(&target[lengthInBytes]);

            while (to < toEnd)
            {
                *to++ = *from++;
            }

            CopyByByte((byte*)from, (byte*)to, (byte*)toEnd);
        }
    }
}