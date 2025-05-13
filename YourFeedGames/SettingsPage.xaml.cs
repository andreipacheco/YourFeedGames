using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using System.Linq;

namespace YourFeedGames
{
    public partial class SettingsPage : ContentPage
    {
        public ObservableCollection<NewsPortal> NewsPortals { get; set; }
        public Command SaveCommand { get; set; }

        public SettingsPage()
        {
            InitializeComponent();

            NewsPortals = new ObservableCollection<NewsPortal>
            {
                new NewsPortal { Name = "Flow Games", Url = "https://flowgames.gg/", IsEnabled = true },
                new NewsPortal { Name = "Gameplayscassi", Url = "https://gameplayscassi.com.br/", IsEnabled = true },
                new NewsPortal { Name = "The Enemy", Url = "https://www.theenemy.com.br/", IsEnabled = true },
                new NewsPortal { Name = "IGN Brasil", Url = "https://br.ign.com/", IsEnabled = true },
                new NewsPortal { Name = "Voxel", Url = "https://voxel.com.br", IsEnabled = Preferences.Get("Voxel", true) },
                new NewsPortal { Name = "GameVicio", Url = "https://www.gamevicio.com", IsEnabled = Preferences.Get("GameVicio", true) },
                new NewsPortal { Name = "TechTudo", Url = "https://www.techtudo.com.br/jogos/", IsEnabled = Preferences.Get("TechTudo", true) },
                new NewsPortal { Name = "Adrenaline", Url = "https://www.adrenaline.com.br/noticias/", IsEnabled = Preferences.Get("Adrenaline", true) }
            };

            SaveCommand = new Command(SaveSettings);

            BindingContext = this;
        }

        private async void SaveSettings()
        {
            foreach (var portal in NewsPortals)
            {
                Preferences.Set(portal.Name, portal.IsEnabled);
            }

            await DisplayAlert("Configurações", "Configurações salvas com sucesso!", "OK");

            // Navegar de volta para a página inicial
            await Shell.Current.GoToAsync("///MainPage");
        }
    }
}
