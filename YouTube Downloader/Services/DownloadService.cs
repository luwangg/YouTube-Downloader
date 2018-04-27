﻿namespace YouTube.Downloader.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using Caliburn.Micro;

    using YouTube.Downloader.Helpers;
    using YouTube.Downloader.Models;
    using YouTube.Downloader.Services.Interfaces;
    using YouTube.Downloader.ViewModels.Interfaces;

    internal class DownloadService : IDownloadService
    {
        private const int MaxConcurrentDownloads = 3;

        private static readonly Regex ProgressReportRegex = new Regex(@"^\[download] (?<ProgressPercentage>[ 1][ 0-9][0-9]\.[0-9])% of .*?(?<TotalDownloadSize>[\d\.]+)?MiB at  (?<DownloadSpeed>.+)MiB\/s");

        private readonly Queue<IDownloadViewModel> _downloadQueue = new Queue<IDownloadViewModel>();

        private readonly List<Download> _currentDownloads = new List<Download>();

        private readonly ISettingsService _settingsService;

        public DownloadService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public void QueueDownloads(IEnumerable<IDownloadViewModel> downloads)
        {
            foreach (IDownloadViewModel download in downloads)
            {
                _downloadQueue.Enqueue(download);
            }

            for (int downloadsStarted = _currentDownloads.Count; downloadsStarted < MaxConcurrentDownloads && _downloadQueue.Count > 0; ++downloadsStarted)
            {
                DownloadNext();
            }
        }

        public void TerminateAllDownloads()
        {
            _currentDownloads.Apply(download => download.Kill());
            _currentDownloads.Clear();
        }

        private static void MonitorOutput(Process process, DownloadProgress downloadProgress, DownloadType downloadType)
        {
            Task.Run(() =>
            {
                int stage = 0;

                downloadProgress.StatusText = "Gathering Data";

                using (StreamReader processStreamReader = process.StandardOutput)
                {
                    while (!processStreamReader.EndOfStream)
                    {
                        Match match = ProgressReportRegex.Match(processStreamReader.ReadLine());

                        if (!match.Success) continue;

                        if (stage == 0)
                        {
                            ++stage;
                            downloadProgress.StatusText = downloadType == DownloadType.Audio ? "Downloading Audio" : "Downloading Video";
                        }

                        downloadProgress.DownloadSpeed = double.Parse(match.Groups["DownloadSpeed"].Value);

                        double newProgressPercentage = double.Parse(match.Groups["ProgressPercentage"].Value);

                        if (downloadProgress.TotalDownloadSize == 0 || newProgressPercentage < downloadProgress.ProgressPercentage)
                        {
                            downloadProgress.TotalDownloadSize = double.Parse(match.Groups["TotalDownloadSize"].Value);
                        }
                        else if (newProgressPercentage == 100)
                        {
                            switch (++stage)
                            {
                                case 2:
                                    downloadProgress.StatusText = downloadType == DownloadType.Audio ? "Finalising" : "Downloading Audio";
                                    break;

                                case 3:
                                    downloadProgress.StatusText = "Finalising";
                                    break;
                            }
                        }

                        downloadProgress.ProgressPercentage = newProgressPercentage;
                    }
                }
            });
        }

        private void DownloadNext()
        {
            if (_downloadQueue.Count == 0)
            {
                return;
            }

            IDownloadViewModel downloadViewModel = _downloadQueue.Dequeue();
            downloadViewModel.DownloadState = DownloadState.Downloading;

            //Process downloadProcess = new Process
            //{
            //    EnableRaisingEvents = true,
            //    StartInfo = new ProcessStartInfo("Resources/youtube-dl.exe", $"-o \"{_settingsService.Settings.DownloadPath}/%(title)s.%(ext)s\" -f {(_settingsService.Settings.DownloadType == DownloadType.Audio ? "bestaudio" : "bestvideo+bestaudio")} \"{download.VideoViewModel.Video.Id}\"")
            //    {
            //        CreateNoWindow = true,
            //        UseShellExecute = false,
            //        RedirectStandardOutput = true
            //    }
            //};

            Download download = new Download(downloadViewModel.VideoViewModel.Video, _settingsService.Settings);
            downloadViewModel.Download = download;

            void DownloadCompleted(object sender, EventArgs e)
            {
                DetachDownload();
            }

            void DownloadPaused(object sender, EventArgs e)
            {
                downloadViewModel.DownloadState = DownloadState.Paused;
            }

            void DownloadResumed(object sender, EventArgs e)
            {
                downloadViewModel.DownloadState = DownloadState.Downloading;
            }

            void DownloadKilled(object sender, EventArgs e)
            {
                DetachDownload();
            }

            void DetachDownload()
            {
                download.Completed -= DownloadCompleted;
                download.Paused -= DownloadPaused;
                download.Resumed -= DownloadResumed;
                download.Killed -= DownloadKilled;

                _currentDownloads.Remove(download);

                downloadViewModel.DownloadState = DownloadState.Completed;
            }

            download.Completed += DownloadCompleted;
            download.Paused += DownloadPaused;
            download.Resumed += DownloadResumed;
            download.Killed += DownloadKilled;

            download.Start();

            _currentDownloads.Add(download);

            DownloadProgress downloadProgress = new DownloadProgress();
            downloadViewModel.DownloadProgress = downloadProgress;

            MonitorOutput(download.Process, downloadProgress, _settingsService.Settings.DownloadType);
        }
    }
}