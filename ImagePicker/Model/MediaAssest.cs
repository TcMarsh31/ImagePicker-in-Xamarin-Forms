using System;
using Xamarin.Forms;

namespace ImagePicker.Model
{
    public class MediaAssest
    {
        //Image,Video
        public enum MediaAssetType
        {
            Image, Video
        }


        public string Id { get; set; }
        public string Name { get; set; }
        public MediaAssetType Type { get; set; }
        public string PreviewPath { get; set; }
        public string Path { get; set; }
        public bool IsSelectable { get; set; }
        public Frame frame { get; set; }
    }
}
