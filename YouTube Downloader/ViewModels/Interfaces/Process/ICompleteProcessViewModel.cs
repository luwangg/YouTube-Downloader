﻿namespace YouTube.Downloader.ViewModels.Interfaces.Process
{
    internal interface ICompleteProcessViewModel : IProcessViewModel
    {
        void Initialise(IVideoViewModel videoViewModel);
    }
}