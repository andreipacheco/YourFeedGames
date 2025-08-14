using HtmlAgilityPack;
using Pagamentos;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace YourFeedGames
{
    public partial class MainPage : ContentPage
    {
        public ObservableCollection<NewsItem> NewsFeed { get; set; }
        private HttpClient _httpClient;
        private bool _debugMode = false;
        private CancellationTokenSource _loadingCts;
        private DateTime _loadingStartTime;
        private List<NewsPortal> _activePortals;
        public ObservableCollection<NewsItem> AllNewsFeed { get; set; } = new();
        public MainPage()
        {
            InitializeComponent();
            NewsFeed = new ObservableCollection<NewsItem>();
            BindingContext = this;
            _httpClient = new HttpClient();

            // Definir cabeçalhos para simular um navegador
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml");

            // Inicializar o cliente HTTP com mais opções
            InitializeHttpClient();

            // Inicia o carregamento das notícias
            Task.Run(async () => await LoadNewsFeed());
        }

        // Método separado para inicializar o HttpClient com configurações anti-bloqueio
        private void InitializeHttpClient()
        {
            // Atualize para um User-Agent mais recente
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            
            // Criar um handler para configurar opções avançadas
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10,
                UseCookies = true,
                CookieContainer = new CookieContainer()
            };

            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromSeconds(20);

            // Simular diferentes navegadores aleatoriamente para evitar bloqueios
            Random rand = new Random();
            string[] userAgents = new string[]
            {
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/92.0.4515.107 Safari/537.36",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/15.0 Safari/605.1.15",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:90.0) Gecko/20100101 Firefox/90.0",
                "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/92.0.4515.107 Safari/537.36"
            };

            string selectedUserAgent = userAgents[rand.Next(userAgents.Length)];
            Console.WriteLine($"Usando User-Agent: {selectedUserAgent}");

            // Definir cabeçalhos para simular um navegador
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", selectedUserAgent);
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "pt-BR,pt;q=0.9,en-US;q=0.8,en;q=0.7");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.google.com/");
            _httpClient.DefaultRequestHeaders.Add("DNT", "1");
            _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "cross-site");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
            _httpClient.DefaultRequestHeaders.Add("Cache-Control", "max-age=0");
        }

        private void OnPortalPickerSelectedIndexChanged(object sender, EventArgs e)
        {
            if (portalPicker.SelectedIndex == -1)
            {
                // Nenhum portal selecionado, pode exibir todos ou nenhum, conforme desejado
                NewsFeed.Clear();
                foreach (var item in AllNewsFeed)
                    NewsFeed.Add(item);
                return;
            }

            if (portalPicker.SelectedItem is string selectedPortal)
            {
                NewsFeed.Clear();
                foreach (var item in AllNewsFeed.Where(n => n.Source == selectedPortal))
                    NewsFeed.Add(item);
            }
        }

        // Método para extrair artigos de scripts de dados estruturados (JSON)
        private bool TryExtractArticlesFromStructuredData(HtmlDocument htmlDoc, NewsPortal portal)
        {
            try
            {
                Console.WriteLine("Tentando extrair artigos de dados estruturados...");

                // Procurar por scripts JSON-LD que contêm artigos
                var scriptNodes = htmlDoc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
                if (scriptNodes == null) return false;

                bool foundArticles = false;

                foreach (var script in scriptNodes)
                {
                    var jsonContent = script.InnerText;
                    if (string.IsNullOrEmpty(jsonContent)) continue;

                    // Buscar por padrões de listas de artigos ou artigos individuais
                    if (jsonContent.Contains("\"@type\":\"NewsArticle\"") ||
                        jsonContent.Contains("\"@type\":\"Article\"") ||
                        jsonContent.Contains("\"@type\":\"ItemList\""))
                    {
                        Console.WriteLine("Encontrado JSON-LD com possíveis artigos");

                        // Extrair URLs e títulos usando regex
                        var urlPattern = new Regex("\"url\":\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
                        var titlePattern = new Regex("\"(headline|name)\":\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);

                        var urlMatches = urlPattern.Matches(jsonContent);
                        var titleMatches = titlePattern.Matches(jsonContent);

                        // Criar um conjunto para evitar duplicatas
                        var processedUrls = new HashSet<string>();

                        // Se encontramos URLs e títulos, vamos processá-los
                        if (urlMatches.Count > 0 && titleMatches.Count > 0)
                        {
                            for (int i = 0; i < Math.Min(urlMatches.Count, titleMatches.Count); i++)
                            {
                                if (i >= 10) break; // Limitar a 10 artigos

                                var url = urlMatches[i].Groups[1].Value;
                                var title = WebUtility.HtmlDecode(titleMatches[i].Groups[2].Value);

                                // Verificar se já processamos esta URL
                                if (processedUrls.Contains(url)) continue;
                                processedUrls.Add(url);

                                // Adicionar o artigo
                                if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(url))
                                {
                                    NewsFeed.Add(new NewsItem
                                    {
                                        Title = title,
                                        Url = url,
                                        Source = portal.Name
                                    });
                                    foundArticles = true;
                                }
                            }
                        }
                    }
                }

                return foundArticles;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao extrair dados estruturados: {ex.Message}");
                return false;
            }
        }

        // Método para habilitar/desabilitar o modo de depuração
        private void ToggleDebugMode(object sender, EventArgs e)
        {
            _debugMode = !_debugMode;

            if (_debugMode)
            {
                DisplayAlert("Depuração Ativada", "O modo de depuração foi ativado. Detalhes do processamento serão exibidos no console.", "OK");
            }
            else
            {
                DisplayAlert("Depuração Desativada", "O modo de depuração foi desativado.", "OK");
            }
        }

        // Método para exportar logs para arquivo
        private async void ExportLogs(object sender, EventArgs e)
        {
            try
            {
                // Criar uma string com os logs (simulando captura dos logs do console)
                StringBuilder logBuilder = new StringBuilder();
                logBuilder.AppendLine($"=== YourFeed Games - Log de Depuração - {DateTime.Now} ===");
                logBuilder.AppendLine($"Versão do App: 1.0.0");
                logBuilder.AppendLine($"Dispositivo: {DeviceInfo.Manufacturer} {DeviceInfo.Model}");
                logBuilder.AppendLine($"Sistema: {DeviceInfo.Platform} {DeviceInfo.VersionString}");
                logBuilder.AppendLine("====================================");

                // Em um app real, você pode capturar os logs do Console
                // Aqui estamos simulando com informações básicas
                foreach (var portal in new[] { "Flow Games", "Gameplayscassi", "The Enemy", "IGN Brasil" })
                {
                    int count = NewsFeed.Count(item => item.Source == portal);
                    logBuilder.AppendLine($"Portal {portal}: {count} notícias carregadas");
                }

                string logContent = logBuilder.ToString();

                // Em um app real, salve em um arquivo e compartilhe
                await DisplayAlert("Logs Exportados",
                    "Os logs foram exportados.\n\n" + logContent,
                    "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", $"Não foi possível exportar os logs: {ex.Message}", "OK");
            }
        }

        private async Task LoadNewsFeed()
        {
            try
            {
                // Inicializa o estado de carregamento
                _loadingCts = new CancellationTokenSource();
                _loadingStartTime = DateTime.Now;

                // Atualiza a UI para estado de carregamento
                Device.BeginInvokeOnMainThread(() =>
                {
                    NewsFeed.Clear();
                    feedScrollView.IsVisible = false;
                    loadingContainer.IsVisible = true;
                    cancelLoadingButton.IsVisible = true;
                    loadingProgress.Progress = 0;
                    loadingLabel.Text = "Preparando para carregar notícias...";
                    statusLabel.Text = "Iniciando...";
                });

                // Lista de portais habilitados
                var enabledPortals = new List<NewsPortal>
                {
                    new NewsPortal { Name = "FlowGames", Url = "https://flowgames.gg", IsEnabled = Preferences.Get("FlowGames", true) },
                    new NewsPortal { Name = "Gameplayscassi", Url = "https://gameplayscassi.com.br", IsEnabled = Preferences.Get("Gameplayscassi", true) },
                    new NewsPortal { Name = "OmeleteGames", Url = "https://www.omelete.com.br/games", IsEnabled = Preferences.Get("OmeleteGames", true) },
                    new NewsPortal { Name = "IGNBrasil", Url = "https://br.ign.com", IsEnabled = Preferences.Get("IGNBrasil", true) },
                    new NewsPortal { Name = "Voxel", Url = "https://www.tecmundo.com.br/voxel", IsEnabled = Preferences.Get("Voxel", true) },
                    new NewsPortal { Name = "GameVicio", Url = "https://www.gamevicio.com", IsEnabled = Preferences.Get("GameVicio", true) },
                    new NewsPortal { Name = "TechTudo", Url = "https://www.techtudo.com.br/jogos/", IsEnabled = Preferences.Get("TechTudo", true) },
                    new NewsPortal { Name = "Adrenaline", Url = "https://www.adrenaline.com.br/noticias/", IsEnabled = Preferences.Get("Adrenaline", true) },
                    new NewsPortal { Name = "ComboInfinito", Url = "https://www.comboinfinito.com.br/principal/", IsEnabled = Preferences.Get("ComboInfinito", true) },
                    new NewsPortal { Name = "Arkade", Url = "https://arkade.com.br/", IsEnabled = Preferences.Get("Arkade", true) }
                };

                var activePortals = enabledPortals.Where(p => p.IsEnabled).ToList();
                int totalPortals = activePortals.Count;
                int completedPortals = 0;

                _activePortals = activePortals;

                // Atualiza o status inicial
                await UpdateStatus($"Carregando {totalPortals} fontes de notícias...");
                await UpdateLoadingProgress(0, totalPortals);

                // Processa cada portal
                foreach (var portal in activePortals)
                {
                    // Verifica se o usuário cancelou
                    _loadingCts.Token.ThrowIfCancellationRequested();

                    try
                    {
                        // Atualiza o status para o portal atual
                        await UpdateStatus($"Conectando com {portal.Name}...");
                        await UpdateLoadingLabel($"Carregando notícias de {portal.Name}");

                        // Busca as notícias do portal
                        await FetchNewsFromPortal(portal);

                        // Atualiza o progresso
                        completedPortals++;
                        await UpdateLoadingProgress(completedPortals, totalPortals);
                        await UpdateStatus($"{completedPortals} de {totalPortals} portais carregados");
                    }
                    catch (OperationCanceledException)
                    {
                        await UpdateStatus("Carregamento cancelado pelo usuário");
                        return;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao carregar {portal.Name}: {ex.Message}");

                        // Adiciona um item de erro à lista
                        Device.BeginInvokeOnMainThread(() =>
                        {
                            NewsFeed.Add(new NewsItem
                            {
                                Title = $"Erro ao carregar {portal.Name}",
                                Description = ex.Message.Length > 100 ? ex.Message.Substring(0, 100) + "..." : ex.Message,
                                Source = portal.Name,
                                Url = portal.Url
                            });
                        });

                        await UpdateStatus($"Erro ao carregar {portal.Name}");
                    }
                }

                // Finalização com sucesso
                var loadingTime = DateTime.Now - _loadingStartTime;
                await UpdateStatus($"Carregamento completo - {NewsFeed.Count} notícias em {loadingTime.TotalSeconds:F1}s");

                // Ordena as notícias por fonte
                Device.BeginInvokeOnMainThread(() =>
                {
                    var sortedNews = new ObservableCollection<NewsItem>(
                        NewsFeed.OrderBy(item => item.Source));
                    NewsFeed.Clear();
                    foreach (var item in sortedNews)
                    {
                        NewsFeed.Add(item);
                    }

                    // Atualize AllNewsFeed para o filtro funcionar
                    AllNewsFeed.Clear();
                    foreach (var item in NewsFeed)
                        AllNewsFeed.Add(item);
                });
            }
            catch (OperationCanceledException)
            {
                await UpdateStatus("Carregamento cancelado pelo usuário");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro geral: {ex.Message}");
                await UpdateStatus("Erro ao carregar o feed");
                await DisplayAlert("Erro", "Houve um problema ao carregar o feed de notícias.", "OK");
            }
            finally
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    loadingContainer.IsVisible = false;
                    feedScrollView.IsVisible = true;
                    cancelLoadingButton.IsVisible = false;

                    if (!NewsFeed.Any())
                    {
                        statusLabel.Text = "Nenhuma notícia foi carregada. Verifique sua conexão.";
                    }

                    PopulatePortalPicker();
                });

                _loadingCts?.Dispose();
                _loadingCts = null;
            }
        }

        private void PopulatePortalPicker()
        {
            var items = new List<string>();
            if (_activePortals != null)
                items.AddRange(_activePortals.Select(p => p.Name));
            portalPicker.ItemsSource = items;
            portalPicker.SelectedIndex = -1; // Nenhum selecionado
        }

        // Métodos auxiliares para atualizar a UI
        private async Task UpdateStatus(string message)
        {
            await Device.InvokeOnMainThreadAsync(async () =>
            {
                await statusLabel.FadeTo(0, 100);
                statusLabel.Text = message;
                await statusLabel.FadeTo(1, 100);
            });
        }

        private async Task UpdateLoadingLabel(string message)
        {
            await Device.InvokeOnMainThreadAsync(() =>
            {
                loadingLabel.Text = message;
            });
        }

        private async Task UpdateLoadingProgress(int completed, int total)
        {
            await Device.InvokeOnMainThreadAsync(() =>
            {
                loadingProgress.Progress = (double)completed / total;
            });
        }

        // Método para o botão de cancelamento
        private void OnCancelLoadingClicked(object sender, EventArgs e)
        {
            _loadingCts?.Cancel();
            cancelLoadingButton.IsVisible = false;
        }

        private async void OnSettingsClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("///SettingsPage");
        }

        private async void OnHotNewsClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("///HotNewsPage");
        }

        private async void OnReadMoreClicked(object sender, EventArgs e)
        {
            var button = sender as Button;
            var url = button?.CommandParameter as string;

            if (!string.IsNullOrEmpty(url))
            {
                await Launcher.OpenAsync(new Uri(url));
            }
        }

        private async void OnRefreshClicked(object sender, EventArgs e)
        {
            _loadingCts?.Cancel();
            await Task.Delay(100);

            Device.BeginInvokeOnMainThread(() =>
            {
                portalPicker.SelectedIndex = -1; // Remove seleção
            });

            await LoadNewsFeed();
        }

        // Melhorar o FetchNewsFromPortal para lidar com timeouts e erros HTTP
        private async Task FetchNewsFromPortal(NewsPortal portal)
        {
            // Configurar um timeout para a requisição
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(portal.Url, cts.Token);

                // Verificar se a requisição foi bem-sucedida
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Erro HTTP para {portal.Name}: {response.StatusCode}");
                    throw new HttpRequestException($"Status code: {response.StatusCode}");
                }

                // Obter o conteúdo HTML
                string htmlContent = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrEmpty(htmlContent))
                {
                    Console.WriteLine($"Resposta vazia de {portal.Name}");
                    return;
                }

                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(htmlContent);

                switch (portal.Name)
                {
                    case "FlowGames":
                        ParseFlowGames(htmlDoc, portal);
                        break;
                    case "Gameplayscassi":
                        ParseGameplayscassi(htmlDoc, portal);
                        break;
                    case "OmeleteGames":
                        ParseOmeleteGames(htmlDoc, portal);
                        break;
                    case "IGNBrasil":
                        ParseIGNBrasil(htmlDoc, portal);
                        break;
                    case "Voxel":
                        ParseVoxel(htmlDoc, portal);
                        break;
                    case "GameVicio":
                        ParseGameVicio(htmlDoc, portal);
                        break;
                    case "TechTudo":
                        ParseTechTudo(htmlDoc, portal);
                        break;
                    case "Adrenaline":
                        ParseAdrenaline(htmlDoc, portal);
                        break;
                    case "ComboInfinito":
                        ParseComboInfinito(htmlDoc, portal);
                        break;
                    case "Arkade":
                        ParseArkade(htmlDoc, portal);
                        break;
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine($"Timeout ao conectar com {portal.Name}");
                throw new TimeoutException($"A conexão com {portal.Name} excedeu o tempo limite.");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Erro HTTP: {ex.Message}");
                throw;
            }
        }

        private void ParseArkade(HtmlDocument htmlDoc, NewsPortal portal)
        {
            try
            {
                Console.WriteLine("Tentando parsear Arkade...");

                // Exibir algumas informações sobre o documento HTML para diagnóstico
                Console.WriteLine($"URL do Portal: {portal.Url}");
                Console.WriteLine($"Título da página: {htmlDoc.DocumentNode.SelectSingleNode("//title")?.InnerText}");

                // Tentativa 1: Artigos no formato de cards ou posts de destaque
                var newsNodes = htmlDoc.DocumentNode.SelectNodes("//article");

                // Tentativa 2: Se não encontrar artigos, tentar divs de posts
                if (newsNodes == null || !newsNodes.Any())
                {
                    newsNodes = htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 'post') or contains(@class, 'card') or contains(@class, 'article')]");
                }

                if (newsNodes != null && newsNodes.Any())
                {
                    Console.WriteLine($"Encontrados {newsNodes.Count} nós de notícia no Arkade");

                    // Vamos examinar detalhadamente o primeiro nó
                    var firstNode = newsNodes.FirstOrDefault();
                    if (firstNode != null)
                    {
                        Console.WriteLine("Estrutura do primeiro nó:");
                        Console.WriteLine($"HTML parcial do nó: {firstNode.OuterHtml.Substring(0, Math.Min(500, firstNode.OuterHtml.Length))}...");

                        // Examinar todos os links
                        var allLinks = firstNode.SelectNodes(".//a");
                        Console.WriteLine($"Links encontrados no primeiro nó: {(allLinks?.Count ?? 0)}");
                        if (allLinks != null)
                        {
                            foreach (var link in allLinks.Take(3))
                            {
                                Console.WriteLine($"Link: {link.GetAttributeValue("href", "")} - Texto: {link.InnerText.Trim()}");
                            }
                        }

                        // Verificar headings
                        var headings = firstNode.SelectNodes(".//*[self::h1 or self::h2 or self::h3 or self::h4]");
                        Console.WriteLine($"Headings encontrados no primeiro nó: {(headings?.Count ?? 0)}");
                        if (headings != null)
                        {
                            foreach (var heading in headings)
                            {
                                Console.WriteLine($"Heading: {heading.Name} - Texto: {heading.InnerText.Trim()}");
                            }
                        }

                        // Verificar imagens
                        var images = firstNode.SelectNodes(".//img");
                        Console.WriteLine($"Imagens encontradas no primeiro nó: {(images?.Count ?? 0)}");
                        if (images != null)
                        {
                            foreach (var img in images.Take(2))
                            {
                                Console.WriteLine($"Imagem: {img.GetAttributeValue("src", "")}");
                                Console.WriteLine($"  Alt: {img.GetAttributeValue("alt", "")}");
                                Console.WriteLine($"  Data-src: {img.GetAttributeValue("data-src", "")}");
                            }
                        }
                    }

                    var processedUrls = new HashSet<string>();
                    int addedCount = 0;

                    foreach (var node in newsNodes.Take(10))
                    {
                        try
                        {
                            // Estratégias para encontrar título e URL
                            string title = null;
                            string url = null;
                            string imageUrl = null;

                            // Estratégia 1: Heading com link
                            var headingLink = node.SelectSingleNode(".//*[self::h1 or self::h2 or self::h3 or self::h4]//a");
                            if (headingLink != null)
                            {
                                title = headingLink.InnerText.Trim();
                                url = headingLink.GetAttributeValue("href", "");
                                Console.WriteLine($"Título encontrado em heading: {title}");
                            }

                            // Estratégia 2: Link com classe específica de título
                            if (string.IsNullOrEmpty(title))
                            {
                                var titleLink = node.SelectSingleNode(".//a[contains(@class, 'title') or contains(@class, 'heading') or contains(@class, 'post-title')]");
                                if (titleLink != null)
                                {
                                    title = titleLink.InnerText.Trim();
                                    url = titleLink.GetAttributeValue("href", "");
                                    Console.WriteLine($"Título encontrado por classe: {title}");
                                }
                            }

                            // Estratégia 3: Qualquer link relevante na estrutura
                            if (string.IsNullOrEmpty(title))
                            {
                                var links = node.SelectNodes(".//a");
                                if (links != null)
                                {
                                    foreach (var link in links)
                                    {
                                        var linkText = link.InnerText.Trim();
                                        if (linkText.Length > 15 && linkText.Length < 150) // Critério para parecer um título
                                        {
                                            title = linkText;
                                            url = link.GetAttributeValue("href", "");
                                            Console.WriteLine($"Título encontrado por tamanho: {title}");
                                            break;
                                        }
                                    }
                                }
                            }

                            // Estratégias para encontrar imagem

                            // Estratégia 1: Imagem dentro do nó
                            var imgNode = node.SelectSingleNode(".//img");
                            if (imgNode != null)
                            {
                                // Tentar vários atributos para a imagem
                                imageUrl = imgNode.GetAttributeValue("src", "");
                                if (string.IsNullOrEmpty(imageUrl) || imageUrl.Contains("placeholder") || imageUrl.Contains("blank.gif"))
                                {
                                    imageUrl = imgNode.GetAttributeValue("data-src", "");
                                }
                                if (string.IsNullOrEmpty(imageUrl) || imageUrl.Contains("placeholder") || imageUrl.Contains("blank.gif"))
                                {
                                    imageUrl = imgNode.GetAttributeValue("data-lazy-src", "");
                                }

                                Console.WriteLine($"Imagem encontrada: {imageUrl}");
                            }

                            // Estratégia 2: Elemento de background com URL de imagem
                            if (string.IsNullOrEmpty(imageUrl))
                            {
                                var elemWithBg = node.SelectSingleNode(".//*[@style[contains(., 'background')]]");
                                if (elemWithBg != null)
                                {
                                    var style = elemWithBg.GetAttributeValue("style", "");
                                    var match = Regex.Match(style, @"url\(['""](.*?)['""]\)");
                                    if (match.Success)
                                    {
                                        imageUrl = match.Groups[1].Value;
                                        Console.WriteLine($"Imagem encontrada em background: {imageUrl}");
                                    }
                                }
                            }

                            // Garantir que a URL seja absoluta
                            if (!string.IsNullOrEmpty(url) && !url.StartsWith("http"))
                            {
                                url = new Uri(new Uri(portal.Url), url).ToString();
                                Console.WriteLine($"URL convertida para absoluta: {url}");
                            }

                            if (!string.IsNullOrEmpty(imageUrl) && !imageUrl.StartsWith("http"))
                            {
                                imageUrl = new Uri(new Uri(portal.Url), imageUrl).ToString();
                                Console.WriteLine($"URL de imagem convertida para absoluta: {imageUrl}");
                            }

                            // Adicionar notícia ao feed
                            if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(url) && !processedUrls.Contains(url))
                            {
                                NewsFeed.Add(new NewsItem
                                {
                                    Title = title,
                                    Url = url,
                                    Source = portal.Name,
                                    ImageUrl = imageUrl
                                });
                                processedUrls.Add(url);
                                addedCount++;
                                Console.WriteLine($"Notícia adicionada ao feed: {title}");
                            }
                            else
                            {
                                Console.WriteLine("Notícia não adicionada: " +
                                    (string.IsNullOrEmpty(title) ? "Título vazio" : "") +
                                    (string.IsNullOrEmpty(url) ? " URL vazia" : "") +
                                    (processedUrls.Contains(url) ? " URL duplicada" : ""));
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Erro ao processar nó individual do Arkade: {ex.Message}");
                        }
                    }

                    Console.WriteLine($"Total de notícias do Arkade adicionadas ao feed: {addedCount}");
                }
                else
                {
                    Console.WriteLine("Nenhuma notícia encontrada no Arkade");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao parsear Arkade: {ex.Message}");
            }
        }
        private void ParseComboInfinito(HtmlDocument htmlDoc, NewsPortal portal)
        {
            try
            {
                Console.WriteLine("Tentando parsear Combo Infinito...");

                // Exibir algumas informações sobre o documento HTML para diagnóstico
                Console.WriteLine($"URL do Portal: {portal.Url}");
                Console.WriteLine($"Título da página: {htmlDoc.DocumentNode.SelectSingleNode("//title")?.InnerText}");

                // Selecionar os nós das notícias do Combo Infinito
                var newsNodes = htmlDoc.DocumentNode.SelectNodes("//article[contains(@class, 'post')]");
                if (newsNodes != null && newsNodes.Any())
                {
                    Console.WriteLine($"Encontrados {newsNodes.Count} nós de notícia no Combo Infinito");

                    // Vamos examinar detalhadamente os primeiros nós
                    var firstNode = newsNodes.FirstOrDefault();
                    if (firstNode != null)
                    {
                        Console.WriteLine("Estrutura do primeiro nó:");
                        Console.WriteLine($"HTML completo do nó: {firstNode.OuterHtml.Substring(0, Math.Min(500, firstNode.OuterHtml.Length))}...");

                        // Examinar todos os elementos a em busca de títulos
                        var allLinks = firstNode.SelectNodes(".//a");
                        Console.WriteLine($"Links encontrados no primeiro nó: {(allLinks?.Count ?? 0)}");
                        if (allLinks != null)
                        {
                            foreach (var link in allLinks.Take(5))
                            {
                                Console.WriteLine($"Link encontrado: {link.GetAttributeValue("href", "")} - Texto: {link.InnerText.Trim()}");
                            }
                        }

                        // Verificar todos os elementos h2, h3, h4 que possam conter títulos
                        var headings = firstNode.SelectNodes(".//*[self::h2 or self::h3 or self::h4]");
                        Console.WriteLine($"Headings encontrados no primeiro nó: {(headings?.Count ?? 0)}");
                        if (headings != null)
                        {
                            foreach (var heading in headings)
                            {
                                Console.WriteLine($"Heading encontrado: {heading.Name} - Texto: {heading.InnerText.Trim()}");
                                var headingLink = heading.SelectSingleNode(".//a");
                                if (headingLink != null)
                                {
                                    Console.WriteLine($"  - Link no heading: {headingLink.GetAttributeValue("href", "")}");
                                }
                            }
                        }
                    }

                    // Tentar identificar padrões nos primeiros 10 nós
                    var processedUrls = new HashSet<string>();
                    int addedCount = 0;

                    foreach (var node in newsNodes.Take(10))
                    {
                        try
                        {
                            // Tentar diferentes seletores para encontrar o título e URL
                            string title = null;
                            string url = null;

                            // Tentativa 1: Links diretos com classe que indica que são títulos
                            var titleLink = node.SelectSingleNode(".//a[contains(@class, 'title') or contains(@class, 'heading') or contains(@class, 'post-title')]");
                            if (titleLink != null)
                            {
                                title = titleLink.InnerText.Trim();
                                url = titleLink.GetAttributeValue("href", "");
                                Console.WriteLine($"Encontrado por classe de link: {title}");
                            }

                            // Tentativa 2: Qualquer heading (h1-h4) que contenha um link
                            if (string.IsNullOrEmpty(title))
                            {
                                var headingWithLink = node.SelectSingleNode(".//*[self::h1 or self::h2 or self::h3 or self::h4]//a");
                                if (headingWithLink != null)
                                {
                                    title = headingWithLink.InnerText.Trim();
                                    url = headingWithLink.GetAttributeValue("href", "");
                                    Console.WriteLine($"Encontrado por heading com link: {title}");
                                }
                            }

                            // Tentativa 3: Qualquer link que pareça ser o título principal do artigo
                            if (string.IsNullOrEmpty(title))
                            {
                                // Pegar o primeiro link grande o suficiente para ser um título
                                var links = node.SelectNodes(".//a");
                                if (links != null)
                                {
                                    foreach (var link in links)
                                    {
                                        var linkText = link.InnerText.Trim();
                                        if (linkText.Length > 10 && linkText.Length < 150) // Parece um título razoável
                                        {
                                            title = linkText;
                                            url = link.GetAttributeValue("href", "");
                                            Console.WriteLine($"Encontrado por tamanho de texto: {title}");
                                            break;
                                        }
                                    }
                                }
                            }

                            // Extrair imagem - tentar diferentes padrões
                            var imgNode = node.SelectSingleNode(".//img");
                            var imageUrl = "";

                            if (imgNode != null)
                            {
                                imageUrl = imgNode.GetAttributeValue("src", "");
                                if (string.IsNullOrEmpty(imageUrl))
                                {
                                    imageUrl = imgNode.GetAttributeValue("data-src", "");
                                }
                                if (string.IsNullOrEmpty(imageUrl))
                                {
                                    imageUrl = imgNode.GetAttributeValue("data-lazy-src", "");
                                }
                            }

                            // Garantir que a URL seja absoluta
                            if (!string.IsNullOrEmpty(url) && !url.StartsWith("http"))
                            {
                                url = new Uri(new Uri(portal.Url), url).ToString();
                            }

                            if (!string.IsNullOrEmpty(imageUrl) && !imageUrl.StartsWith("http"))
                            {
                                imageUrl = new Uri(new Uri(portal.Url), imageUrl).ToString();
                            }

                            // Adicionar notícia ao feed
                            if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(url) && !processedUrls.Contains(url))
                            {
                                NewsFeed.Add(new NewsItem
                                {
                                    Title = title,
                                    Url = url,
                                    Source = portal.Name,
                                    ImageUrl = imageUrl
                                });
                                processedUrls.Add(url);
                                addedCount++;
                                Console.WriteLine($"Notícia adicionada ao feed: {title}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Erro ao processar nó individual do Combo Infinito: {ex.Message}");
                        }
                    }

                    Console.WriteLine($"Total de notícias do Combo Infinito adicionadas ao feed: {addedCount}");
                }
                else
                {
                    Console.WriteLine("Nenhuma notícia encontrada no Combo Infinito");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao parsear Combo Infinito: {ex.Message}");
            }
        }
        private void ParseAdrenaline(HtmlDocument htmlDoc, NewsPortal portal)
        {
            try
            {
                Console.WriteLine("Tentando parsear Adrenaline...");
                var processedUrls = new HashSet<string>();
                int addedCount = 0;

                // Lista de títulos a serem ignorados
                var titulosIgnorados = new[]
                {
            "0 Só demorou 25 anos REVIEW | Fatal Fury: City of the Wolves é a volta que a série merecia",
            "0 Soulslike que segue a receita REVIEW | The First Berserker: Khazan é um soulslike básico e competente",
            "0 Merece sua atenção REVIEW | Split Fiction é videogame em sua forma mais pura",
            "0 A luta pela sobrevivência continua REVIEW | Frostpunk 2 mostra o que acontece depois do fim do mundo"
        };

                var newsNodes = htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 'article-card')]") ??
                                htmlDoc.DocumentNode.SelectNodes("//article");

                if (newsNodes != null && newsNodes.Any())
                {
                    Console.WriteLine($"Encontrados {newsNodes.Count} nós de notícia no Adrenaline");

                    foreach (var node in newsNodes)
                    {
                        if (addedCount >= 10) break; // Só para quando realmente adicionou 10 notícias válidas

                        var linkNode = node.SelectSingleNode(".//a[contains(@class, 'title')] | .//h2/a | .//h3/a | .//a[contains(@class, 'post-title')] | .//h2//a | .//h3//a | .//a[contains(@class, 'headline')]");
                        if (linkNode == null)
                        {
                            linkNode = node.SelectSingleNode(".//a[@href]");
                        }

                        if (linkNode != null)
                        {
                            var url = linkNode.GetAttributeValue("href", "").Trim();
                            var title = CleanHtml(linkNode.InnerText).Trim();

                            // Ignorar títulos indesejados
                            if (titulosIgnorados.Contains(title))
                            {
                                Console.WriteLine($"Notícia ignorada: {title}");
                                continue;
                            }

                            // Garantir que a URL seja absoluta
                            if (!string.IsNullOrEmpty(url))
                            {
                                if (!url.StartsWith("http"))
                                {
                                    url = new Uri(new Uri(portal.Url), url).ToString();
                                }

                                if (url.Contains("#comments") || url.Contains("page=") || title.Length < 10)
                                {
                                    Console.WriteLine($"URL ignorada: {url}");
                                    continue;
                                }

                                if (!string.IsNullOrEmpty(title) && !processedUrls.Contains(url))
                                {
                                    NewsFeed.Add(new NewsItem
                                    {
                                        Title = title,
                                        Url = url,
                                        Source = portal.Name
                                    });
                                    processedUrls.Add(url);
                                    addedCount++;
                                    Console.WriteLine($"Adicionado: {title}");
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("Link não encontrado neste nó");
                        }
                    }
                }

                // Se não conseguiu extrair nenhuma notícia com a primeira abordagem
                if (addedCount == 0)
                {
                    Console.WriteLine("Tentando abordagem alternativa para Adrenaline");

                    // Buscar por links que contenham "/noticias/" ou "/artigos/" no href
                    var allLinks = htmlDoc.DocumentNode.SelectNodes("//a[contains(@href, '/noticias/') or contains(@href, '/artigos/')]");

                    if (allLinks != null && allLinks.Any())
                    {
                        Console.WriteLine($"Encontrados {allLinks.Count} links de notícias/artigos");

                        foreach (var link in allLinks)
                        {
                            if (addedCount >= 10) break;

                            var url = link.GetAttributeValue("href", "").Trim();
                            var title = CleanHtml(link.InnerText).Trim();

                            // Debug info
                            Console.WriteLine($"URL Alt Extraída: '{url}'");
                            Console.WriteLine($"Título Alt Extraído: '{title}'");

                            // Filtrar links inválidos
                            if (string.IsNullOrEmpty(url) ||
                                url.Contains("#") ||
                                url.Contains("page=") ||
                                title.Length < 10)
                            {
                                continue;
                            }

                            // Garantir URL absoluta
                            if (!url.StartsWith("http"))
                            {
                                url = new Uri(new Uri(portal.Url), url).ToString();
                            }

                            // Adicionar ao feed se não for duplicado
                            if (!processedUrls.Contains(url))
                            {
                                NewsFeed.Add(new NewsItem
                                {
                                    Title = title,
                                    Url = url,
                                    Source = portal.Name
                                });
                                processedUrls.Add(url);
                                addedCount++;
                                Console.WriteLine($"Adicionado (alternativo): {title}");
                            }
                        }
                    }
                }

                // Log final de status
                if (addedCount > 0)
                {
                    Console.WriteLine($"Adicionadas {addedCount} notícias do Adrenaline ao feed");
                }
                else
                {
                    Console.WriteLine("Não foi possível extrair notícias do Adrenaline");

                    // Tentativa final: Usar JSON estruturado se existir
                    TryExtractArticlesFromStructuredData(htmlDoc, portal);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao parsear Adrenaline: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        private void ParseVoxel(HtmlDocument htmlDoc, NewsPortal portal)
        {
            try
            {
                Console.WriteLine("Tentando parsear Voxel (TecMundo)...");

                // Tentativa 1: Notícias principais no novo formato TecMundo/Voxel
                var newsNodes = htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 'tec--card__content')]//a[contains(@class, 'tec--card__title__link')]");

                // Tentativa 2: Notícias em listagem
                if (newsNodes == null || !newsNodes.Any())
                {
                    newsNodes = htmlDoc.DocumentNode.SelectNodes("//article[contains(@class, 'tec--card')]//h3/a");
                }

                // Tentativa 3: Notícias em formato de manchete
                if (newsNodes == null || !newsNodes.Any())
                {
                    newsNodes = htmlDoc.DocumentNode.SelectNodes("//a[contains(@class, 'tec--card__title__link')]");
                }

                if (newsNodes != null && newsNodes.Any())
                {
                    Console.WriteLine($"Encontrados {newsNodes.Count} nós de notícia no Voxel");

                    var processedUrls = new HashSet<string>();
                    foreach (var node in newsNodes.Take(10))
                    {
                        var url = node.GetAttributeValue("href", "");
                        var title = CleanHtml(node.InnerText.Trim());

                        // Corrigir URL para o formato correto
                        if (!string.IsNullOrEmpty(url))
                        {
                            if (url.StartsWith("/voxel"))
                            {
                                url = "https://www.tecmundo.com.br" + url;
                            }
                            else if (!url.StartsWith("http"))
                            {
                                url = new Uri(new Uri("https://www.tecmundo.com.br/voxel"), url).ToString();
                            }
                        }

                        if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(url) && !processedUrls.Contains(url))
                        {
                            NewsFeed.Add(new NewsItem
                            {
                                Title = title,
                                Url = url,
                                Source = portal.Name
                            });
                            processedUrls.Add(url);
                            Console.WriteLine($"Adicionado: {title}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Nenhuma notícia encontrada no Voxel - Tentando extrair do JSON embutido");
                    TryExtractArticlesFromStructuredData(htmlDoc, portal);

                    // Se ainda não encontrou, tentar uma abordagem mais agressiva
                    if (!NewsFeed.Any(item => item.Source == portal.Name))
                    {
                        Console.WriteLine("Tentando abordagem alternativa para Voxel");
                        ParseVoxelAlternative(htmlDoc, portal);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao parsear Voxel: {ex.Message}");
            }
        }

        // Método alternativo para quando o parsing principal falhar
        private void ParseVoxelAlternative(HtmlDocument htmlDoc, NewsPortal portal)
        {
            try
            {
                // Tentar encontrar qualquer link que pareça ser de notícia
                var allLinks = htmlDoc.DocumentNode.SelectNodes("//a[contains(@href, '/noticia/') or contains(@href, '/voxel/')]");

                if (allLinks != null && allLinks.Any())
                {
                    var processedUrls = new HashSet<string>();
                    var addedCount = 0;

                    foreach (var node in allLinks)
                    {
                        if (addedCount >= 10) break;

                        var url = node.GetAttributeValue("href", "");
                        var title = CleanHtml(node.InnerText.Trim());

                        // Filtrar URLs que não são de notícias
                        if (string.IsNullOrEmpty(url) ||
                            url.Contains("/tag/") ||
                            url.Contains("/busca/") ||
                            url.Contains("/autor/") ||
                            title.Length < 10)
                        {
                            continue;
                        }

                        // Corrigir URL
                        if (!url.StartsWith("http"))
                        {
                            url = "https://www.tecmundo.com.br" + (url.StartsWith("/") ? url : "/" + url);
                        }

                        if (!processedUrls.Contains(url))
                        {
                            NewsFeed.Add(new NewsItem
                            {
                                Title = title,
                                Url = url,
                                Source = portal.Name
                            });
                            processedUrls.Add(url);
                            addedCount++;
                            Console.WriteLine($"Adicionado (alternativo): {title}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro no parser alternativo do Voxel: {ex.Message}");
            }
        }

        private void ParseGameVicio(HtmlDocument htmlDoc, NewsPortal portal)
        {
            try
            {
                Console.WriteLine("Tentando parsear GameVicio...");

                // Tente buscar por links de notícias de forma mais genérica
                var newsLinks = htmlDoc.DocumentNode.SelectNodes("//a[contains(@href, '/noticia/') or contains(@href, '/noticias/')]");
                if (newsLinks == null || !newsLinks.Any())
                {
                    Console.WriteLine("Nenhum link de notícia encontrado no GameVicio");
                    TryExtractArticlesFromStructuredData(htmlDoc, portal);
                    return;
                }

                var processedUrls = new HashSet<string>();
                int addedCount = 0;

                foreach (var node in newsLinks)
                {
                    if (addedCount >= 15) break;

                    var url = node.GetAttributeValue("href", "");
                    var title = node.GetAttributeValue("title", "");

                    if (string.IsNullOrEmpty(title))
                        title = CleanHtml(node.InnerText);

                    if (!string.IsNullOrEmpty(url) && !url.StartsWith("http"))
                        url = new Uri(new Uri(portal.Url), url).ToString();

                    if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(url) && !processedUrls.Contains(url))
                    {
                        NewsFeed.Add(new NewsItem
                        {
                            Title = title,
                            Url = url,
                            Source = portal.Name
                        });
                        processedUrls.Add(url);
                        addedCount++;
                        Console.WriteLine($"Adicionado: {title}");
                    }
                }

                if (addedCount == 0)
                    Console.WriteLine("Nenhuma notícia válida adicionada do GameVicio.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao parsear GameVicio: {ex.Message}");
            }
        }
        private void ParseIGNBrasil(HtmlDocument htmlDoc, NewsPortal portal)
        {
            var newsNodes = htmlDoc.DocumentNode.SelectNodes("//div[@class='item-title']/a") ??
                           htmlDoc.DocumentNode.SelectNodes("//article//h3/a") ??
                           htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 'article')]//a");

            if (newsNodes != null && newsNodes.Any())
            {
                foreach (var node in newsNodes.Take(10))
                {
                    var title = CleanHtml(node.InnerText);
                    var url = node.GetAttributeValue("href", "");

                    // Garantir que a URL seja absoluta
                    if (!string.IsNullOrEmpty(url) && !url.StartsWith("http"))
                    {
                        url = new Uri(new Uri(portal.Url), url).ToString();
                    }

                    if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(url))
                    {
                        NewsFeed.Add(new NewsItem
                        {
                            Title = title,
                            Url = url,
                            Source = portal.Name
                        });
                    }
                }
            }
        }
        private void ParseFlowGames(HtmlDocument htmlDoc, NewsPortal portal)
        {
            // Primeiro, tente o XPath original
            var newsNodes = htmlDoc.DocumentNode.SelectNodes("//ul[@class='list-post']/li");

            // Se não funcionar, tente outros seletores mais genéricos
            if (newsNodes == null || !newsNodes.Any())
            {
                // Tentativa alternativa: procurar por artigos ou posts
                newsNodes = htmlDoc.DocumentNode.SelectNodes("//article") ??
                            htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 'post')]") ??
                            htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 'article')]");
            }

            if (newsNodes != null && newsNodes.Any())
            {
                foreach (var node in newsNodes.Take(10)) // Limitar a 10 notícias
                {
                    var titleNode = node.SelectSingleNode(".//h2") ??
                                    node.SelectSingleNode(".//h3") ??
                                    node.SelectSingleNode(".//a[@title]");

                    var linkNode = node.SelectSingleNode(".//a[@href]");

                    if (titleNode != null && linkNode != null)
                    {
                        var title = CleanHtml(titleNode.InnerText);
                        var url = linkNode.GetAttributeValue("href", "");

                        // Garantir que a URL seja absoluta
                        if (!string.IsNullOrEmpty(url) && !url.StartsWith("http"))
                        {
                            url = new Uri(new Uri(portal.Url), url).ToString();
                        }

                        if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(url))
                        {
                            NewsFeed.Add(new NewsItem
                            {
                                Title = title,
                                Url = url,
                                Source = portal.Name
                            });
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine($"Nenhum nó de notícia encontrado para {portal.Name}");
            }
        }

        private void ParseGameplayscassi(HtmlDocument htmlDoc, NewsPortal portal)
        {
            Console.WriteLine("Tentando analisar Gameplayscassi com nova abordagem...");

            // Primeiro, tentamos extrair notícias da estrutura do site
            var newsNodes = htmlDoc.DocumentNode.SelectNodes("//article[contains(@class, 'post')]") ??
                           htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 'post')]");

            if (newsNodes != null && newsNodes.Any())
            {
                Console.WriteLine($"Encontrados {newsNodes.Count} artigos de notícias");

                foreach (var node in newsNodes.Take(10))
                {
                    var linkNode = node.SelectSingleNode(".//h2/a") ??
                                 node.SelectSingleNode(".//h3/a") ??
                                 node.SelectSingleNode(".//a[contains(@class, 'post-title')]");

                    if (linkNode != null)
                    {
                        var url = linkNode.GetAttributeValue("href", "");
                        var title = CleanHtml(linkNode.InnerText);

                        if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(title))
                        {
                            Console.WriteLine($"Adicionando notícia: {title} - {url}");
                            NewsFeed.Add(new NewsItem
                            {
                                Title = title,
                                Url = url,
                                Source = portal.Name
                            });
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("Nenhum artigo encontrado, tentando abordagem alternativa via links...");

                // Abordagem alternativa para extrair de links de notícias
                var allLinks = htmlDoc.DocumentNode.SelectNodes("//a[@href]");
                var newsLinks = new List<HtmlNode>();

                if (allLinks != null)
                {
                    foreach (var link in allLinks)
                    {
                        var href = link.GetAttributeValue("href", "");

                        // Filtra links que parecem ser de notícias
                        if (!string.IsNullOrEmpty(href) &&
                            href.Contains("/noticias/") &&
                            href.Contains(portal.Url) &&
                            !href.EndsWith("/noticias/"))
                        {
                            newsLinks.Add(link);
                        }
                    }
                }

                if (newsLinks.Any())
                {
                    Console.WriteLine($"Encontrados {newsLinks.Count} links de notícias");

                    var processedUrls = new HashSet<string>();

                    foreach (var node in newsLinks.Take(15))
                    {
                        var url = node.GetAttributeValue("href", "");

                        if (string.IsNullOrEmpty(url) || processedUrls.Contains(url))
                            continue;

                        processedUrls.Add(url);

                        // Extrai título da URL quando não está no texto do link
                        string title = CleanHtml(node.InnerText);

                        if (string.IsNullOrWhiteSpace(title) || title.All(char.IsDigit))
                        {
                            // Extrai o título da parte descritiva da URL
                            var urlParts = url.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                            var lastPart = urlParts.LastOrDefault(p => !string.IsNullOrEmpty(p) && !p.All(char.IsDigit));

                            if (lastPart != null)
                            {
                                title = WebUtility.UrlDecode(lastPart)
                                    .Replace("-", " ")
                                    .Trim();

                                // Capitaliza a primeira letra
                                if (title.Length > 0)
                                {
                                    title = char.ToUpper(title[0]) + title.Substring(1);
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(url))
                        {
                            Console.WriteLine($"Adicionando notícia: {title} - {url}");
                            NewsFeed.Add(new NewsItem
                            {
                                Title = title,
                                Url = url,
                                Source = portal.Name
                            });
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Nenhum link de notícia encontrado.");
                }
            }
        }

        //private void ParseTheEnemy(HtmlDocument htmlDoc, NewsPortal portal)
        //{
        //    Console.WriteLine("Tentando analisar The Enemy com nova abordagem...");

        //    // O site TheEnemy provavelmente usa JavaScript para renderizar o conteúdo
        //    // Vamos tentar uma abordagem diferente, buscando por links que parecem ser de artigos

        //    // Opção 1: Buscar por scripts que contenham dados de artigos embutidos (JSON)
        //    var scriptNodes = htmlDoc.DocumentNode.SelectNodes("//script");
        //    var articlesFound = false;

        //    if (scriptNodes != null)
        //    {
        //        Console.WriteLine($"Encontrados {scriptNodes.Count} scripts para análise");

        //        foreach (var script in scriptNodes)
        //        {
        //            var scriptContent = script.InnerText;

        //            // Procurar por padrões que pareçam conter URLs de artigos
        //            if (scriptContent.Contains("\"url\":") &&
        //                (scriptContent.Contains("\"title\":") || scriptContent.Contains("\"headline\":")))
        //            {
        //                try
        //                {
        //                    Console.WriteLine("Encontrado possível script com dados de artigos");

        //                    // Extrair URLs e títulos usando regex
        //                    var urlPattern = new Regex("\"url\":\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
        //                    var titlePattern = new Regex("\"(title|headline)\":\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);

        //                    var urlMatches = urlPattern.Matches(scriptContent);
        //                    var titleMatches = titlePattern.Matches(scriptContent);

        //                    // Se encontramos algumas URLs e títulos, vamos tentar usá-los
        //                    if (urlMatches.Count > 0 && titleMatches.Count > 0 &&
        //                        urlMatches.Count == titleMatches.Count)
        //                    {
        //                        Console.WriteLine($"Extraídos {urlMatches.Count} pares de URL/título do script");

        //                        for (int i = 0; i < Math.Min(10, urlMatches.Count); i++)
        //                        {
        //                            var url = urlMatches[i].Groups[1].Value;
        //                            var title = WebUtility.HtmlDecode(titleMatches[i].Groups[2].Value);

        //                            // Verificar se a URL é do próprio site e não é uma URL de imagem ou outro recurso
        //                            if (url.Contains("theenemy.com.br") &&
        //                                !url.EndsWith(".jpg") && !url.EndsWith(".png") &&
        //                                !url.EndsWith(".css") && !url.EndsWith(".js"))
        //                            {
        //                                NewsFeed.Add(new NewsItem
        //                                {
        //                                    Title = title,
        //                                    Url = url,
        //                                    Source = portal.Name
        //                                });
        //                                articlesFound = true;
        //                            }
        //                        }
        //                    }
        //                }
        //                catch (Exception ex)
        //                {
        //                    Console.WriteLine($"Erro ao processar script: {ex.Message}");
        //                }
        //            }
        //        }
        //    }

        //    // Se não encontramos artigos nos scripts, tente a abordagem alternativa
        //    if (!articlesFound)
        //    {
        //        // Buscar por links com características de artigos
        //        var allLinks = htmlDoc.DocumentNode.SelectNodes("//a[@href]");

        //        if (allLinks != null)
        //        {
        //            Console.WriteLine($"Analisando {allLinks.Count} links");

        //            // Filtrar links que parecem ser de artigos
        //            var articlePattern = new Regex(@"/(noticia|review|artigo|coluna|noticias)/", RegexOptions.IgnoreCase);
        //            var newsLinks = new List<HtmlNode>();

        //            foreach (var link in allLinks)
        //            {
        //                var href = link.GetAttributeValue("href", "");

        //                // Verificar se parece ser um link de artigo
        //                if (!string.IsNullOrEmpty(href) &&
        //                    (articlePattern.IsMatch(href) ||
        //                     href.Contains("theenemy.com.br") && !href.Equals(portal.Url) && !href.Contains("#") &&
        //                     href.Split('/').Length > 4))
        //                {
        //                    newsLinks.Add(link);
        //                }
        //            }

        //            // Processar os links encontrados
        //            if (newsLinks.Any())
        //            {
        //                Console.WriteLine($"Encontrados {newsLinks.Count} possíveis links de artigos");

        //                // Remover duplicatas baseado em URLs
        //                var processedUrls = new HashSet<string>();

        //                foreach (var node in newsLinks.Take(15)) // Pegamos mais para compensar possíveis duplicatas
        //                {
        //                    var title = CleanHtml(node.InnerText);
        //                    var url = node.GetAttributeValue("href", "");

        //                    // Garantir que a URL seja absoluta
        //                    if (!string.IsNullOrEmpty(url) && !url.StartsWith("http"))
        //                        url = new Uri(new Uri(portal.Url), url).ToString();

        //                    // Evitar duplicatas
        //                    if (string.IsNullOrEmpty(url) || processedUrls.Contains(url))
        //                        continue;

        //                    processedUrls.Add(url);

        //                    // Se o texto do link estiver vazio, tentar extrair título de elementos filhos
        //                    if (string.IsNullOrWhiteSpace(title))
        //                    {
        //                        var textNodes = node.SelectNodes(".//text()");
        //                        if (textNodes != null && textNodes.Any())
        //                        {
        //                            title = CleanHtml(string.Join(" ", textNodes.Select(n => n.InnerText)));
        //                        }
        //                    }

        //                    // Se ainda não temos título, extrair da URL
        //                    if (string.IsNullOrWhiteSpace(title))
        //                    {
        //                        // Extrair última parte da URL e converter para título
        //                        var urlParts = url.Split('/').Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        //                        if (urlParts.Any())
        //                        {
        //                            var lastPart = WebUtility.UrlDecode(urlParts.Last().Replace("-", " "));
        //                            title = CleanHtml(lastPart);
        //                            // Primeira letra maiúscula
        //                            if (!string.IsNullOrEmpty(title) && title.Length > 1)
        //                            {
        //                                title = char.ToUpper(title[0]) + title.Substring(1);
        //                            }
        //                        }
        //                    }

        //                    if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(url) && processedUrls.Count <= 10)
        //                    {
        //                        NewsFeed.Add(new NewsItem
        //                        {
        //                            Title = title,
        //                            Url = url,
        //                            Source = portal.Name
        //                        });
        //                        articlesFound = true;
        //                    }
        //                }
        //            }
        //        }
        //    }

        //    // Se ainda não encontramos artigos, tente extrair da meta tags
        //    if (!articlesFound)
        //    {
        //        Console.WriteLine("Tentando extrair artigos das meta tags...");

        //        var metaTags = htmlDoc.DocumentNode.SelectNodes("//meta[@property='og:url' or @property='og:title']");
        //        if (metaTags != null && metaTags.Count >= 2)
        //        {
        //            string url = null;
        //            string title = null;

        //            foreach (var meta in metaTags)
        //            {
        //                var property = meta.GetAttributeValue("property", "");
        //                var content = meta.GetAttributeValue("content", "");

        //                if (property == "og:url")
        //                    url = content;
        //                else if (property == "og:title")
        //                    title = content;
        //            }

        //            if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(title))
        //            {
        //                // Adicionar a própria página como uma notícia (geralmente é a principal)
        //                NewsFeed.Add(new NewsItem
        //                {
        //                    Title = title,
        //                    Url = url,
        //                    Source = portal.Name
        //                });
        //                articlesFound = true;
        //            }
        //        }
        //    }

        //    if (!articlesFound)
        //    {
        //        Console.WriteLine($"Nenhum artigo encontrado para {portal.Name} usando todas as estratégias");
        //        // Para debug, salvar o HTML em um arquivo
        //        Console.WriteLine("HTML recebido: " + htmlDoc.DocumentNode.OuterHtml.Length + " caracteres");
        //    }
        //}

        private void ParseOmeleteGames(HtmlDocument htmlDoc, NewsPortal portal)
        {
            Console.WriteLine("Analisando Omelete Games...");

            try
            {
                // O Omelete Games usa uma estrutura mais simples com links diretos
                // Buscar por todos os links que apontam para artigos de games
                var articleLinks = htmlDoc.DocumentNode.SelectNodes("//a[@href]");

                if (articleLinks == null)
                {
                    Console.WriteLine("Nenhum link encontrado na página");
                    return;
                }

                Console.WriteLine($"Encontrados {articleLinks.Count} links para análise");

                var processedUrls = new HashSet<string>();
                var articlesFound = 0;

                foreach (var link in articleLinks)
                {
                    var href = link.GetAttributeValue("href", "");

                    // Filtrar apenas links que são artigos de games do Omelete
                    if (string.IsNullOrEmpty(href) ||
                        !href.StartsWith("/games/") ||
                        href.Contains("/ofertas/") // Excluir ofertas se necessário
                        )
                    {
                        continue;
                    }

                    // Construir URL completa
                    var fullUrl = $"https://www.omelete.com.br{href}";

                    // Evitar duplicatas
                    if (processedUrls.Contains(fullUrl))
                        continue;

                    processedUrls.Add(fullUrl);

                    // Extrair título do link
                    var title = ExtractTitleFromOmeleteLink(link);

                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        NewsFeed.Add(new NewsItem
                        {
                            Title = CleanHtml(title),
                            Url = fullUrl,
                            Source = portal.Name
                        });

                        articlesFound++;

                        // Limitar a 10-15 artigos para não sobrecarregar
                        if (articlesFound >= 15)
                            break;
                    }
                }

                Console.WriteLine($"Encontrados {articlesFound} artigos do Omelete Games");

                // Se não encontrou artigos suficientes com a abordagem principal, 
                // tentar buscar por padrões alternativos
                if (articlesFound < 5)
                {
                    TryAlternativeOmeleteParser(htmlDoc, portal, processedUrls);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao analisar Omelete Games: {ex.Message}");

                // Fallback: tentar extrair pelo menos um artigo das meta tags
                TryExtractFromMetaTags(htmlDoc, portal);
            }
        }

        private string ExtractTitleFromOmeleteLink(HtmlNode linkNode)
        {
            // Primeiro, tentar pegar o texto direto do link
            var directText = CleanHtml(linkNode.InnerText);
            if (!string.IsNullOrWhiteSpace(directText) && directText.Length > 10)
            {
                return directText;
            }

            // Se não tem texto direto, buscar em elementos filhos
            var textNodes = linkNode.SelectNodes(".//text()[normalize-space(.) != '']");
            if (textNodes != null && textNodes.Any())
            {
                var combinedText = string.Join(" ", textNodes.Select(n => CleanHtml(n.InnerText)))
                                        .Trim();
                if (!string.IsNullOrWhiteSpace(combinedText) && combinedText.Length > 10)
                {
                    return combinedText;
                }
            }

            // Se ainda não encontrou, tentar extrair de atributos como title ou aria-label
            var titleAttr = linkNode.GetAttributeValue("title", "");
            if (!string.IsNullOrWhiteSpace(titleAttr))
            {
                return CleanHtml(titleAttr);
            }

            var ariaLabel = linkNode.GetAttributeValue("aria-label", "");
            if (!string.IsNullOrWhiteSpace(ariaLabel))
            {
                return CleanHtml(ariaLabel);
            }

            // Como último recurso, extrair título da URL
            var href = linkNode.GetAttributeValue("href", "");
            if (!string.IsNullOrEmpty(href))
            {
                return ExtractTitleFromUrl(href);
            }

            return string.Empty;
        }

        private void TryAlternativeOmeleteParser(HtmlDocument htmlDoc, NewsPortal portal, HashSet<string> processedUrls)
        {
            Console.WriteLine("Tentando parser alternativo para Omelete Games...");

            try
            {
                // Buscar por estruturas de artigos mais específicas
                var articleContainers = htmlDoc.DocumentNode.SelectNodes(
                    "//article//a[@href] | //div[contains(@class, 'article')]//a[@href] | " +
                    "//div[contains(@class, 'news')]//a[@href] | //div[contains(@class, 'post')]//a[@href]");

                if (articleContainers != null)
                {
                    Console.WriteLine($"Encontrados {articleContainers.Count} containers de artigos alternativos");

                    var articlesFound = 0;
                    foreach (var container in articleContainers)
                    {
                        var href = container.GetAttributeValue("href", "");

                        if (string.IsNullOrEmpty(href) || !href.StartsWith("/games/"))
                            continue;

                        var fullUrl = $"https://www.omelete.com.br{href}";

                        if (processedUrls.Contains(fullUrl))
                            continue;

                        var title = ExtractTitleFromOmeleteLink(container);

                        if (!string.IsNullOrWhiteSpace(title))
                        {
                            NewsFeed.Add(new NewsItem
                            {
                                Title = CleanHtml(title),
                                Url = fullUrl,
                                Source = portal.Name
                            });

                            processedUrls.Add(fullUrl);
                            articlesFound++;

                            if (articlesFound >= 10)
                                break;
                        }
                    }

                    Console.WriteLine($"Parser alternativo encontrou {articlesFound} artigos adicionais");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro no parser alternativo: {ex.Message}");
            }
        }

        private void TryExtractFromMetaTags(HtmlDocument htmlDoc, NewsPortal portal)
        {
            Console.WriteLine("Tentando extrair artigo das meta tags...");

            try
            {
                var urlMeta = htmlDoc.DocumentNode.SelectSingleNode("//meta[@property='og:url']");
                var titleMeta = htmlDoc.DocumentNode.SelectSingleNode("//meta[@property='og:title']");

                if (urlMeta != null && titleMeta != null)
                {
                    var url = urlMeta.GetAttributeValue("content", "");
                    var title = titleMeta.GetAttributeValue("content", "");

                    if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(title) &&
                        url.Contains("omelete.com.br/games"))
                    {
                        NewsFeed.Add(new NewsItem
                        {
                            Title = CleanHtml(title),
                            Url = url,
                            Source = portal.Name
                        });

                        Console.WriteLine("Artigo extraído das meta tags com sucesso");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao extrair das meta tags: {ex.Message}");
            }
        }

        private string ExtractTitleFromUrl(string url)
        {
            try
            {
                // Extrair a parte final da URL e converter em título legível
                var urlParts = url.Split('/').Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
                if (urlParts.Any())
                {
                    var lastPart = WebUtility.UrlDecode(urlParts.Last())
                                           .Replace("-", " ")
                                           .Replace("_", " ");

                    // Capitalizar primeira letra de cada palavra
                    var words = lastPart.Split(' ');
                    for (int i = 0; i < words.Length; i++)
                    {
                        if (words[i].Length > 0)
                        {
                            words[i] = char.ToUpper(words[i][0]) +
                                      (words[i].Length > 1 ? words[i].Substring(1).ToLower() : "");
                        }
                    }

                    return string.Join(" ", words);
                }
            }
            catch
            {
                // Ignore errors and return empty
            }

            return string.Empty;
        }

        private void ParseTechTudo(HtmlDocument htmlDoc, NewsPortal portal)
        {
            try
            {
                Console.WriteLine("Tentando parsear TechTudo...");

                // Selecionar os nós das notícias
                var newsNodes = htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 'feed-post-body')]");

                if (newsNodes != null && newsNodes.Any())
                {
                    Console.WriteLine($"Encontrados {newsNodes.Count} nós de notícia no TechTudo");

                    var processedUrls = new HashSet<string>();
                    foreach (var node in newsNodes.Take(10)) // Limitar a 10 notícias
                    {
                        // Extrair título
                        var titleNode = node.SelectSingleNode(".//a[contains(@class, 'feed-post-link')]");
                        var title = titleNode?.InnerText.Trim();

                        // Extrair URL
                        var url = titleNode?.GetAttributeValue("href", "");

                        // Extrair imagem
                        var imgNode = node.SelectSingleNode(".//img");
                        var imageUrl = imgNode?.GetAttributeValue("src", "");

                        // Garantir que a URL seja absoluta
                        if (!string.IsNullOrEmpty(url) && !url.StartsWith("http"))
                        {
                            url = new Uri(new Uri(portal.Url), url).ToString();
                        }

                        if (!string.IsNullOrEmpty(imageUrl) && !imageUrl.StartsWith("http"))
                        {
                            imageUrl = new Uri(new Uri(portal.Url), imageUrl).ToString();
                        }

                        // Adicionar notícia ao feed
                        if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(url) && !processedUrls.Contains(url))
                        {
                            NewsFeed.Add(new NewsItem
                            {
                                Title = title,
                                Url = url,
                                Source = portal.Name,
                                ImageUrl = imageUrl
                            });
                            processedUrls.Add(url);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Nenhuma notícia encontrada no TechTudo");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao parsear TechTudo: {ex.Message}");
            }
        }

        private string CleanHtml(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Remover HTML tags
            var withoutTags = Regex.Replace(input, "<.*?>", string.Empty);

            // Decodificar entidades HTML
            var decoded = System.Net.WebUtility.HtmlDecode(withoutTags);

            // Remover espaços extras
            var trimmed = Regex.Replace(decoded, @"\s+", " ").Trim();

            return trimmed;
        }

        private async void OnShareClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is string url)
            {
                await Share.Default.RequestAsync(new ShareTextRequest
                {
                    Uri = url,
                    Title = "Compartilhar notícia"
                });
            }
        }

        private async void OnSummaryClicked(object sender, EventArgs e)
        {
            try
            {
                // Coletar títulos e descrições das notícias
                var textos = NewsFeed
                    .Where(n => !string.IsNullOrEmpty(n.Title))
                    .Select(n => $"{n.Title}: {n.Description ?? ""}")
                    .ToList();

                // Verificar se há notícias para resumir
                if (!textos.Any())
                {
                    await DisplayAlert("Aviso", "Nenhuma notícia encontrada para resumir.", "OK");
                    return;
                }

                // Montar o texto para resumir
                var textoParaResumo = string.Join("\n", textos);

                // Navegar imediatamente para a página de resumo (que iniciará o loading)
                var summaryPage = new SummaryPage(textoParaResumo);
                await Navigation.PushAsync(summaryPage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao navegar para resumo: {ex}");
                await DisplayAlert("Erro", "Erro ao abrir página de resumo. Tente novamente.", "OK");
            }
        }

        //private async Task<string> GerarResumoComIA(string texto)
        //{
        //    var apiKey = Secrets.apiKeyGeminiIA;
        //    var endpoint = $"{Secrets.geminiBaseUrl}?key={Secrets.apiKeyGeminiIA}";

        //    var prompt = $"Resuma as principais notícias abaixo em até 2 minutos de leitura, focando nos fatos mais relevantes, verifique se as notícias se repetem em diferentes portais e dê destaque para essas notícias:\n{texto}";

        //    var body = new
        //    {
        //        contents = new[]
        //        {
        //    new
        //    {
        //        parts = new[]
        //        {
        //            new { text = prompt }
        //        }
        //    }
        //}
        //    };

        //    var jsonBody = JsonSerializer.Serialize(body, new JsonSerializerOptions
        //    {
        //        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        //    });

        //    using var client = new HttpClient();
        //    var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        //    var response = await client.PostAsync(endpoint, content);
        //    var json = await response.Content.ReadAsStringAsync();

        //    if (!response.IsSuccessStatusCode)
        //    {
        //        throw new Exception($"Erro Gemini: {response.StatusCode} - {json}");
        //    }

        //    var doc = JsonDocument.Parse(json);
        //    var resumo = doc.RootElement
        //        .GetProperty("candidates")[0]
        //        .GetProperty("content")
        //        .GetProperty("parts")[0]
        //        .GetProperty("text")
        //        .GetString();

        //    return resumo ?? "Não foi possível gerar o resumo.";
        //}

    }
}
