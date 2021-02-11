// Copyright Scott Louvau, MIT License.

using System;
using System.Threading.Tasks;

namespace Copy
{
    public class CopyTestBase
    {
        public const int Length = 64 * 1024 * 1024 - 1;
        private byte[] Source;
        private byte[] Target;

        public CopyTestBase()
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

                CopyByByte(source, target, partitions * slice, lengthInBytes);
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
        protected unsafe void CopyUntilAligned(ref byte* source, ref byte* target, ref int lengthInBytes, int alignment)
        {
            int bytesPastAlignment = (int)((long)target & (alignment - 1));
            if (bytesPastAlignment > 0)
            {
                // Copy bytes until target is aligned to the required byte boundary
                int bytesUntilAligned = alignment - bytesPastAlignment;
                CopyByByte(source, target, target + bytesUntilAligned);

                source += bytesUntilAligned;
                target += bytesUntilAligned;
                lengthInBytes -= bytesUntilAligned;
            }
        }

        // Methods which require target pointer alignment can run this first to copy any unaligned prefix
        protected void CopyUntilAligned(byte[] source, byte[] target, ref int index, ref int lengthInBytes, int alignment)
        {
            int bytesPastAlignment = (int)(index & (alignment - 1));
            if (bytesPastAlignment > 0)
            {
                // Copy bytes until target is aligned to the required byte boundary
                int bytesUntilAligned = alignment - bytesPastAlignment;
                CopyByByte(source, target, index, index + bytesUntilAligned);

                index += bytesUntilAligned;
                lengthInBytes -= bytesUntilAligned;
            }
        }

        // Copy individual bytes, moving pointers as we go
        protected unsafe void CopyByByte(byte* source, byte* target, byte* targetEnd)
        {
            while (target < targetEnd)
            {
                *target++ = *source++;
            }
        }

        // Copy individual bytes (non-unsafe code)
        protected void CopyByByte(byte[] source, byte[] target, int index, int endIndex)
        {
            for (int i = index; i < endIndex; ++i)
            {
                target[i] = source[i];
            }
        }
    }
}