using Microsoft.Maui.Controls;

namespace YourFeedGames
{
    public partial class HotNewsPage : ContentPage
    {
        public HotNewsPage()
        {
            InitializeComponent();
        }

        private async void OnRockstarLinkClicked(object sender, EventArgs e)
        {
            var url = "https://www.rockstargames.com/";
            await Launcher.OpenAsync(new Uri(url));
        }
    }
}
