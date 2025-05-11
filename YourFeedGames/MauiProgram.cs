using Microsoft.Extensions.Logging;
using YourFeedGames.Converters;

namespace YourFeedGames
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Registrar os converters
            builder.ConfigureMauiHandlers(handlers =>
            {
                // Aqui você pode configurar handlers específicos se necessário
            });

            // Registrar páginas
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<SettingsPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}