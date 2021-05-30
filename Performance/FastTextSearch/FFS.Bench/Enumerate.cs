using RoughBench.Attributes;
using System;
using System.Collections.Generic;
using System.IO;

namespace FFS.Bench
{
    public class Enumerate
    {
        public const string RootPath = @"C:\CodeSnap";

        // 150 ms
        [Benchmark]
        public void Directory_GetFiles()
        {
            Directory.GetFiles(RootPath, "*.*", SearchOption.AllDirectories);
        }

        // 150 ms
        [Benchmark]
        public void DirectoryInfo_GetFiles()
        {
            DirectoryInfo root = new DirectoryInfo(RootPath);
            root.GetFiles("*.*", SearchOption.AllDirectories);
        }

        // 150 ms
        [Benchmark]
        public void EnumerateAndSplit()
        {
            DirectoryInfo root = new DirectoryInfo(RootPath);

            DateTime cutoff = DateTime.UtcNow.AddDays(-7);
            List<FileInfo> stale = new List<FileInfo>();
            List<FileInfo> fresh = new List<FileInfo>();

            foreach (FileInfo fi in root.GetFiles("*.*", SearchOption.AllDirectories))
            {
                if (fi.LastWriteTimeUtc < cutoff)
                {
                    stale.Add(fi);
                }
                else
                {
                    fresh.Add(fi);
                }
            }
        }
    }
}
