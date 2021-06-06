using RoughBench.Attributes;
using System;
using System.Collections.Generic;
using System.IO;

namespace FFS.Bench
{
    public class Enumerate
    {
        public string RootPath { get; private set; } = @"C:\CodeSnap";

        // 75 ms
        [Benchmark]
        public void Directory_GetFiles()
        {
            Directory.GetFiles(RootPath, "*.*", SearchOption.AllDirectories);
        }

        // 75 ms
        [Benchmark]
        public void DirectoryInfo_GetFiles()
        {
            DirectoryInfo root = new DirectoryInfo(RootPath);
            root.GetFiles("*.*", SearchOption.AllDirectories);
        }

        // 155 ms (2x GetFiles AllDirectories)
        [Benchmark]
        public void DirectoryInfo_GetFilesRecursive()
        {
            List<FileInfo> results = new List<FileInfo>();
            DirectoryInfo_GetFilesRecursive(new DirectoryInfo(RootPath), results);
        }

        private void DirectoryInfo_GetFilesRecursive(DirectoryInfo under, List<FileInfo> files)
        {
            files.AddRange(under.GetFiles());

            foreach(DirectoryInfo subfolder in under.GetDirectories())
            {
                DirectoryInfo_GetFilesRecursive(subfolder, files);
            }
        }

        // 81 ms
        [Benchmark]
        public void FileSystemInfo_GetFilesRecursive()
        {
            List<FileInfo> results = new List<FileInfo>();
            FileSystemInfo_GetFilesRecursive(new DirectoryInfo(RootPath), results);
        }

        private void FileSystemInfo_GetFilesRecursive(DirectoryInfo under, List<FileInfo> files)
        {
            FileSystemInfo[] children = under.GetFileSystemInfos();

            foreach (FileSystemInfo child in children)
            {
                if (child is FileInfo)
                {
                    files.Add((FileInfo)child);
                }
            }

            foreach (FileSystemInfo child in children)
            {
                if (child is DirectoryInfo)
                {
                    FileSystemInfo_GetFilesRecursive((DirectoryInfo)child, files);
                }
            }
        }

        // TopDirectoryOnly: 25 us
        // AllDirectories: 72 ms
        [Benchmark]
        public void DirectoryInfo_GetDirectories()
        {
            DirectoryInfo root = new DirectoryInfo(RootPath);
            root.GetDirectories("*.*", SearchOption.TopDirectoryOnly);
        }

        // 75 ms
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
