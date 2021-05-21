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
            if (Avx2.IsSupported == false)
            {
                return Utf8.CodepointCount(content);
            }

            if (content == null)
            {
                return 0;
            }

            int count = 0;
            int i = 0;
            int fullBlockEnd = content.Length - 31;

            if (i < fullBlockEnd)
            {
                fixed (byte* contentPtr = &content[0])
                {
                    Vector256<sbyte> cutoff = SetVector256To(0xBF);

                    for (; i < fullBlockEnd; i += 32)
                    {
                        // Load a vector of bytes
                        Vector256<sbyte> contentV = Unsafe.ReadUnaligned<Vector256<sbyte>>(&contentPtr[i]);

                        // Look for bytes not between 0x80 and 0xBF.
                        // This is a signed byte (sbyte) operation, so 0x80 is -128 and 0xBF = -64.
                        // GreaterThan 0xBF means 0xC0-0xFF and 0x00 - 0x7F.
                        Vector256<sbyte> starts = Avx2.CompareGreaterThan(contentV, cutoff);
                        uint startBits = unchecked((uint)Avx2.MoveMask(starts));
                        count += (int)Popcnt.PopCount(startBits);
                    }
                }
            }

            // Search the last partial block
            for (; i < content.Length; ++i)
            {
                if (content[i] < 0x80 || content[i] > 0xBF)
                {
                    count++;
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
