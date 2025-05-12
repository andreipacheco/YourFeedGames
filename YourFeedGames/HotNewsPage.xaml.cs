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

        private async void On2KLinkClicked(object sender, EventArgs e)
        {
            var url = "https://mafia.2k.com/the-old-country/";
            await Launcher.OpenAsync(new Uri(url));
        }
    }
}
