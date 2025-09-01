using OneSignalSDK.DotNet;

namespace YourFeedGames;

public partial class EventsPage : ContentPage
{
    private readonly OneSignalService _oneSignalService;
    private string _playerId;

    // OneSignal tem limite de agendamento - geralmente 30 dias no máximo
    private static readonly TimeSpan MAX_SCHEDULE_AHEAD = TimeSpan.FromDays(30);

    // Estados dos botões para controlar se as notificações estão ativas
    private bool _tgsNotificationsActive = false;
    private bool _tgaNotificationsActive = false;

    public EventsPage()
    {
        InitializeComponent();
        _oneSignalService = new OneSignalService();
        _playerId = GetCurrentPlayerId();
    }

    private async void OnTgsNotifyClicked(object sender, EventArgs e)
    {
        if (!_tgsNotificationsActive)
        {
            // Ativar notificações
            bool success = await AgendarNotificacaoTgs();
            if (success)
            {
                _tgsNotificationsActive = true;
                TgsNotifyButton.Text = "🔕 Desativar Notificações";
                TgsNotifyButton.BackgroundColor = Colors.Gray;
            }
        }
        else
        {
            // Desativar notificações
            await CancelarNotificacoes("Tokyo Game Show 2025");
            _tgsNotificationsActive = false;
            TgsNotifyButton.Text = "🔔 Ativar Notificações";
            TgsNotifyButton.BackgroundColor = (Color)Application.Current.Resources["Primary"];
        }
    }

    private async void OnTgaNotifyClicked(object sender, EventArgs e)
    {
        if (!_tgaNotificationsActive)
        {
            // Ativar notificações
            bool success = await AgendarNotificacaoTga();
            if (success)
            {
                _tgaNotificationsActive = true;
                TgaNotifyButton.Text = "🔕 Desativar Notificações";
                TgaNotifyButton.BackgroundColor = Colors.Gray;
            }
        }
        else
        {
            // Desativar notificações
            await CancelarNotificacoes("The Game Awards 2025");
            _tgaNotificationsActive = false;
            TgaNotifyButton.Text = "🔔 Ativar Notificações";
            TgaNotifyButton.BackgroundColor = (Color)Application.Current.Resources["Primary"];
        }
    }

    private async Task CancelarNotificacoes(string nomeEvento)
    {
        try
        {
            bool cancelado = await _oneSignalService.CancelarNotificacoesEventoAsync(nomeEvento);
            if (cancelado)
            {
                await DisplayAlert("❌ Notificações Canceladas",
                    $"Todos os lembretes do {nomeEvento} foram removidos!",
                    "OK");
            }
            else
            {
                await DisplayAlert("⚠️ Atenção",
                    "Algumas notificações podem não ter sido canceladas.",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Erro",
                $"Erro ao cancelar notificações: {ex.Message}",
                "OK");
        }
    }

    private async Task<bool> AgendarNotificacaoTgs()
    {
        try
        {
            const string nomeEvento = "Tokyo Game Show 2025";
            var dataEvento = new DateTime(2025, 9, 25, 10, 0, 0);
            var agora = DateTime.Now;

            // Verifica se o evento já passou
            if (dataEvento <= agora)
            {
                await DisplayAlert("⚠️ Evento Passado",
                    "O Tokyo Game Show 2025 já aconteceu ou está acontecendo agora!",
                    "OK");
                return false;
            }

            var notificacoes = new List<(string titulo, string conteudo, DateTime dataEnvio)>();

            // Calcula as datas de notificação ajustadas para o fuso horário do Brasil
            var dataEvento7DiasBrasil = dataEvento.AddDays(-7).AddHours(-14);
            var dataEvento1DiaBrasil = dataEvento.AddDays(-1).AddHours(-14);
            var dataEventoDiaBrasil = dataEvento.AddHours(-14);

            // Só agenda notificações que estão dentro do limite do OneSignal
            if (dataEvento7DiasBrasil > agora && (dataEvento7DiasBrasil - agora) <= MAX_SCHEDULE_AHEAD)
            {
                notificacoes.Add((
                    "🗓️ Tokyo Game Show 2025 em uma semana!",
                    "Faltam apenas 7 dias para o maior evento de games do Japão! Prepare-se para as grandes novidades.",
                    dataEvento7DiasBrasil
                ));
            }

            if (dataEvento1DiaBrasil > agora && (dataEvento1DiaBrasil - agora) <= MAX_SCHEDULE_AHEAD)
            {
                notificacoes.Add((
                    "🎮 Tokyo Game Show começa amanhã!",
                    "O maior evento de games do Japão acontece amanhã em Chiba. Microsoft/Xbox e outras gigantes estarão presentes!",
                    dataEvento1DiaBrasil
                ));
            }

            if (dataEventoDiaBrasil > agora && (dataEventoDiaBrasil - agora) <= MAX_SCHEDULE_AHEAD)
            {
                notificacoes.Add((
                    "🚀 Tokyo Game Show está acontecendo AGORA!",
                    "O TGS 2025 começou! Acompanhe as principais apresentações e anúncios ao vivo.",
                    dataEventoDiaBrasil
                ));
            }

            if (notificacoes.Count == 0)
            {
                await DisplayAlert("⚠️ Limite de Agendamento",
                    $"As notificações do Tokyo Game Show não podem ser agendadas ainda.\n\n" +
                    $"O OneSignal só permite agendar notificações com até {MAX_SCHEDULE_AHEAD.TotalDays} dias de antecedência.\n\n" +
                    "Tente novamente mais próximo da data do evento!",
                    "Entendi");
                return false;
            }

            // Agenda as notificações disponíveis
            var ids = await _oneSignalService.AgendarNotificacoesEventoAsync(
                _playerId,
                nomeEvento,
                notificacoes,
                "https://example.com/tgs2025-logo.png"
            );

            var mensagem = $"Agendamos {ids.Count} lembretes sobre o Tokyo Game Show 2025!\n\n";

            if (notificacoes.Any(n => n.dataEnvio == dataEvento7DiasBrasil))
                mensagem += "• 7 dias antes\n";
            if (notificacoes.Any(n => n.dataEnvio == dataEvento1DiaBrasil))
                mensagem += "• 1 dia antes\n";
            if (notificacoes.Any(n => n.dataEnvio == dataEventoDiaBrasil))
                mensagem += "• No dia do evento\n";

            var totalPossivel = 3;
            if (ids.Count < totalPossivel)
            {
                mensagem += $"\nNota: Algumas notificações não puderam ser agendadas devido ao limite de {MAX_SCHEDULE_AHEAD.TotalDays} dias do OneSignal.";
            }

            await DisplayAlert("✅ Notificações Agendadas", mensagem, "Perfeito!");
            return true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Erro",
                $"Não foi possível agendar as notificações:\n{ex.Message}",
                "OK");
            return false;
        }
    }

    private async Task<bool> AgendarNotificacaoTga()
    {
        try
        {
            const string nomeEvento = "The Game Awards 2025";
            var dataEvento = new DateTime(2025, 12, 11, 20, 0, 0);
            var agora = DateTime.Now;

            if (dataEvento <= agora)
            {
                await DisplayAlert("⚠️ Evento Passado",
                    "O The Game Awards 2025 já aconteceu ou está acontecendo agora!",
                    "OK");
                return false;
            }

            var notificacoes = new List<(string titulo, string conteudo, DateTime dataEnvio)>();

            // Calcula as datas de notificação ajustadas para o fuso horário do Brasil
            var dataEvento3DiasBrasil = dataEvento.AddDays(-3).AddHours(3);
            var dataEvento1DiaBrasil = dataEvento.AddDays(-1).AddHours(3);
            var dataEvento2HorasBrasil = dataEvento.AddHours(-2).AddHours(3);
            var dataEventoDuranteBrasil = dataEvento.AddMinutes(15).AddHours(3);

            // Só agenda notificações que estão dentro do limite do OneSignal
            if (dataEvento3DiasBrasil > agora && (dataEvento3DiasBrasil - agora) <= MAX_SCHEDULE_AHEAD)
            {
                notificacoes.Add((
                    "🏆 The Game Awards 2025 se aproxima!",
                    "Faltam apenas 3 dias para a maior premiação de games do mundo! Quais são seus favoritos este ano?",
                    dataEvento3DiasBrasil
                ));
            }

            if (dataEvento1DiaBrasil > agora && (dataEvento1DiaBrasil - agora) <= MAX_SCHEDULE_AHEAD)
            {
                notificacoes.Add((
                    "🎖️ The Game Awards 2025 é amanhã!",
                    "A maior premiação de games do mundo acontece amanhã no Peacock Theater, Los Angeles. Prepare-se para conhecer os vencedores!",
                    dataEvento1DiaBrasil
                ));
            }

            if (dataEvento2HorasBrasil > agora && (dataEvento2HorasBrasil - agora) <= MAX_SCHEDULE_AHEAD)
            {
                notificacoes.Add((
                    "⏰ The Game Awards começam em 2 horas!",
                    "Faltam apenas 2 horas para a cerimônia de premiação! A transmissão ao vivo está começando.",
                    dataEvento2HorasBrasil
                ));
            }

            if (dataEventoDuranteBrasil > agora && (dataEventoDuranteBrasil - agora) <= MAX_SCHEDULE_AHEAD)
            {
                notificacoes.Add((
                    "🎮 The Game Awards 2025 AO VIVO!",
                    "A cerimônia começou! Acompanhe agora a premiação dos melhores games de 2025.",
                    dataEventoDuranteBrasil
                ));
            }

            if (notificacoes.Count == 0)
            {
                await DisplayAlert("⚠️ Limite de Agendamento",
                    $"As notificações do The Game Awards não podem ser agendadas ainda.\n\n" +
                    $"O OneSignal só permite agendar notificações com até {MAX_SCHEDULE_AHEAD.TotalDays} dias de antecedência.\n\n" +
                    "Tente novamente mais próximo da data do evento!",
                    "Entendi");
                return false;
            }

            var ids = await _oneSignalService.AgendarNotificacoesEventoAsync(
                _playerId,
                nomeEvento,
                notificacoes,
                "https://example.com/tga2025-logo.png"
            );

            var mensagem = $"Agendamos {ids.Count} lembretes sobre o The Game Awards 2025!\n\n";

            if (notificacoes.Any(n => n.dataEnvio == dataEvento3DiasBrasil))
                mensagem += "• 3 dias antes\n";
            if (notificacoes.Any(n => n.dataEnvio == dataEvento1DiaBrasil))
                mensagem += "• 1 dia antes\n";
            if (notificacoes.Any(n => n.dataEnvio == dataEvento2HorasBrasil))
                mensagem += "• 2 horas antes\n";
            if (notificacoes.Any(n => n.dataEnvio == dataEventoDuranteBrasil))
                mensagem += "• Durante o evento\n";

            var totalPossivel = 4;
            if (ids.Count < totalPossivel)
            {
                mensagem += $"\nNota: Algumas notificações não puderam ser agendadas devido ao limite de {MAX_SCHEDULE_AHEAD.TotalDays} dias do OneSignal.";
            }

            await DisplayAlert("✅ Notificações Agendadas", mensagem, "Fantástico!");
            return true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Erro",
                $"Não foi possível agendar as notificações:\n{ex.Message}",
                "OK");
            return false;
        }
    }

    private string GetCurrentPlayerId()
    {
        try
        {
            string pushId = OneSignal.User.PushSubscription.Id;
            Console.WriteLine($"[OneSignal] Push ID: {pushId}");
            return pushId ?? string.Empty;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OneSignal] Erro ao obter Push ID: {ex.Message}");
            return string.Empty;
        }
    }

    private async Task<bool> VerificarPermissaoNotificacao()
    {
        try
        {
            // Implementação específica da plataforma para verificar permissões
            return true;
        }
        catch
        {
            return false;
        }
    }

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
}