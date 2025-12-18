using Microsoft.Maui.Controls;

namespace YourFeedGames
{
    public partial class HotNewsPage : ContentPage
    {
        public HotNewsPage()
        {
            InitializeComponent();
        }

        private async void OnNintendoLinkClicked(object sender, EventArgs e)
        {
            var url = "https://www.nintendo.com/pt-br/gaming-systems/switch-2/";
            await Launcher.OpenAsync(new Uri(url));
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

        private async void OnSteamLinkClicked(object sender, EventArgs e)
        {
            var url = "https://store.steampowered.com/news/collection/steam/?emclan=103582791475000432&emgid=578276333072679812";
            await Launcher.OpenAsync(new Uri(url));
        }
    }
}