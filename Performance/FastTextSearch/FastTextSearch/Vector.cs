using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace FastTextSearch
{
    public unsafe static class Vector
    {
        private static Vector256<sbyte> SetVector256To(byte value)
        {
            sbyte* _loader = stackalloc sbyte[32];
            for (int i = 0; i < 32; ++i)
            {
                _loader[i] = unchecked((sbyte)value);
            }

            return Unsafe.Read<Vector256<sbyte>>(_loader);
        }

        public static int CountAndLastIndex(byte b, ReadOnlySpan<byte> content, out int lastIndex)
        {
            lastIndex = -1;
            int count = 0;

            int i = 0;
            int fullBlockEnd = content.Length - 32;

            if (i < fullBlockEnd)
            {
                fixed (byte* contentPtr = &content[0])
                {
                    Vector256<sbyte> newlineV = SetVector256To(b);

                    for (; i < fullBlockEnd; i += 32)
                    {
                        // Load a vector of bytes
                        Vector256<sbyte> contentV = Unsafe.ReadUnaligned<Vector256<sbyte>>(&contentPtr[i]);

                        // Find newlines and convert to a bit vector
                        Vector256<sbyte> matches = Avx2.CompareEqual(newlineV, contentV);
                        uint matchBits = unchecked((uint)Avx2.MoveMask(matches));

                        // Count the newlines seen
                        int thisCount = (int)Popcnt.PopCount(matchBits);
                        count += thisCount;

                        if (thisCount > 0)
                        {
                            int bytesAfterLast = (int)Lzcnt.LeadingZeroCount(matchBits);
                            lastIndex = i + 31 - bytesAfterLast;
                        }
                    }
                }
            }

            // Count matches after the end of the block
            for (; i < content.Length; ++i)
            {
                if (content[i] == b)
                {
                    count++;
                    lastIndex = i;
                }
            }

            return count;
        }
    }
}
