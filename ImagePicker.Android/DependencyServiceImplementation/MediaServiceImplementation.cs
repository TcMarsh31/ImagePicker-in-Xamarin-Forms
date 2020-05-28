using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Acr.UserDialogs;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Database;
using Android.Graphics;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Android.Support.V4.App;
using ImagePicker.DependencyService;
using ImagePicker.Droid.DependencyServiceImplementation;
using ImagePicker.Model;
using Java.IO;
using Xamarin.Forms;
using static Android.Provider.MediaStore;
using static ImagePicker.Model.MediaAssest;

[assembly: Dependency(typeof(MediaServiceImplementation))]
namespace ImagePicker.Droid.DependencyServiceImplementation
{
    public class MediaServiceImplementation : IMediaService
    {
        bool stopLoad = false;

        bool _isLoading = false;
        public bool IsLoading => _isLoading;

        static TaskCompletionSource<bool> mediaPermissionTcs;

        static TaskCompletionSource<string> cameraImagePath;

        public const int RequestMedia = 1354;

        public const int RequestCamera = 1355;

        public event EventHandler<MediaEventArgs> OnMediaAssetLoaded;


        public MediaServiceImplementation()
        {
        }

        async void RequestMediaPermissions()
        {
            if (ActivityCompat.ShouldShowRequestPermissionRationale(MainActivity.FormsActivity, Manifest.Permission.WriteExternalStorage))
            {

                // Provide an additional rationale to the user if the permission was not granted
                // and the user would benefit from additional context for the use of the permission.
                // For example, if the request has been denied previously.

                await UserDialogs.Instance.AlertAsync("Media Permission", "This action requires external storafge permission", "Ok");
            }
            else
            {
                // Media permissions have not been granted yet. Request them directly.
                ActivityCompat.RequestPermissions(MainActivity.FormsActivity, new string[] { Manifest.Permission.WriteExternalStorage }, RequestMedia);
            }
        }

        public static void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            if (requestCode == MediaServiceImplementation.RequestMedia)
            {
                // We have requested multiple permissions for Media, so all of them need to be
                // checked.
                if (PermissionUtil.VerifyPermissions(grantResults))
                {
                    // All required permissions have been granted, display Media fragment.
                    mediaPermissionTcs.TrySetResult(true);
                }
                else
                {
                    mediaPermissionTcs.TrySetResult(false);
                }

            }
            if(requestCode == RequestCamera)
            {
                // We have requested multiple permissions for Camera, so all of them need to be
                // checked.
                if (PermissionUtil.VerifyPermissions(grantResults))
                {
                    // All required permissions have been granted, display Media fragment.
                    mediaPermissionTcs.TrySetResult(true);
                }
                else
                {
                    mediaPermissionTcs.TrySetResult(false);
                }
            }
        }

        public async Task<bool> RequestPermissionAsync()
        {
            mediaPermissionTcs = new TaskCompletionSource<bool>();
            // Verify that all required Media permissions have been granted.
            if (Android.Support.V4.Content.ContextCompat.CheckSelfPermission(MainActivity.FormsActivity, Manifest.Permission.WriteExternalStorage) != (int)Permission.Granted)
            {
                // Media permissions have not been granted.
                RequestMediaPermissions();
            }
            else
            {
                // Media permissions have been granted. 
                mediaPermissionTcs.TrySetResult(true);
            }

            return await mediaPermissionTcs.Task;
        }

        public async Task<IList<MediaAssest>> RetrieveMediaAssetsAsync(CancellationToken? token = null)
        {
            stopLoad = false;

            if (!token.HasValue)
                token = CancellationToken.None;

            // We create a TaskCompletionSource of decimal
            var taskCompletionSource = new TaskCompletionSource<IList<MediaAssest>>();

            // Registering a lambda into the cancellationToken
            token.Value.Register(() =>
            {
                // We received a cancellation message, cancel the TaskCompletionSource.Task
                stopLoad = true;
                taskCompletionSource.TrySetCanceled();
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
            var hasPermission = await RequestPermissionAsync();
            if (hasPermission)
            {
                var uri = MediaStore.Files.GetContentUri("external");
                var ctx = Android.App.Application.Context;
                await Task.Run(async () =>
                {
                    var cursor = ctx.ApplicationContext.ContentResolver.Query(uri, new string[]
                    {
                        MediaStore.Files.FileColumns.Id,
                        MediaStore.Files.FileColumns.Data,
                        MediaStore.Files.FileColumns.DateAdded,
                        MediaStore.Files.FileColumns.MediaType,
                        MediaStore.Files.FileColumns.MimeType,
                        MediaStore.Files.FileColumns.Title,
                        MediaStore.Files.FileColumns.Parent,
                        MediaStore.Files.FileColumns.DisplayName,
                        MediaStore.Files.FileColumns.Size
                    //}, $"{MediaStore.Files.FileColumns.MediaType} = {(int)MediaType.Image} OR {MediaStore.Files.FileColumns.MediaType} = {(int)MediaType.Video}", null, $"{MediaStore.Files.FileColumns.DateAdded} DESC");
                }, $"{MediaStore.Files.FileColumns.MediaType} = {(int)MediaType.Image}", null, $"{MediaStore.Files.FileColumns.DateAdded} DESC");

                    if (cursor.Count > 0)
                    {
                        while (cursor.MoveToNext())
                        {
                            try
                            {
                                var id = cursor.GetInt(cursor.GetColumnIndex(MediaStore.Files.FileColumns.Id));
                                var mediaType = cursor.GetInt(cursor.GetColumnIndex(MediaStore.Files.FileColumns.MediaType));
                                Bitmap bitmap = null;
                                switch (mediaType)
                                {
                                    case (int)MediaType.Image:
                                        bitmap = MediaStore.Images.Thumbnails.GetThumbnail(MainActivity.FormsActivity.ContentResolver, id, ThumbnailKind.MiniKind, new BitmapFactory.Options()
                                        {
                                            InSampleSize = 4,
                                            InPurgeable = true
                                        });
                                        break;
                                    case (int)MediaType.Video:
                                        bitmap = MediaStore.Video.Thumbnails.GetThumbnail(MainActivity.FormsActivity.ContentResolver, id, VideoThumbnailKind.MiniKind, new BitmapFactory.Options()
                                        {
                                            InSampleSize = 4,
                                            InPurgeable = true
                                        });
                                        break;
                                }
                                var tmpPath = System.IO.Path.GetTempPath();
                                var name = GetString(cursor, MediaStore.Files.FileColumns.DisplayName);
                                var filePath = System.IO.Path.Combine(tmpPath, $"tmp_{name}");

                                var path = GetString(cursor, MediaStore.Files.FileColumns.Data);

                                //filePath = path;
                                using (var stream = new FileStream(filePath, FileMode.Create))
                                {
                                    bitmap?.Compress(Bitmap.CompressFormat.Png, 100, stream);
                                    stream.Close();
                                }


                                if (!string.IsNullOrWhiteSpace(filePath))
                                {
                                    var asset = new MediaAssest()
                                    {
                                        Id = $"{id}",
                                        Type = mediaType == (int)MediaType.Video ? MediaAssetType.Video : MediaAssetType.Image,
                                        Name = name,
                                        PreviewPath = filePath,
                                        Path = path,
                                        IsSelectable = true
                                    };

                                    using (var h = new Handler(Looper.MainLooper))
                                        h.Post(async () => { OnMediaAssetLoaded?.Invoke(this, new Model.MediaEventArgs(asset)); });

                                    assets.Add(asset);
                                }

                                if (stopLoad)
                                    break;
                            }
                            catch (Exception ex)
                            {
                                await UserDialogs.Instance.AlertAsync(ex.StackTrace.ToString(), "error", "ok");
                            }

                        }
                    }
                });
            }
            return assets;
        }
        string GetString(ICursor cursor, string key)
        {
            return cursor.GetString(cursor.GetColumnIndex(key));
        }


        public async Task<string> StoreProfileImage(string path)
        {

            Bitmap bm = BitmapFactory.DecodeFile(path);
            string filePath;
            byte[] bitmapData;
            using (var stream = new MemoryStream())
            {
                bm.Compress(Bitmap.CompressFormat.Png, 0, stream);
                bitmapData = stream.ToArray();
            }

            var pictures = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
            filePath = System.IO.Path.Combine(pictures, "ProfilePicture" + ".png");

            try
            {
                System.IO.File.WriteAllBytes(filePath, bitmapData);
                Java.IO.File fl = new Java.IO.File(filePath);
            }
            catch (System.Exception e)
            {
                System.Console.WriteLine(e.ToString());
            }
            return filePath;
        }

        public async Task<string> GetImageWithCamera()
        {
            cameraImagePath = new TaskCompletionSource<string>();
            if (MainActivity.FormsActivity.PackageManager.HasSystemFeature(PackageManager.FeatureCamera))
            {
                //camera is available in system 
                var hasPermission = await RequestCameraPermissionAsync();
                if (hasPermission)

                {
                    Intent intent = new Intent(MediaStore.ActionImageCapture);
                    MainActivity.FormsActivity.StartActivityForResult(intent, RequestCamera);

                }
            }
            return await cameraImagePath.Task;
        }


        async void RequestCameraPermissions()
        {
            if (ActivityCompat.ShouldShowRequestPermissionRationale(MainActivity.FormsActivity, Manifest.Permission.Camera))
            {

                // Provide an additional rationale to the user if the permission was not granted
                // and the user would benefit from additional context for the use of the permission.
                // For example, if the request has been denied previously.

                await UserDialogs.Instance.AlertAsync("Camera Permission", "This action requires Camera permission", "Ok");
            }
            else
            {
                // Media permissions have not been granted yet. Request them directly.
                ActivityCompat.RequestPermissions(MainActivity.FormsActivity, new string[] { Manifest.Permission.Camera }, RequestCamera);
            }
        }

        public async Task<bool> RequestCameraPermissionAsync()
        {
            mediaPermissionTcs = new TaskCompletionSource<bool>();
            // Verify that all required Camera permissions have been granted.
            if (Android.Support.V4.Content.ContextCompat.CheckSelfPermission(MainActivity.FormsActivity, Manifest.Permission.Camera) != (int)Permission.Granted)
            {
                // Camera permissions have not been granted.
                RequestCameraPermissions();
            }
            else
            {
                // Camera permissions have been granted. 
                mediaPermissionTcs.TrySetResult(true);
            }

            return await mediaPermissionTcs.Task;
        }

        public static void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
           
            switch (requestCode)
            {
                case RequestCamera:
                    if (resultCode == Result.Ok)
                    {
                        var selectedImage = data.Data;
                        Bitmap photo = (Bitmap)data.Extras.Get("data");
                        // CALL THIS METHOD TO GET THE URI FROM THE BITMAP
                        Android.Net.Uri tempUri = getImageUri(MainActivity.FormsContext, photo);

                        // CALL THIS METHOD TO GET THE ACTUAL PATH
                        //Java.IO.File finalFile = new Java.IO.File(getRealPathFromURI(tempUri));
                        string filePathofCameraImage = getRealPathFromURI(tempUri);
                        cameraImagePath.TrySetResult(filePathofCameraImage);
                        //StoreProfileImage(filePathofCameraImage);
                        //System.out.println(mImageCaptureUri);
                        //imageview.setImageUri(selectedImage);
                    }
                    else
                    {
                        cameraImagePath.TrySetResult("");
                    }
                    break;
            }
        }

        public static Android.Net.Uri getImageUri(Context inContext, Bitmap inImage)
        {
            //ByteArrayOutputStream bytes = new ByteArrayOutputStream();
            //inImage.Compress(Bitmap.CompressFormat.Jpeg, 100, bytes);

            using (var stream = new MemoryStream())
            {
                inImage.Compress(Bitmap.CompressFormat.Png, 0, stream);
                //bitmapData = stream.ToArray();
            }

            String path = Images.Media.InsertImage(inContext.ContentResolver, inImage, "Title", null);
            return Android.Net.Uri.Parse(path);
        }

        public static String getRealPathFromURI(Android.Net.Uri uri)
        {
            String path = "";
            if (MainActivity.FormsContext.ContentResolver != null)
            {
                var cursor = MainActivity.FormsContext.ContentResolver.Query(uri, null, null, null, null);
                if (cursor != null)
                {
                    cursor.MoveToFirst();
                    int idx = cursor.GetColumnIndex(MediaStore.Images.ImageColumns.Data);
                    path = cursor.GetString(idx);
                    cursor.Close();
                }
            }
            return path;
        }
    }
}

