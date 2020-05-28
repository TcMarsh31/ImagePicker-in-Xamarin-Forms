using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ImagePicker.DependencyService;
using ImagePicker.Model;
using Xamarin.Forms;

namespace ImagePicker.ViewModel
{
    public class MainPageViewModel : INotifyPropertyChanged
    {
        IMediaService _mediaService;

        public event PropertyChangedEventHandler PropertyChanged;

        public string SearchText { get; set; }
        public ObservableCollection<MediaAssest> MediaAssets { get; set; }

        CancellationTokenSource source = new CancellationTokenSource();
        CancellationToken token;

        public ICommand ItemTappedCommand { get; set; }

        public MainPageViewModel(IMediaService mediaService)
        {
            _mediaService = mediaService;
            MediaAssets = new ObservableCollection<MediaAssest>();
            //token = source.Token;
            Xamarin.Forms.BindingBase.EnableCollectionSynchronization(MediaAssets, null, ObservableCollectionCallback);
            _mediaService.OnMediaAssetLoaded += OnMediaAssetLoaded;
        }

        void ObservableCollectionCallback(IEnumerable collection, object context, Action accessMethod, bool writeAccess)
        {
            // `lock` ensures that only one thread access the collection at a time
            lock (collection)
            {
                accessMethod?.Invoke();
            }
        }

        private void OnMediaAssetLoaded(object sender, MediaEventArgs e)
        {
            try
            {
                MediaAssets.Add(e.Media);
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        public async Task LoadMediaAssets()
        {
            try
            {
                if (source.IsCancellationRequested)
                {
                    source = new CancellationTokenSource();
                    token = source.Token;
                }
                else
                {
                    token = source.Token;
                }
                MediaAssets.Clear(); //clear list if already exists
                /*/
                 * Create default camera image as the first one
                 * so when click this image we can call camera action later
                 */
                MediaAssest defaultmedia = new MediaAssest();
                defaultmedia.PreviewPath = "group.png";
                defaultmedia.IsSelectable = false;
                MediaAssets.Add(defaultmedia);
                await _mediaService.RetrieveMediaAssetsAsync(token);
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Task was cancelled");
            }
        }

        public void CancelMediaAssests()
        {
            //await _mediaService.RetrieveMediaAssetsAsync(token);
            source.Cancel();
           
        }
    }
}
