﻿namespace YouTube.Downloader.Utilities
{
    using System;
    using System.Runtime.InteropServices;

    using YouTube.Downloader.Utilities.Interfaces;

    internal class FileSystemUtility : IFileSystemUtility
    {
        public FileSystemUtility()
        {
            Guid downloadsFolderGuid = new Guid("374DE290-123F-4565-9164-39C4925E467B");

            SHGetKnownFolderPath(downloadsFolderGuid, 0, IntPtr.Zero, out string downloadsFolderPath);

            DownloadsFolderPath = downloadsFolderPath;
        }

        public string DownloadsFolderPath { get; }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid guid, uint flags, IntPtr tokenHandle, out string path);
    }
}