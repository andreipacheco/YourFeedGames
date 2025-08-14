using Microsoft.Maui.Controls;
using System.Text.Json;
using System.Text;
using Pagamentos;

namespace YourFeedGames
{
    public partial class SummaryPage : ContentPage
    {
        private string _originalText;
        private CancellationTokenSource _cancellationTokenSource;

        public SummaryPage(string textoParaResumo)
        {
            InitializeComponent();
            _originalText = textoParaResumo;
            
            // Iniciar a gera��o do resumo automaticamente
            _ = Task.Run(async () => await GenerateSummaryAsync());
        }

        private async Task GenerateSummaryAsync()
        {
            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                
                // Atualizar UI para mostrar loading
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ShowLoadingState();
                    progressLabel.Text = "Conectando com a IA...";
                });

                // Simular um pequeno delay para mostrar o loading
                await Task.Delay(500, _cancellationTokenSource.Token);

                // Atualizar progresso
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    progressLabel.Text = "Analisando not�cias...";
                });

                // Gerar o resumo
                var resumo = await GerarResumoComIA(_originalText, _cancellationTokenSource.Token);

                // Mostrar resultado
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ShowContentState(resumo);
                });
            }
            catch (OperationCanceledException)
            {
                // Cancelado pelo usu�rio
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ShowErrorState("Opera��o cancelada pelo usu�rio.");
                });
            }
            catch (Exception ex)
            {
                // Erro na gera��o
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ShowErrorState($"Erro ao gerar resumo: {ex.Message}");
                });
            }
        }

        private void ShowLoadingState()
        {
            loadingStack.IsVisible = true;
            contentStack.IsVisible = false;
            errorStack.IsVisible = false;
            loadingIndicator.IsRunning = true;
        }

        private void ShowContentState(string resumo)
        {
            loadingStack.IsVisible = false;
            contentStack.IsVisible = true;
            errorStack.IsVisible = false;
            loadingIndicator.IsRunning = false;
            
            summaryLabel.Text = resumo;
        }

        private void ShowErrorState(string errorMessage)
        {
            loadingStack.IsVisible = false;
            contentStack.IsVisible = false;
            errorStack.IsVisible = true;
            loadingIndicator.IsRunning = false;
            
            errorLabel.Text = errorMessage;
        }

        private async Task<string> GerarResumoComIA(string texto, CancellationToken cancellationToken = default)
        {
            var apiKey = Secrets.apiKeyGeminiIA;
            var endpoint = $"{Secrets.geminiBaseUrl}?key={Secrets.apiKeyGeminiIA}";
            var prompt = $"Resuma as principais not�cias abaixo em at� 2 minutos de leitura, focando nos fatos mais relevantes, verifique se as not�cias se repetem em diferentes portais e d� destaque para essas not�cias:\n{texto}";
            
            var body = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            var jsonBody = JsonSerializer.Serialize(body, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(2); // Timeout de 2 minutos
            
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            
            // Atualizar progresso antes da chamada
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                progressLabel.Text = "Gerando resumo inteligente...";
            });
            
            var response = await client.PostAsync(endpoint, content, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Erro Gemini: {response.StatusCode} - {json}");
            }

            var doc = JsonDocument.Parse(json);
            var resumo = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return resumo ?? "N�o foi poss�vel gerar o resumo.";
        }

        private async void OnRegenerateClicked(object sender, EventArgs e)
        {
            // Cancelar opera��o anterior se estiver rodando
            _cancellationTokenSource?.Cancel();
            
            // Gerar novo resumo
            _ = Task.Run(async () => await GenerateSummaryAsync());
        }

        private async void OnRetryClicked(object sender, EventArgs e)
        {
            // Tentar novamente
            _ = Task.Run(async () => await GenerateSummaryAsync());
        }

        protected override void OnDisappearing()
        {
            // Cancelar opera��o se o usu�rio sair da p�gina
            _cancellationTokenSource?.Cancel();
            base.OnDisappearing();
        }
    }
}