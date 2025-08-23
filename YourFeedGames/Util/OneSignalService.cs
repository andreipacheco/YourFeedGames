using System.Text.Json;
using System.Text;
using Pagamentos;

public class OneSignalService
{
    private readonly string _appId = Secrets.OneSignalAppId;
    private readonly string _apiKey = Secrets.OneSignalRestApiKey;
    private readonly HttpClient _httpClient;

    // Dicionário para armazenar IDs de notificações agendadas
    private static readonly Dictionary<string, List<string>> _notificacoesAgendadas = new();

    public OneSignalService()
    {
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// Envia uma notificação agendada para um evento específico
    /// </summary>
    public async Task<string> EnviarNotificacaoEventoAsync(string idPlayer, string nomeEvento,
        string titulo, string conteudo, DateTime dataEnvio, string? imagemUrl = null)
    {
        try
        {
            var notificationData = new
            {
                app_id = _appId,
                include_player_ids = new[] { idPlayer },
                headings = new { pt = titulo, en = titulo },
                contents = new { pt = conteudo, en = conteudo },
                send_after = dataEnvio.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
                big_picture = imagemUrl ?? "https://meu-balde-s3.s3.sa-east-1.amazonaws.com/AppJaPaguei/JaPaguei.png",
                // Adiciona dados customizados para identificar o evento
                data = new { evento = nomeEvento, tipo = "lembrete_evento" },
                // Configurações de entrega
                priority = 10,
                ttl = 259200, // 3 dias em segundos
                // Ícone personalizado se disponível
                chrome_web_icon = imagemUrl,
                // Configurações para Android
                android_accent_color = "FF6B35FF",
                android_visibility = 1
            };

            string json = JsonSerializer.Serialize(notificationData);

            var request = new HttpRequestMessage(HttpMethod.Post, "https://onesignal.com/api/v1/notifications")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Authorization", $"Basic {_apiKey}");

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);

                string notificationId = responseObj.GetProperty("id").GetString() ?? "";

                // Armazena o ID da notificação para possível cancelamento
                ArmazenarIdNotificacao(nomeEvento, notificationId);

                Console.WriteLine($"Notificação agendada com sucesso para {nomeEvento}! ID: {notificationId}");
                return notificationId;
            }
            else
            {
                string error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Erro ao agendar notificação para {nomeEvento}: {error}");
                throw new Exception($"Erro da API OneSignal: {error}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao agendar notificação para {nomeEvento}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Agenda múltiplas notificações para um evento
    /// </summary>
    public async Task<List<string>> AgendarNotificacoesEventoAsync(string idPlayer, string nomeEvento,
        List<(string titulo, string conteudo, DateTime dataEnvio)> notificacoes, string? imagemUrl = null)
    {
        var notificationIds = new List<string>();

        foreach (var notificacao in notificacoes)
        {
            try
            {
                var id = await EnviarNotificacaoEventoAsync(idPlayer, nomeEvento,
                    notificacao.titulo, notificacao.conteudo, notificacao.dataEnvio, imagemUrl);
                notificationIds.Add(id);

                // Pequeno delay entre as requisições para evitar rate limiting
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao agendar notificação '{notificacao.titulo}': {ex.Message}");
            }
        }

        return notificationIds;
    }

    /// <summary>
    /// Cancela notificações agendadas para um evento
    /// </summary>
    public async Task<bool> CancelarNotificacoesEventoAsync(string nomeEvento)
    {
        if (!_notificacoesAgendadas.ContainsKey(nomeEvento))
        {
            return true; // Nenhuma notificação para cancelar
        }

        var ids = _notificacoesAgendadas[nomeEvento];
        bool sucessoTotal = true;

        foreach (var notificationId in ids)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Delete,
                    $"https://onesignal.com/api/v1/notifications/{notificationId}?app_id={_appId}");
                request.Headers.Add("Authorization", $"Basic {_apiKey}");

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Erro ao cancelar notificação {notificationId}");
                    sucessoTotal = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao cancelar notificação {notificationId}: {ex.Message}");
                sucessoTotal = false;
            }
        }

        // Remove os IDs da lista local
        _notificacoesAgendadas.Remove(nomeEvento);

        return sucessoTotal;
    }

    /// <summary>
    /// Verifica se existem notificações agendadas para um evento
    /// </summary>
    public bool TemNotificacoesAgendadas(string nomeEvento)
    {
        return _notificacoesAgendadas.ContainsKey(nomeEvento) &&
               _notificacoesAgendadas[nomeEvento].Any();
    }

    /// <summary>
    /// Armazena o ID de uma notificação agendada
    /// </summary>
    private void ArmazenarIdNotificacao(string nomeEvento, string notificationId)
    {
        if (!_notificacoesAgendadas.ContainsKey(nomeEvento))
        {
            _notificacoesAgendadas[nomeEvento] = new List<string>();
        }

        _notificacoesAgendadas[nomeEvento].Add(notificationId);
    }

    // Manter os métodos originais para compatibilidade
    public async Task EnviarNotificacaoAsync(string idPlayer, string titulo, string conteudo, DateTime? dataEnvio = null)
    {
        await EnviarNotificacaoEventoAsync(idPlayer, "geral", titulo, conteudo,
            dataEnvio ?? DateTime.UtcNow);
    }

    public async Task EnviarNotificacaoTesteAsync(string idPlayer)
    {
        await EnviarNotificacaoEventoAsync(idPlayer, "teste",
            "Lembrete de Pagamento!",
            "Não se esqueça de verificar suas contas este mês!",
            DateTime.UtcNow.AddMinutes(1));
    }
}