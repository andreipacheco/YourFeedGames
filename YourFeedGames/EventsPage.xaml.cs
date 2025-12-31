using OneSignalSDK.DotNet;

namespace YourFeedGames;
using System.Collections.ObjectModel;
using YourFeedGames.Models;
using System.Linq;

public partial class EventsPage : ContentPage
{
    private readonly OneSignalService _oneSignalService;
    private readonly SupabaseService _supabaseService = new SupabaseService();
    private string _playerId;

    // OneSignal tem limite de agendamento - geralmente 30 dias no máximo
    private static readonly TimeSpan MAX_SCHEDULE_AHEAD = TimeSpan.FromDays(30);

    public ObservableCollection<Events> EventosFuturos { get; set; } = new();

    public EventsPage()
    {
        InitializeComponent();
        // _oneSignalService = new OneSignalService();
        // _playerId = GetCurrentPlayerId();
        BindingContext = this;
    }


    //private string GetCurrentPlayerId()
    //{
    //    try
    //    {
    //        string pushId = OneSignal.User.PushSubscription.Id;
    //        Console.WriteLine($"[OneSignal] Push ID: {pushId}");
    //        return pushId ?? string.Empty;
    //    }
    //    catch (Exception ex)
    //    {
    //        Console.WriteLine($"[OneSignal] Erro ao obter Push ID: {ex.Message}");
    //        return string.Empty;
    //    }
    //}

    // Método adicional para verificar se uma data está dentro do limite do OneSignal
    private bool EstaDentroDoLimiteAgendamento(DateTime dataNotificacao)
    {
        var agora = DateTime.Now;
        return dataNotificacao > agora && (dataNotificacao - agora) <= MAX_SCHEDULE_AHEAD;
    }

    // Método para calcular quando o usuário deve tentar agendar novamente
    private DateTime CalcularProximaTentativa(DateTime dataEvento)
    {
        return dataEvento.AddDays(-MAX_SCHEDULE_AHEAD.TotalDays);
    }


    private async void OnGetEventosClicked(object sender, EventArgs e)
    {
        var eventos = await _supabaseService.GetEventosAsync();
        var eventosFuturos = eventos.Where(ev => ev.data > DateTime.Now).OrderBy(ev => ev.data).ToList();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            EventosFuturos.Clear();
            foreach (var evento in eventosFuturos)
                EventosFuturos.Add(evento);
        });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        OnGetEventosClicked(this, EventArgs.Empty);
    }

private async void OnEventoSelecionado(object sender, SelectionChangedEventArgs e)
{
    if (e.CurrentSelection?.FirstOrDefault() is YourFeedGames.Models.Events evento && !string.IsNullOrWhiteSpace(evento.url))
    {
        try
        {
            if (Uri.TryCreate(evento.url, UriKind.Absolute, out var uriResult) &&
                (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
            {
                await Launcher.OpenAsync(uriResult);
            }
            else
            {
                await DisplayAlert("Link inválido", "O link deste evento está mal formatado ou ausente.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro", $"Não foi possível abrir o link: {ex.Message}", "OK");
        }
    }
    ((CollectionView)sender).SelectedItem = null;
}

private async void OnReadMoreClicked(object sender, EventArgs e)
{
    if (sender is Button button && button.CommandParameter is string url && !string.IsNullOrWhiteSpace(url))
    {
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uriResult) &&
                (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
            {
                await Launcher.OpenAsync(uriResult);
            }
            else
            {
                await DisplayAlert("Link inválido", "O link deste evento está mal formatado ou ausente.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro", $"Não foi possível abrir o link: {ex.Message}", "OK");
        }
    }
}
}