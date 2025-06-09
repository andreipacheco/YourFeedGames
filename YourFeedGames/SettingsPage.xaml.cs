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
                new NewsPortal { Name = "FlowGames", Url = "https://flowgames.gg/", IsEnabled = Preferences.Get("FlowGames", true) },
                new NewsPortal { Name = "Gameplayscassi", Url = "https://gameplayscassi.com.br/", IsEnabled = Preferences.Get("Gameplayscassi", true) },
                new NewsPortal { Name = "TheEnemy", Url = "https://www.theenemy.com.br/", IsEnabled = Preferences.Get("TheEnemy", true) },
                new NewsPortal { Name = "IGNBrasil", Url = "https://br.ign.com/", IsEnabled = Preferences.Get("IGNBrasil", true) },
                new NewsPortal { Name = "Voxel", Url = "https://voxel.com.br", IsEnabled = Preferences.Get("Voxel", true) },
                new NewsPortal { Name = "GameVicio", Url = "https://www.gamevicio.com", IsEnabled = Preferences.Get("GameVicio", true) },
                new NewsPortal { Name = "TechTudo", Url = "https://www.techtudo.com.br/jogos/", IsEnabled = Preferences.Get("TechTudo", true) },
                new NewsPortal { Name = "Adrenaline", Url = "https://www.adrenaline.com.br/noticias/", IsEnabled = Preferences.Get("Adrenaline", true) },
                new NewsPortal { Name = "ComboInfinito", Url = "https://www.comboinfinito.com.br/principal/", IsEnabled = Preferences.Get("ComboInfinito", true) },
                new NewsPortal { Name = "Arkade", Url = "https://arkade.com.br/", IsEnabled = Preferences.Get("Arkade", true) }
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
