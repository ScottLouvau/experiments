// Copyright Scott Louvau, MIT License.

using RoughBench.Attributes;
using System;
using System.Runtime.CompilerServices;

namespace Copy
{
    public class CopyBuiltIns : CopyTestBase
    {
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
        public void UnsafeCopyBlock(byte[] source, byte[] target, int index, int lengthInBytes)
        {
            Unsafe.CopyBlock(ref source[index], ref target[index], (uint)lengthInBytes);
        }

        [Benchmark]
        public void UnsafeCopyBlockUnaligned(byte[] source, byte[] target, int index, int lengthInBytes)
        {
            Unsafe.CopyBlockUnaligned(ref source[index], ref target[index], (uint)lengthInBytes);
        }

        [Benchmark]
        public void AsSpanCopy(byte[] source, byte[] target, int index, int lengthInBytes)
        {
            source.AsSpan().Slice(index, lengthInBytes).CopyTo(target.AsSpan().Slice(index, lengthInBytes));
        }
    }
}