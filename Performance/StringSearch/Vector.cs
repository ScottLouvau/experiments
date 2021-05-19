using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace StringSearch
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

        private static Vector128<sbyte> SetVector128To(byte value)
        {
            sbyte* _loader = stackalloc sbyte[16];
            for (int i = 0; i < 16; ++i)
            {
                _loader[i] = unchecked((sbyte)value);
            }

            return Unsafe.Read<Vector128<sbyte>>(_loader);
        }

        public static int NewlineCount(ReadOnlySpan<byte> content, out int lastNewlineIndex)
        {
            lastNewlineIndex = -1;
            int newlineCount = 0;

            int i = 0;
            int fullBlockLength = content.Length - 32;

            if (i < fullBlockLength)
            {
                fixed (byte* contentPtr = &content[0])
                {
                    Vector256<sbyte> newlineV = SetVector256To((byte)'\n');

                    for (; i < fullBlockLength; i += 32)
                    {
                        // Load a vector of bytes
                        Vector256<sbyte> contentV = Unsafe.ReadUnaligned<Vector256<sbyte>>(&contentPtr[i]);

                        // Find newlines and convert to a bit vector
                        Vector256<sbyte> matches = Avx2.CompareEqual(newlineV, contentV);
                        uint newlineBits = unchecked((uint)Avx2.MoveMask(matches));

                        // Count the newlines seen
                        int lineCount = (int)Popcnt.PopCount(newlineBits);
                        newlineCount += lineCount;

                        if (lineCount > 0)
                        {
                            lastNewlineIndex = i + (int)Bmi1.TrailingZeroCount(newlineBits);
                        }
                    }
                }
            }

            // Count newlines after the end of the block
            for (; i < content.Length; ++i)
            {
                if (content[i] == (byte)'\n')
                {
                    newlineCount++;
                    lastNewlineIndex = i;
                }
            }

            return newlineCount;
        }
    }
}
