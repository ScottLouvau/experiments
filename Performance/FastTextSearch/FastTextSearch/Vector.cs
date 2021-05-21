// Copyright (c) Scott Louvau. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace FastTextSearch
{
    public unsafe static class Vector
    {
        /* Vector Operation Notes
         * ======================
         *  - These operations use AVX2 with 256-bit vector registers.
         *  - AVX2 is implemented in Intel CPUs since Haswell (2013) and AMD CPUs since Excavator (2015)
         *  - AVX2 byte operations treat each byte as a signed value from -128 (0x80) to 127 (0x7F).
         *  
         *  - AVX2 byte operations produce a vector output, with all zero byte for false and an all one byte (-1) for true.
         *  - Use Avx2.MoveMask to turn this set of bytes into a set of bits. The lowest bit corresponds to the first input byte.
         *  
         *  - UTF-8 continuation bytes are 0x80 - 0xBF, so all signed bytes > 0xBF are codepoint start bytes.
         */

        private const byte BiggestContinuation = 0xBF;

        private static Vector256<sbyte> SetVector256To(byte value)
        {
            sbyte* _loader = stackalloc sbyte[32];
            for (int i = 0; i < 32; ++i)
            {
                _loader[i] = unchecked((sbyte)value);
            }

            return Unsafe.Read<Vector256<sbyte>>(_loader);
        }

        public static int CodepointCount(ReadOnlySpan<byte> content)
        {
            if (Avx2.IsSupported == false || content.Length < 32)
            {
                return Utf8.CodepointCount(content);
            }

            int count = 0;
            int i = 0;
            int fullBlockEnd = content.Length - 31;

            fixed (byte* contentPtr = &content[0])
            {
                Vector256<sbyte> continuations = SetVector256To(BiggestContinuation);

                // Count full blocks
                for (; i < fullBlockEnd; i += 32)
                {
                    // Load a vector of bytes
                    Vector256<sbyte> contentV = Unsafe.ReadUnaligned<Vector256<sbyte>>(&contentPtr[i]);

                    // Count codepoint start bytes
                    Vector256<sbyte> starts = Avx2.CompareGreaterThan(contentV, continuations);
                    uint startBits = unchecked((uint)Avx2.MoveMask(starts));
                    count += (int)Popcnt.PopCount(startBits);
                }

                // Count the last partial block
                if (i < content.Length)
                {
                    // Load the last 32 bytes (including some previously counted)
                    Vector256<sbyte> contentV = Unsafe.ReadUnaligned<Vector256<sbyte>>(&contentPtr[content.Length - 32]);

                    // Count codepoint start bytes
                    Vector256<sbyte> starts = Avx2.CompareGreaterThan(contentV, continuations);
                    uint startBits = unchecked((uint)Avx2.MoveMask(starts));

                    // Screen away bytes counted in the prior loop (the low bits).
                    int bitsToInclude = (content.Length - i);
                    startBits = startBits >> (32 - bitsToInclude);

                    // Add the last set of starting bytes
                    count += (int)Popcnt.PopCount(startBits);
                }
            }

            return count;
        }

        public static int CountAndLastIndex(byte b, ReadOnlySpan<byte> content, out int lastIndex)
        {
            if (Avx2.IsSupported == false)
            {
                return Utf8.CountAndLastIndex(b, content, out lastIndex);
            }

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

                        // Find matches and convert to a bit vector
                        Vector256<sbyte> matches = Avx2.CompareEqual(newlineV, contentV);
                        uint matchBits = unchecked((uint)Avx2.MoveMask(matches));

                        // Count the matches seen
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

        public static int IndexOf(byte b, ReadOnlySpan<byte> content)
        {
            if (Avx2.IsSupported == false)
            {
                return content.IndexOf(b);
            }

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

                        // Look for the desired byte
                        Vector256<sbyte> matches = Avx2.CompareEqual(newlineV, contentV);
                        uint matchBits = unchecked((uint)Avx2.MoveMask(matches));

                        // Return the index if found
                        if (matchBits != 0)
                        {
                            int firstIndex = (int)Bmi1.TrailingZeroCount(matchBits);
                            return i + firstIndex;
                        }
                    }
                }
            }

            // Search the last partial block
            for (; i < content.Length; ++i)
            {
                if (content[i] == b)
                {
                    return i;
                }
            }

            return -1;
        }

        public static int IndexOf(ReadOnlySpan<byte> valueToFind, ReadOnlySpan<byte> content)
        {
            if (valueToFind.Length < 3 || Avx2.IsSupported == false)
            {
                return content.IndexOf(valueToFind);
            }

            int i = 0;
            int fullBlockEnd = content.Length - 32;

            if (i < fullBlockEnd)
            {
                fixed (byte* contentPtr = &content[0])
                {
                    Vector256<sbyte> v0 = SetVector256To(valueToFind[0]);
                    Vector256<sbyte> v1 = SetVector256To(valueToFind[1]);
                    Vector256<sbyte> v2 = SetVector256To(valueToFind[2]);

                    for (; i < fullBlockEnd; i += 29)
                    {
                        // Load a vector of bytes
                        Vector256<sbyte> contentV = Unsafe.ReadUnaligned<Vector256<sbyte>>(&contentPtr[i]);

                        // Look for the first three bytes of the valueToFind
                        Vector256<sbyte> match0 = Avx2.CompareEqual(v0, contentV);
                        uint bits0 = unchecked((uint)Avx2.MoveMask(match0));

                        Vector256<sbyte> match1 = Avx2.CompareEqual(v1, contentV);
                        uint bits1 = unchecked((uint)Avx2.MoveMask(match1));

                        Vector256<sbyte> match2 = Avx2.CompareEqual(v2, contentV);
                        uint bits2 = unchecked((uint)Avx2.MoveMask(match2));

                        uint bitsAll = bits0 & (bits1 >> 1) & (bits2 >> 2);

                        if (bitsAll != 0)
                        {
                            int firstIndex = (int)Bmi1.TrailingZeroCount(bitsAll);
                            if (content.Slice(i + firstIndex).StartsWith(valueToFind))
                            {
                                return i + firstIndex;
                            }

                            // Resume search just after this non-match
                            i = i - 29 + firstIndex + 1;
                        }
                    }
                }
            }

            int matchIndex = content.Slice(i).IndexOf(valueToFind);
            return (matchIndex == -1 ? -1 : i + matchIndex);
        }
    }
}
