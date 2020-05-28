using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetsLibrary;
using AVFoundation;
using CoreGraphics;
using Foundation;
using ImagePicker.DependencyService;
using ImagePicker.iOS.DependencyServiceImplementation;
using ImagePicker.Model;
using Photos;
using UIKit;
using Xamarin.Forms;
using static ImagePicker.Model.MediaAssest;

[assembly: Dependency(typeof(MediaServiceImplementation))]
namespace ImagePicker.iOS.DependencyServiceImplementation
{
    public class MediaServiceImplementation : IMediaService
    {
        bool requestStop = false;

        bool _isLoading = false;
        public bool IsLoading => _isLoading;

        public event EventHandler<MediaEventArgs> OnMediaAssetLoaded;
        static TaskCompletionSource<string> cameraImagePath;
        public MediaServiceImplementation()
        {
        }

        public async Task<bool> RequestPermissionAsync()
        {
            var status = PHPhotoLibrary.AuthorizationStatus;

            bool authotization = status == PHAuthorizationStatus.Authorized;

            if (!authotization)
            {
                authotization = await PHPhotoLibrary.RequestAuthorizationAsync() == PHAuthorizationStatus.Authorized;
            }
            return authotization;

        }

        CancellationToken? CancelToken;
        public async Task<IList<MediaAssest>> RetrieveMediaAssetsAsync(CancellationToken? token = null)
        {
            CancelToken = token;
            requestStop = false;

            if (!CancelToken.HasValue)
                CancelToken = CancellationToken.None;

            // We create a TaskCompletionSource of decimal
            var taskCompletionSource = new TaskCompletionSource<IList<MediaAssest>>();

            // Registering a lambda into the cancellationToken
            CancelToken.Value.Register(() =>
            {
                requestStop = true;
                taskCompletionSource.SetCanceled();
            });

            _isLoading = true;

            var task = LoadMediaAsync();

            // Wait for the first task to finish among the two
            var completedTask = await Task.WhenAny(task, taskCompletionSource.Task);
            _isLoading = false;

            return await completedTask;
        }

        async Task<IList<MediaAssest>> LoadMediaAsync()
        {
            IList<MediaAssest> assets = new List<MediaAssest>();
            var imageManager = new PHCachingImageManager();
            var hasPermission = await RequestPermissionAsync();
            if (hasPermission)
            {
                await Task.Run(async () =>
                {
                    var thumbnailRequestOptions = new PHImageRequestOptions();
                    thumbnailRequestOptions.ResizeMode = PHImageRequestOptionsResizeMode.Fast;
                    thumbnailRequestOptions.DeliveryMode = PHImageRequestOptionsDeliveryMode.FastFormat;
                    thumbnailRequestOptions.NetworkAccessAllowed = true;
                    thumbnailRequestOptions.Synchronous = true;

                    var requestOptions = new PHImageRequestOptions();
                    requestOptions.ResizeMode = PHImageRequestOptionsResizeMode.Exact;
                    requestOptions.DeliveryMode = PHImageRequestOptionsDeliveryMode.HighQualityFormat;
                    requestOptions.NetworkAccessAllowed = true;
                    requestOptions.Synchronous = true;

                    var fetchOptions = new PHFetchOptions();
                    fetchOptions.SortDescriptors = new NSSortDescriptor[] { new NSSortDescriptor("creationDate", false) };
                    fetchOptions.Predicate = NSPredicate.FromFormat($"mediaType == {(int)PHAssetMediaType.Image}");

                    //fetchOptions.Predicate = NSPredicate.FromFormat($"mediaType == {(int)PHAssetMediaType.Image} || mediaType == {(int)PHAssetMediaType.Video}");
                    var fetchResults = PHAsset.FetchAssets(fetchOptions);
                    var tmpPath = Path.GetTempPath();
                    var allAssets = fetchResults.Select(p => p as PHAsset).ToArray();
                    var thumbnailSize = new CGSize(1000.0f, 1000.0f);

                    imageManager.StartCaching(allAssets, thumbnailSize, PHImageContentMode.AspectFit, thumbnailRequestOptions);
                    //imageManager.StartCaching(allAssets, PHImageManager.MaximumSize, PHImageContentMode.AspectFit, requestOptions);


                    foreach (var result in fetchResults)
                    {
                        if(CancelToken.Value.IsCancellationRequested){
                            break;
                        }
                        var phAsset = (result as PHAsset);
                        var name = PHAssetResource.GetAssetResources(phAsset)?.FirstOrDefault()?.OriginalFilename;
                        var asset = new MediaAssest()
                        {
                            Id = phAsset.LocalIdentifier,
                            Name = name,
                            Type = phAsset.MediaType == PHAssetMediaType.Image ? MediaAssetType.Image : MediaAssetType.Video,
                            IsSelectable = true,

                        };

                        imageManager.RequestImageForAsset(phAsset, thumbnailSize, PHImageContentMode.Default, thumbnailRequestOptions, (image, info) =>
                        {

                            if (image != null)
                            {
                                NSData imageData = null;
                                if (image.CGImage.RenderingIntent == CGColorRenderingIntent.Default)
                                {
                                    imageData = image.AsJPEG(0.8f);

                                }
                                else
                                {
                                    imageData = image.AsPNG();
                                }

                                if (imageData != null)
                                {

                                    var fileName = Path.Combine(tmpPath, $"tmp_thumbnail_{Path.GetFileNameWithoutExtension(name)}.jpg");
                                    NSError error = null;
                                    imageData.Save(fileName, true, out error);
                                    if (error == null)
                                    {


                                        asset.PreviewPath = fileName;

                                    }

                                }
                            }
                        });
                        switch (phAsset.MediaType)
                        {

                            case PHAssetMediaType.Image:

                                imageManager.RequestImageForAsset(phAsset, PHImageManager.MaximumSize, PHImageContentMode.AspectFit, requestOptions, (image, info) =>
                                {

                                    if (image != null)
                                    {
                                        NSData imageData = null;
                                        if (image.CGImage.RenderingIntent == CGColorRenderingIntent.Default)
                                        {
                                            imageData = image.AsJPEG(0.8f);

                                        }
                                        else
                                        {
                                            imageData = image.AsPNG();
                                        }

                                        if (imageData != null)
                                        {
                                            var fileName = Path.Combine(tmpPath, $"tmp_{name}");
                                            NSError error = null;
                                            imageData.Save(fileName, true, out error);
                                            if (error == null)
                                            {
                                                asset.Path = fileName;
                                            }

                                        }
                                    }
                                });
                                break;
                            case PHAssetMediaType.Video:
                                var videoRequestOptions = new PHVideoRequestOptions();
                                videoRequestOptions.NetworkAccessAllowed = true;
                                var tcs = new TaskCompletionSource<bool>();
                                imageManager.RequestAvAsset(phAsset, null, (vAsset, audioMix, info) =>
                                {
                                    var avAsset = vAsset as AVUrlAsset;
                                    var avData = NSData.FromUrl(avAsset.Url);
                                    NSError error = null;
                                    var path = Path.Combine(tmpPath, $"tmp_{name}");
                                    avData.Save(path, true, out error);
                                    if (error == null)
                                    {
                                        asset.Path = path;


                                        tcs.TrySetResult(true);
                                    }
                                    else
                                    {
                                        tcs.TrySetResult(false);
                                    }
                                });
                                await tcs.Task;
                                break;
                        }

                        if (CancelToken.Value.IsCancellationRequested)
                        {
                            break;
                        }
                        else
                        {
                            UIApplication.SharedApplication.InvokeOnMainThread(delegate
                            {


                                OnMediaAssetLoaded?.Invoke(this, new MediaEventArgs(asset));

                            });
                        }
                        assets.Add(asset);

                        if (requestStop)
                            break;
                    }
                });

                imageManager.StopCaching();
            }

            return assets;
        }

        string fileName;
        string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        async public Task<string> StoreProfileImage(string filepath)
        { 
            fileName = Path.Combine(folderPath, "profileimage.jpg");
            
            var img = UIImage.FromFile(filepath);
            NSData image = img.AsJPEG();
            NSError err = null;
            image.Save(fileName, false, out err);
            return fileName;
            
        }

        UIImagePickerController imagePicker;
       
        public async Task<string> GetImageWithCamera()
        {
            cameraImagePath = new TaskCompletionSource<string>();
            CancellationToken? token=null;

            requestStop = false;

            if (!token.HasValue)
                token = CancellationToken.None;

            // We create a TaskCompletionSource of decimal
            var taskCompletionSource = new TaskCompletionSource<string>();

            // Registering a lambda into the cancellationToken
            token.Value.Register(() =>
            {
                requestStop = true;
                taskCompletionSource.TrySetCanceled();
            });

            _isLoading = true;

            var task = GetImageFromCamera();

            // Wait for the first task to finish among the two
            var completedTask = await Task.WhenAny(task, taskCompletionSource.Task);
            _isLoading = false;

            return await cameraImagePath.Task;
        }

        private async Task GetImageFromCamera()
        {
            if (await AuthorizeCameraUse())
            {

                //Create an image picker object
                imagePicker = new UIImagePickerController { SourceType = UIImagePickerControllerSourceType.Camera };

                //Make sure we can find the top most view controller to launch the camera
                var window = UIApplication.SharedApplication.KeyWindow;
                var vc = window.RootViewController;
                while (vc.PresentedViewController != null)
                {
                    vc = vc.PresentedViewController;
                }

                vc.PresentViewController(imagePicker, true, null);
                imagePicker.FinishedPickingMedia += Handle_FinishedPickingMedia;
                imagePicker.Canceled += Handle_Canceled;
                
            }
            else
            {
                //permission denied
            }
        }


        protected void Handle_FinishedPickingMedia(object sender, UIImagePickerMediaPickedEventArgs e)
        {
            // determine what was selected, video or image
            bool isImage = false;
            switch (e.Info[UIImagePickerController.MediaType].ToString())
            {
                case "public.image":
                    Console.WriteLine("Image selected");
                    isImage = true;
                    break;
                case "public.video":
                    Console.WriteLine("Video selected");
                    break;
            }

            // get common info (shared between images and video)
            NSUrl referenceURL = e.Info[new NSString("UIImagePickerControllerReferenceUrl")] as NSUrl;
            if (referenceURL != null)
                Console.WriteLine("Url:" + referenceURL.ToString());

            // if it was an image, get the other image info
            if (isImage)
            {
                // get the original image
                UIImage originalImage = e.Info[UIImagePickerController.OriginalImage] as UIImage;

                if (originalImage != null)
                {
                    // do something with the image
                    Console.WriteLine("got the original image");

                    var documentsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string jpgFilename = System.IO.Path.Combine(documentsDirectory, "Photo.jpg");
                    NSData imgData = originalImage.AsJPEG();
                    NSError err = null;
                    if (imgData.Save(jpgFilename, false, out err))
                    {
                        Console.WriteLine("saved as " + jpgFilename);
                        cameraImagePath.TrySetResult(jpgFilename);
                    }
                    else
                    {
                        Console.WriteLine("NOT saved as" + jpgFilename + " because" + err.LocalizedDescription);
                        cameraImagePath.TrySetResult("");
                    }
                }
                else
                { // if it's a video
                  // get video url
                    NSUrl mediaURL = e.Info[UIImagePickerController.MediaURL] as NSUrl;
                    if (mediaURL != null)
                    {
                        Console.WriteLine(mediaURL.ToString());
                    }
                }
                
                imagePicker.DismissViewController(true, null);
            }
        }

        void Handle_Canceled(object sender, EventArgs e)
        {
            
         cameraImagePath.TrySetResult("");
         imagePicker.DismissViewController(true, null);
         
        }

        public static async Task<bool> AuthorizeCameraUse()
        {
            var authorizationStatus = AVCaptureDevice.GetAuthorizationStatus(AVMediaType.Video);

            if (authorizationStatus != AVAuthorizationStatus.Authorized)
            {
                return await AVCaptureDevice.RequestAccessForMediaTypeAsync(AVMediaType.Video);
            }
            else
                return true;
        }

        class CameraDelegate : UIImagePickerControllerDelegate
        {
            public override void FinishedPickingMedia(UIImagePickerController picker, NSDictionary info)
            {
                picker.DismissModalViewController(true);
                var image = info.ValueForKey(new NSString("UIImagePickerControllerOriginalImage")) as UIImage;
            }
        }
    }
}
