// Copyright (c) Scott Louvau. All rights reserved.
// Licensed under the MIT License.

using System;

namespace FastTextSearch
{
    public enum FileTypeDetected : byte
    {
        OtherNonUtf8,       // File Type could not be classified but is invalid UTF-8
        UTF8,               // UTF-8 BOM detected, or all bytes scanned were potentially valid UTF-8
        UnicodeOther,       // UTF-16 or UTF-32 BOM detected
        Executable,         // DLL or EXE (Windows Portable Executable format) identified
        DebugSymbols,       // PDB
        Compressed,         // ZIP, CAB, GZIP, or ZLIB compressed content found
        Image               // PNG or JPG image found
    }

    public struct FileSniffResult
    {
        public readonly FileTypeDetected Type { get; }
        public readonly int BomByteCount { get; }
        public bool BomFound => (BomByteCount > 0);

        public FileSniffResult(FileTypeDetected type, int bomByteCount = 0)
        {
            this.Type = type;
            this.BomByteCount = bomByteCount;
        }
    }

    public class FileSniffer
    {

        /// <summary>
        ///  Identify tries to identify a file using the initial bytes.
        ///  It checks for byte-order-marks and well known file formats,
        ///  but otherwise checks whether the remaining bytes could be
        ///  valid UTF-8.
        /// </summary>
        /// <param name="bytes">Prefix of file contents to scan</param>
        /// <returns>FileDetectionResult identifying file</returns>
        public static FileSniffResult Sniff(Span<byte> bytes)
        {
            if (TryDetectByteOrderMark(bytes, out FileSniffResult result))
            {
                return result;
            }

            if (Utf8.IsInvalidSequence(bytes))
            {
                return new FileSniffResult(FileTypeDetected.OtherNonUtf8);
            }

            return new FileSniffResult(FileTypeDetected.UTF8);
        }

        /// <summary>
        ///  Look for Unicode format byte-order-marks and the first bytes of other common
        ///  file formats, especially ones which are often large files.
        /// </summary>
        /// <param name="bytes">First Bytes from file</param>
        /// <param name="result">FileDetectionResult identifying file and prefix byte length, if found</param>
        /// <returns>True if a known prefix detected, False otherwise</returns>
        public static bool TryDetectByteOrderMark(Span<byte> bytes, out FileSniffResult result)
        {
            result = default;
            if (bytes == null || bytes.Length < 4) { return false; }

            // Modelling Byte-Order-Mark detection in StreamReader.DetectEncoding
            // https://referencesource.microsoft.com/#mscorlib/system/io/streamreader.cs,ea5187ae9c79350e,references

            if (bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                result = new FileSniffResult(FileTypeDetected.UTF8, 3);
                return true;
            }
            else if (bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                result = new FileSniffResult(FileTypeDetected.UnicodeOther, 2);
                return true;
            }
            else if (bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                if (bytes[2] != 0 || bytes[3] != 0)
                {
                    result = new FileSniffResult(FileTypeDetected.UnicodeOther, 2);
                    return true;
                }
                else
                {
                    result = new FileSniffResult(FileTypeDetected.UnicodeOther, 4);
                    return true;
                }
            }
            else if (bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0xFE && bytes[3] == 0xFF)
            {
                result = new FileSniffResult(FileTypeDetected.UnicodeOther, 4);
                return true;
            }
            else if (bytes[0] == 0x4D && bytes[1] == 0x5A && bytes.Length >= 14 && bytes[12] == 0xFF && bytes[13] == 0xFF)
            {
                // PE: 'MZ', then DOS stub.
                result = new FileSniffResult(FileTypeDetected.Executable);
                return true;
            }
            else if (bytes[0] == 0x42 && bytes[1] == 0x53 && bytes[2] == 0x4A && bytes[3] == 0x42 && bytes.Length >= 6 && bytes[4] == 0x01 && bytes[5] == 0x00)
            {
                // PDB (Portable PDB): 'BSJB' 0x01 0x00
                result = new FileSniffResult(FileTypeDetected.DebugSymbols);
                return true;
            }
            else if (bytes[0] == 0x4D && bytes[1] == 0x69 && bytes[2] == 0x63 && bytes.Length >= 30 && bytes[26] == 0x1A && bytes[29] == 0x00)
            {
                // PDB (Older): 'Microsoft C/C++ MSF 7.00\r\n' 0x1A 'DS' 0x0000000
                result = new FileSniffResult(FileTypeDetected.DebugSymbols);
                return true;
            }
            else if (bytes[0] == 0x50 && bytes[1] == 0x4B && bytes[2] == 0x03 && bytes[3] == 0x04)
            {
                // ZIP: 'PK' 0x03 0x04
                result = new FileSniffResult(FileTypeDetected.Compressed);
                return true;
            }
            else if (bytes[0] == 0x78 && (bytes[1] == 0x01 || bytes[1] == 0x9C || bytes[1] == 0x5E || bytes[1] == 0xDA))
            {
                // ZLIB: 0x78 [0x01 | 0x9C | 0x5E | 0xDA] (compression levels)
                //  (covers Git object files, which are plentiful in an active repo)
                result = new FileSniffResult(FileTypeDetected.Compressed);
                return true;
            }
            else if (bytes[0] == 0x1F && bytes[1] == 0x8B)
            {
                // GZIP: 0x1F 0x8B
                result = new FileSniffResult(FileTypeDetected.Compressed);
                return true;
            }
            else if (bytes[0] == 0x4D && bytes[1] == 0x53 && bytes[2] == 0x43 && bytes[3] == 0x46 && bytes.Length >= 6 && bytes[4] == 0x00 && bytes[5] == 0x00)
            {
                // CAB: 'MSCF' 0x00000000
                result = new FileSniffResult(FileTypeDetected.Compressed);
                return true;
            }
            else if (bytes[0] == 0xFF && bytes[1] == 0xD8)
            {
                // JPG: 0xFF 0xD8
                result = new FileSniffResult(FileTypeDetected.Image);
                return true;
            }
            else if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            {
                // PNG: 0x89 'PNG' 
                result = new FileSniffResult(FileTypeDetected.Image);
                return true;
            }

            return false;
        }
    }
}
