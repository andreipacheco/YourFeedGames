using HtmlAgilityPack;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Net;
using System.Text;

namespace YourFeedGames
{
    public partial class MainPage : ContentPage
    {
        public ObservableCollection<NewsItem> NewsFeed { get; set; }
        private HttpClient _httpClient;
        private bool _debugMode = false;

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

            LoadNewsFeed();
        }

        // Método separado para inicializar o HttpClient com configurações anti-bloqueio
        private void InitializeHttpClient()
        {
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

        // Método para tentar extrair artigos usando abordagens diferentes dependendo do site
        private async Task<string> GetHtmlContentWithFallbacks(string url)
        {
            try
            {
                // Primeira tentativa - abordagem padrão
                HttpResponseMessage response = await _httpClient.GetAsync(url);

                // Se bloqueado, mostrar código de status
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Resposta HTTP: {response.StatusCode} para {url}");

                    // Se for código 403 (Forbidden), tente uma abordagem diferente
                    if (response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        // Tentar novamente com cabeçalhos diferentes
                        InitializeHttpClient(); // Reinicia com novos cabeçalhos
                        response = await _httpClient.GetAsync(url);

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new HttpRequestException($"Bloqueado com status: {response.StatusCode}");
                        }
                    }
                    else
                    {
                        throw new HttpRequestException($"Erro HTTP: {response.StatusCode}");
                    }
                }

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao obter HTML: {ex.Message}");
                throw;
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

        private async void LoadNewsFeed()
        {
            try
            {
                // Limpar feed atual antes de carregar novos itens
                NewsFeed.Clear();

                // Exibir indicador de carregamento
                loadingIndicator.IsVisible = true;
                feedScrollView.IsVisible = false;

                var enabledPortals = new List<NewsPortal>
                {
                    new NewsPortal { Name = "Flow Games", Url = "https://flowgames.gg", IsEnabled = Preferences.Get("Flow Games", true) },
                    new NewsPortal { Name = "Gameplayscassi", Url = "https://gameplayscassi.com.br", IsEnabled = Preferences.Get("Gameplayscassi", true) },
                    new NewsPortal { Name = "The Enemy", Url = "https://www.theenemy.com.br", IsEnabled = Preferences.Get("The Enemy", true) },
                    new NewsPortal { Name = "IGN Brasil", Url = "https://br.ign.com", IsEnabled = Preferences.Get("IGN Brasil", true) }
                };

                foreach (var portal in enabledPortals.Where(p => p.IsEnabled))
                {
                    try
                    {
                        await FetchNewsFromPortal(portal);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao carregar notícias de {portal.Name}: {ex.Message}");
                        // Adicionar um item de notícia indicando erro
                        NewsFeed.Add(new NewsItem
                        {
                            Title = $"Erro ao carregar notícias de {portal.Name}",
                            Description = "Não foi possível obter o feed deste portal no momento.",
                            Source = portal.Name,
                            Url = portal.Url
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro geral: {ex.Message}");
                await DisplayAlert("Erro", "Houve um problema ao carregar o feed de notícias.", "OK");
            }
            finally
            {
                // Ocultar indicador de carregamento
                loadingIndicator.IsVisible = false;
                feedScrollView.IsVisible = true;
            }
        }

        private async void OnSettingsClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("///SettingsPage");
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

        // Add this method to your MainPage class to help with debugging
        private async void OnRefreshClicked(object sender, EventArgs e)
        {
            await LoadNewsFeedWithDebug();
        }

        // Enhanced version of LoadNewsFeed with better debugging
        private async Task LoadNewsFeedWithDebug()
        {
            try
            {
                // Limpar feed atual antes de carregar novos itens
                NewsFeed.Clear();

                // Exibir indicador de carregamento
                loadingIndicator.IsVisible = true;
                feedScrollView.IsVisible = false;

                var enabledPortals = new List<NewsPortal>
        {
            new NewsPortal { Name = "Flow Games", Url = "https://flowgames.gg", IsEnabled = Preferences.Get("Flow Games", true) },
            new NewsPortal { Name = "Gameplayscassi", Url = "https://gameplayscassi.com.br", IsEnabled = Preferences.Get("Gameplayscassi", true) },
            new NewsPortal { Name = "The Enemy", Url = "https://www.theenemy.com.br", IsEnabled = Preferences.Get("The Enemy", true) },
            new NewsPortal { Name = "IGN Brasil", Url = "https://br.ign.com", IsEnabled = Preferences.Get("IGN Brasil", true) }
        };

                foreach (var portal in enabledPortals.Where(p => p.IsEnabled))
                {
                    try
                    {
                        Console.WriteLine($"Tentando carregar notícias de {portal.Name}...");
                        var startTime = DateTime.Now;

                        // Adicione um contador para acompanhar quantas notícias foram adicionadas
                        int itemsCountBefore = NewsFeed.Count;

                        await FetchNewsFromPortal(portal);

                        int itemsAdded = NewsFeed.Count - itemsCountBefore;
                        var elapsed = DateTime.Now - startTime;

                        Console.WriteLine($"Portal {portal.Name}: {itemsAdded} notícias carregadas em {elapsed.TotalSeconds:F2} segundos");

                        if (itemsAdded == 0)
                        {
                            // Adicionar um item de notícia indicando que não encontrou nada
                            NewsFeed.Add(new NewsItem
                            {
                                Title = $"Nenhuma notícia encontrada em {portal.Name}",
                                Description = "Verifique a conexão com a internet ou o site pode ter alterado sua estrutura HTML.",
                                Source = portal.Name,
                                Url = portal.Url
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao carregar notícias de {portal.Name}: {ex.Message}");
                        Console.WriteLine($"StackTrace: {ex.StackTrace}");

                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                        }

                        // Adicionar um item de notícia indicando erro
                        NewsFeed.Add(new NewsItem
                        {
                            Title = $"Erro ao carregar notícias de {portal.Name}",
                            Description = $"Erro: {ex.Message}",
                            Source = portal.Name,
                            Url = portal.Url
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro geral: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                await DisplayAlert("Erro", "Houve um problema ao carregar o feed de notícias.", "OK");
            }
            finally
            {
                // Ocultar indicador de carregamento
                loadingIndicator.IsVisible = false;
                feedScrollView.IsVisible = true;
            }
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
                    case "Flow Games":
                        ParseFlowGames(htmlDoc, portal);
                        break;
                    case "Gameplayscassi":
                        ParseGameplayscassi(htmlDoc, portal);
                        break;
                    case "The Enemy":
                        ParseTheEnemy(htmlDoc, portal);
                        break;
                    case "IGN Brasil":
                        ParseIGNBrasil(htmlDoc, portal);
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

            // Baseado na análise do HTML, o problema pode ser que o conteúdo está sendo carregado via JavaScript
            // Vamos tentar extrair quaisquer links com características de notícias
            var allLinks = htmlDoc.DocumentNode.SelectNodes("//a[@href]");
            var newsLinks = new List<HtmlNode>();

            if (allLinks != null)
            {
                // Regex para URLs que parecem ser de posts/artigos
                var articlePattern = new Regex(@"/(20\d{2}/\d{2}/|post/|noticia/|review/|artigo/)", RegexOptions.IgnoreCase);

                foreach (var link in allLinks)
                {
                    var href = link.GetAttributeValue("href", "");

                    // Verificar se parece ser um link de artigo
                    if (!string.IsNullOrEmpty(href) &&
                        (articlePattern.IsMatch(href) ||
                         href.Contains("gameplayscassi.com.br") && !href.Equals(portal.Url) && !href.Contains("category") && !href.Contains("tag")))
                    {
                        newsLinks.Add(link);
                        Console.WriteLine($"Link potencial encontrado: {href}");
                    }
                }
            }

            if (newsLinks.Any())
            {
                Console.WriteLine($"Encontrados {newsLinks.Count} possíveis links de notícias");

                // Remover duplicatas baseado em URLs
                var processedUrls = new HashSet<string>();

                foreach (var node in newsLinks.Take(15)) // Pegamos mais para compensar possíveis duplicatas
                {
                    var url = node.GetAttributeValue("href", "");

                    // Garantir que a URL seja absoluta
                    if (!string.IsNullOrEmpty(url) && !url.StartsWith("http"))
                        url = new Uri(new Uri(portal.Url), url).ToString();

                    // Evitar duplicatas
                    if (string.IsNullOrEmpty(url) || processedUrls.Contains(url))
                        continue;

                    processedUrls.Add(url);

                    // Tentar extrair título do texto do link, ou de elementos internos como img alt ou spans
                    string title = "";

                    // Verificar se há texto direto no link
                    var directText = CleanHtml(node.InnerText);
                    if (!string.IsNullOrWhiteSpace(directText) && directText.Length > 5)
                    {
                        title = directText;
                    }
                    // Verificar se há uma imagem com alt
                    else
                    {
                        var imgNode = node.SelectSingleNode(".//img");
                        if (imgNode != null)
                        {
                            var alt = imgNode.GetAttributeValue("alt", "");
                            if (!string.IsNullOrWhiteSpace(alt))
                            {
                                title = alt;
                            }
                        }
                    }

                    // Se ainda não temos título, extrair da URL
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        // Extrair última parte da URL e converter para título
                        var urlParts = url.Split('/').Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
                        if (urlParts.Any())
                        {
                            var lastPart = WebUtility.UrlDecode(urlParts.Last().Replace("-", " "));
                            title = CleanHtml(lastPart);
                            // Primeira letra maiúscula
                            if (!string.IsNullOrEmpty(title) && title.Length > 1)
                            {
                                title = char.ToUpper(title[0]) + title.Substring(1);
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(url) && processedUrls.Count <= 10)
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
                // Alternativa: tentar uma abordagem mais genérica usando divs que parecem ser cards de notícias
                var possibleCards = htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 'card') or contains(@class, 'post') or contains(@class, 'noticia')]");

                if (possibleCards != null && possibleCards.Any())
                {
                    Console.WriteLine($"Tentando extrair das divs de cards: {possibleCards.Count} encontrados");

                    foreach (var card in possibleCards.Take(10))
                    {
                        var linkNode = card.SelectSingleNode(".//a");
                        var titleNode = card.SelectSingleNode(".//h2") ?? card.SelectSingleNode(".//h3");

                        if (linkNode != null && titleNode != null)
                        {
                            var title = CleanHtml(titleNode.InnerText);
                            var url = linkNode.GetAttributeValue("href", "");

                            // Garantir que a URL seja absoluta
                            if (!string.IsNullOrEmpty(url) && !url.StartsWith("http"))
                                url = new Uri(new Uri(portal.Url), url).ToString();

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
                    Console.WriteLine($"Nenhum nó de notícia encontrado para {portal.Name} usando todas as abordagens");
                    // Para debug, salvar o HTML em um arquivo
                    Console.WriteLine("HTML recebido: " + htmlDoc.DocumentNode.OuterHtml.Length + " caracteres");
                }
            }
        }

        private void ParseTheEnemy(HtmlDocument htmlDoc, NewsPortal portal)
        {
            Console.WriteLine("Tentando analisar The Enemy com nova abordagem...");

            // O site TheEnemy provavelmente usa JavaScript para renderizar o conteúdo
            // Vamos tentar uma abordagem diferente, buscando por links que parecem ser de artigos

            // Opção 1: Buscar por scripts que contenham dados de artigos embutidos (JSON)
            var scriptNodes = htmlDoc.DocumentNode.SelectNodes("//script");
            var articlesFound = false;

            if (scriptNodes != null)
            {
                Console.WriteLine($"Encontrados {scriptNodes.Count} scripts para análise");

                foreach (var script in scriptNodes)
                {
                    var scriptContent = script.InnerText;

                    // Procurar por padrões que pareçam conter URLs de artigos
                    if (scriptContent.Contains("\"url\":") &&
                        (scriptContent.Contains("\"title\":") || scriptContent.Contains("\"headline\":")))
                    {
                        try
                        {
                            Console.WriteLine("Encontrado possível script com dados de artigos");

                            // Extrair URLs e títulos usando regex
                            var urlPattern = new Regex("\"url\":\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
                            var titlePattern = new Regex("\"(title|headline)\":\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);

                            var urlMatches = urlPattern.Matches(scriptContent);
                            var titleMatches = titlePattern.Matches(scriptContent);

                            // Se encontramos algumas URLs e títulos, vamos tentar usá-los
                            if (urlMatches.Count > 0 && titleMatches.Count > 0 &&
                                urlMatches.Count == titleMatches.Count)
                            {
                                Console.WriteLine($"Extraídos {urlMatches.Count} pares de URL/título do script");

                                for (int i = 0; i < Math.Min(10, urlMatches.Count); i++)
                                {
                                    var url = urlMatches[i].Groups[1].Value;
                                    var title = WebUtility.HtmlDecode(titleMatches[i].Groups[2].Value);

                                    // Verificar se a URL é do próprio site e não é uma URL de imagem ou outro recurso
                                    if (url.Contains("theenemy.com.br") &&
                                        !url.EndsWith(".jpg") && !url.EndsWith(".png") &&
                                        !url.EndsWith(".css") && !url.EndsWith(".js"))
                                    {
                                        NewsFeed.Add(new NewsItem
                                        {
                                            Title = title,
                                            Url = url,
                                            Source = portal.Name
                                        });
                                        articlesFound = true;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Erro ao processar script: {ex.Message}");
                        }
                    }
                }
            }

            // Se não encontramos artigos nos scripts, tente a abordagem alternativa
            if (!articlesFound)
            {
                // Buscar por links com características de artigos
                var allLinks = htmlDoc.DocumentNode.SelectNodes("//a[@href]");

                if (allLinks != null)
                {
                    Console.WriteLine($"Analisando {allLinks.Count} links");

                    // Filtrar links que parecem ser de artigos
                    var articlePattern = new Regex(@"/(noticia|review|artigo|coluna|noticias)/", RegexOptions.IgnoreCase);
                    var newsLinks = new List<HtmlNode>();

                    foreach (var link in allLinks)
                    {
                        var href = link.GetAttributeValue("href", "");

                        // Verificar se parece ser um link de artigo
                        if (!string.IsNullOrEmpty(href) &&
                            (articlePattern.IsMatch(href) ||
                             href.Contains("theenemy.com.br") && !href.Equals(portal.Url) && !href.Contains("#") &&
                             href.Split('/').Length > 4))
                        {
                            newsLinks.Add(link);
                        }
                    }

                    // Processar os links encontrados
                    if (newsLinks.Any())
                    {
                        Console.WriteLine($"Encontrados {newsLinks.Count} possíveis links de artigos");

                        // Remover duplicatas baseado em URLs
                        var processedUrls = new HashSet<string>();

                        foreach (var node in newsLinks.Take(15)) // Pegamos mais para compensar possíveis duplicatas
                        {
                            var title = CleanHtml(node.InnerText);
                            var url = node.GetAttributeValue("href", "");

                            // Garantir que a URL seja absoluta
                            if (!string.IsNullOrEmpty(url) && !url.StartsWith("http"))
                                url = new Uri(new Uri(portal.Url), url).ToString();

                            // Evitar duplicatas
                            if (string.IsNullOrEmpty(url) || processedUrls.Contains(url))
                                continue;

                            processedUrls.Add(url);

                            // Se o texto do link estiver vazio, tentar extrair título de elementos filhos
                            if (string.IsNullOrWhiteSpace(title))
                            {
                                var textNodes = node.SelectNodes(".//text()");
                                if (textNodes != null && textNodes.Any())
                                {
                                    title = CleanHtml(string.Join(" ", textNodes.Select(n => n.InnerText)));
                                }
                            }

                            // Se ainda não temos título, extrair da URL
                            if (string.IsNullOrWhiteSpace(title))
                            {
                                // Extrair última parte da URL e converter para título
                                var urlParts = url.Split('/').Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
                                if (urlParts.Any())
                                {
                                    var lastPart = WebUtility.UrlDecode(urlParts.Last().Replace("-", " "));
                                    title = CleanHtml(lastPart);
                                    // Primeira letra maiúscula
                                    if (!string.IsNullOrEmpty(title) && title.Length > 1)
                                    {
                                        title = char.ToUpper(title[0]) + title.Substring(1);
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(url) && processedUrls.Count <= 10)
                            {
                                NewsFeed.Add(new NewsItem
                                {
                                    Title = title,
                                    Url = url,
                                    Source = portal.Name
                                });
                                articlesFound = true;
                            }
                        }
                    }
                }
            }

            // Se ainda não encontramos artigos, tente extrair da meta tags
            if (!articlesFound)
            {
                Console.WriteLine("Tentando extrair artigos das meta tags...");

                var metaTags = htmlDoc.DocumentNode.SelectNodes("//meta[@property='og:url' or @property='og:title']");
                if (metaTags != null && metaTags.Count >= 2)
                {
                    string url = null;
                    string title = null;

                    foreach (var meta in metaTags)
                    {
                        var property = meta.GetAttributeValue("property", "");
                        var content = meta.GetAttributeValue("content", "");

                        if (property == "og:url")
                            url = content;
                        else if (property == "og:title")
                            title = content;
                    }

                    if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(title))
                    {
                        // Adicionar a própria página como uma notícia (geralmente é a principal)
                        NewsFeed.Add(new NewsItem
                        {
                            Title = title,
                            Url = url,
                            Source = portal.Name
                        });
                        articlesFound = true;
                    }
                }
            }

            if (!articlesFound)
            {
                Console.WriteLine($"Nenhum artigo encontrado para {portal.Name} usando todas as estratégias");
                // Para debug, salvar o HTML em um arquivo
                Console.WriteLine("HTML recebido: " + htmlDoc.DocumentNode.OuterHtml.Length + " caracteres");
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
    }
}
