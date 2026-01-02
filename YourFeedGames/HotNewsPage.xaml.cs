using System.Collections.ObjectModel;
using YourFeedGames.Models;
using Microsoft.Maui.Controls;

namespace YourFeedGames
{
    public partial class HotNewsPage : ContentPage
    {
        private readonly SupabaseService _supabaseService = new SupabaseService();
        public ObservableCollection<HotNews> HotNewsAtivas { get; set; } = new();

        public HotNewsPage()
        {
            InitializeComponent();
            BindingContext = this;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            var noticias = await _supabaseService.GetHotNewsAtivasAsync();
            Console.WriteLine(noticias);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                HotNewsAtivas.Clear();
                foreach (var n in noticias)
                    HotNewsAtivas.Add(n);
            });
        }
    }
}