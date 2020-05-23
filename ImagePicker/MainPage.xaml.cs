using System.ComponentModel;
using System.Threading.Tasks;
using ImagePicker.DependencyService;
using ImagePicker.Model;
using ImagePicker.ViewModel;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace ImagePicker
{
    // Learn more about making custom code visible in the Xamarin.Forms previewer
    // by visiting https://aka.ms/xamarinforms-previewer
    [DesignTimeVisible(false)]
    public partial class MainPage : ContentPage
    {
        MainPageViewModel mainPageViewModel;
        Frame currentSelectedFrame; //store the selected image frame from collection view
        bool isSelected; // determines is selected of the image
        Frame selectedFrame; //stores the previous selected image frame of collection view
        MediaAssest selectedMediaAsset; //this holds the selected image details

        public MainPage()
        {
            IMediaService mediaService = Xamarin.Forms.DependencyService.Get<IMediaService>();
            mainPageViewModel = new MainPageViewModel(mediaService);
            BindingContext = mainPageViewModel;
            InitializeComponent();

            ImageSkipOrSelectImageClickEvent();// check preference already image selected if selected load the profile picture or else defult image

        }

        async void TapGestureRecognizer_Tapped(System.Object sender, System.EventArgs e)
        {
            imageselector.IsVisible = true; //make image picker stack layout visible
            bodyContent.Opacity = 0.3; //make behing content opacity so that image picker gets attention
            bodyContent.InputTransparent = true; // disable any user interaction on main content
            imageNext.IsVisible = false;
            imageSkip.IsVisible = true;
            await imageselectorFrame.TranslateTo(0, imageselectorFrame.Y + 50, 300);
            await mainPageViewModel.LoadMediaAssets(); //load the image from phone storage
        }

        void imageSkipTapped(System.Object sender, System.EventArgs e)
        {
            if (Preferences.Get("ProfileImage", null)==null || Preferences.Get("ProfileImage", null) =="default5.png")
            {
                //if first time "ProfileImage" is null or profile not choosen
                bodyContent.Opacity = 1;
                bodyContent.InputTransparent = false;
                imageselector.IsVisible = false;
                isSelected = false;
                selectedMediaAsset = null;
                if (selectedFrame != null)
                {
                    selectedFrame.BackgroundColor = Color.Transparent;
                }
                Preferences.Set("ProfileImage", "default"); //skip pressed so store the data in Preferences with key "ProfileImage" and value as "default"
                ImageSkipOrSelectImageClickEvent(); //function handles if image choosen display the selected image or else display default image
            }
            else
            {
                // a valid profile image is already selected so this is second time or any number of time
                // so no need to set the preferences
                bodyContent.Opacity = 1;
                bodyContent.InputTransparent = false;
                imageselector.IsVisible = false;
                isSelected = false;
                selectedMediaAsset = null;
                if (selectedFrame != null)
                {
                    selectedFrame.BackgroundColor = Color.Transparent;
                }
                ImageSkipOrSelectImageClickEvent();
            }
        }

        async void imageNextTapped(System.Object sender, System.EventArgs e)
        {
            bodyContent.Opacity = 1;
            bodyContent.InputTransparent = false;
            imageselector.IsVisible = false;
            
            string path = await Xamarin.Forms.DependencyService.Get<IMediaService>().StoreProfileImage(selectedMediaAsset.Path); //store the image in app folder
            Preferences.Set("ProfileImage", path); //set the path of the image in preferences
            ImageSkipOrSelectImageClickEvent();
            isSelected = false;
            selectedMediaAsset = null;
            selectedFrame.BackgroundColor = Color.Transparent;
        }

        private void ImageSkipOrSelectImageClickEvent()
        {
            //common method to handle to display default image or seleceted image
            if (Preferences.ContainsKey("ProfileImage"))
            {
                string path = Preferences.Get("ProfileImage", null);
                if (path == "default")
                {
                    profilePicture.Source = "default5.png";
                }
                else
                {
                    profilePicture.Source = path;
                }
            }


        }

        async void imageTapped(System.Object sender, System.EventArgs e)
        {
            var s = (StackLayout)sender;
            var ss = s.Children[0] as Grid;
            var sss = ss.Children[0] as Frame;
            selectedFrame = ss.Children[1] as Frame;
            var clicked = (TappedEventArgs)e;
            var mediaAssest = (MediaAssest)clicked.Parameter;
            selectedMediaAsset = mediaAssest;
            if (mediaAssest.PreviewPath == "group.png")
            {
                //TODO : open camera 
            }
            else
            {
                if (currentSelectedFrame != null)
                {
                    if (selectedFrame != currentSelectedFrame)
                    {
                        selectedFrame.BackgroundColor = Color.Green;
                        currentSelectedFrame.BackgroundColor = Color.Transparent;
                        currentSelectedFrame = selectedFrame;
                        isSelected = true;
                    }
                    else
                    {
                        if (selectedFrame.BackgroundColor == Color.Green)
                        {
                            selectedFrame.BackgroundColor = Color.Transparent;

                            currentSelectedFrame = selectedFrame;
                            isSelected = false;
                        }
                        else
                        {
                            selectedFrame.BackgroundColor = Color.Green;
                            isSelected = true;
                        }
                    }
                }
                else
                {
                    selectedFrame.BackgroundColor = Color.Green;
                    currentSelectedFrame = selectedFrame;
                    isSelected = true;
                }

                //display next button
                if (isSelected)
                {
                    //display next button
                    imageNext.IsVisible = true;
                    await imageNext.TranslateTo(0, 0, 300);
                }
                else
                {
                    //display skip options
                    imageNext.IsVisible = false;
                    await imageNext.TranslateTo(100, 0, 300);
                }

            }
        }
    }
}
