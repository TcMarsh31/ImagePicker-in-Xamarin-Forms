using System;
namespace ImagePicker.Model
{
    public class MediaEventArgs
    {
        public MediaAssest Media { get; }
        public MediaEventArgs(MediaAssest media)
        {
            Media = media;
        }
    }
}
