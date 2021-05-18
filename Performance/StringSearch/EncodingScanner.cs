using System;

namespace StringSearch
{
    public enum FileTypeDetected
    {
        UTF8,
        UnicodeOther,
        Executable,
        Compressed,
        Image,
        OtherNonUtf8
    }

    public struct FileDetectionResult
    {
        public readonly FileTypeDetected Type { get; }
        public readonly int BomByteCount { get; }
        public bool BomFound => (BomByteCount > 0);

        public FileDetectionResult(FileTypeDetected type, int bomByteCount = 0)
        {
            this.Type = type;
            this.BomByteCount = bomByteCount;
        }
    }

    public class FileTypeScanner
    {
        /// <summary>
        ///  Identify tries to identify a file using the initial bytes.
        ///  It checks for byte-order-marks and well known file formats,
        ///  but otherwise checks whether the remaining bytes could be
        ///  valid UTF-8.
        /// </summary>
        /// <param name="bytes">Prefix of file contents to scan</param>
        /// <returns>FileDetectionResult identifying file</returns>
        public static FileDetectionResult Identify(Span<byte> bytes)
        {
            if (TryDetectByteOrderMark(bytes, out FileDetectionResult result))
            {
                return result;
            }

            if (IsInvalidUtf8(bytes))
            {
                return new FileDetectionResult(FileTypeDetected.OtherNonUtf8);
            }

            return new FileDetectionResult(FileTypeDetected.UTF8);
        }

        /// <summary>
        ///  Look for Unicode format byte-order-marks and the first bytes of other common
        ///  file formats, especially ones which are often large files.
        /// </summary>
        /// <param name="bytes">First Bytes from file</param>
        /// <param name="result">FileDetectionResult identifying file and prefix byte length, if found</param>
        /// <returns>True if a known prefix detected, False otherwise</returns>
        private static bool TryDetectByteOrderMark(Span<byte> bytes, out FileDetectionResult result)
        {
            result = default;
            if (bytes.Length < 4) { return false; }

            // Modelling Byte-Order-Mark detection in StreamReader.DetectEncoding
            // https://referencesource.microsoft.com/#mscorlib/system/io/streamreader.cs,ea5187ae9c79350e,references

            if (bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                result = new FileDetectionResult(FileTypeDetected.UTF8, 3);
                return true;
            }
            else if (bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                result = new FileDetectionResult(FileTypeDetected.UnicodeOther, 2);
                return true;
            }
            else if (bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                if (bytes[2] != 0 || bytes[3] != 0)
                {
                    result = new FileDetectionResult(FileTypeDetected.UnicodeOther, 2);
                    return true;
                }
                else
                {
                    result = new FileDetectionResult(FileTypeDetected.UnicodeOther, 4);
                    return true;
                }
            }
            else if (bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0xFE && bytes[3] == 0xFF)
            {
                result = new FileDetectionResult(FileTypeDetected.UnicodeOther, 4);
                return true;
            }
            else if (bytes[0] == 0x5A && bytes[1] == 0x4D && bytes.Length >= 14 && bytes[12] == 0xFF && bytes[13] == 0xFF)
            {
                // PE 'MZ' header, then Dos Stub, or at least illegal UTF-8.
                result = new FileDetectionResult(FileTypeDetected.Executable);
                return true;
            }
            else if (bytes[0] == 0x50 && bytes[1] == 0x4B && bytes[2] == 0x03 && bytes[4] == 0x04)
            {
                // ZIP
                result = new FileDetectionResult(FileTypeDetected.Compressed);
                return true;
            }
            else if (bytes[0] == 0x78 && (bytes[1] == 0x01 || bytes[1] == 0x9C || bytes[1] == 0x5E || bytes[1] == 0xDA))
            {
                // ZLIB (including Git object files)
                result = new FileDetectionResult(FileTypeDetected.Compressed);
                return true;
            }
            else if (bytes[0] == 0x1F && bytes[1] == 0x8B)
            {
                // GZIP
                result = new FileDetectionResult(FileTypeDetected.Compressed);
                return true;
            }
            else if (bytes[0] == 0xFF && bytes[1] == 0xD8)
            {
                // JPG
                result = new FileDetectionResult(FileTypeDetected.Image);
                return true;
            }
            else if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[2] == 0x47)
            {
                // PNG
                result = new FileDetectionResult(FileTypeDetected.Image);
                return true;
            }

            return false;
        }

        /// <summary>
        ///  Looks for invalid UTF-8 byte patterns in the given bytes.
        /// </summary>
        /// <remarks>
        ///  In UTF-8, characters may be one to four bytes long.
        ///  Single byte characters are less than 0x80 (start with a 0 bit).
        ///  Multi-byte characters are 0xC0 or greater (start with 11 bits).
        ///  Continuation bytes (bytes after the first in a character) are 0x80 to 0xBF (start with 10 bits).
        ///  
        ///  Most non-UTF8 data will have a disallowed byte pair early on:
        ///   - A single byte character followed by a continuation,
        ///   - A multi-byte start not followed by a continuation.
        /// </remarks>
        /// <param name="bytes">First Bytes from file to scan</param>
        /// <returns>True if a byte pair invalid in UTF-8 detected, False if bytes could be valid UTF-8</returns>
        private static bool IsInvalidUtf8(Span<byte> bytes)
        {
            // Invalid UTF-8 detection informed by Lemire's "Validating UTF-8 In Less Than One Instruction Per Byte"
            if (bytes.Length < 2) { return false; }

            byte last = bytes[0];

            for (int i = 1; i < bytes.Length; ++i)
            {
                byte current = bytes[i];

                if (IsUtf8ContinuationByte(current))
                {
                    if (IsUtf8SingleByte(last))
                    {
                        return true;
                    }
                }
                else if (IsUtf8MultiByteStart(last))
                {
                    return true;
                }

                last = current;
            }

            return false;
        }

        private static bool IsUtf8SingleByte(byte b)
        {
            return (b < 0x80);
        }

        private static bool IsUtf8ContinuationByte(byte b)
        {
            return (b >= 0x80 && b < 0xC0);
        }

        private static bool IsUtf8MultiByteStart(byte b)
        {
            return (b >= 0xC0);
        }
    }
}
