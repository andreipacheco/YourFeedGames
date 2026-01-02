using Supabase;
using System.Threading.Tasks;
using System.Collections.Generic;
using YourFeedGames.Models;
using Supabase.Postgrest.Models;

namespace YourFeedGames
{
    internal class supabase
    {
    }

    public class SupabaseService
    {

        private readonly Client _client;
        private Task _initTask;

        public SupabaseService()
        {
            _client = new Client(Pagamentos.Secrets.SupabaseUrl, Pagamentos.Secrets.SupabaseAnonKey);
            _initTask = _client.InitializeAsync();
        }


        public async Task<List<Events>> GetEventosAsync()
        {
            try
            {
                // Aguarda a inicialização do client
                if (_initTask != null)
                    await _initTask;

                var result = await _client.From<Events>().Get();
                if (result.Models == null)
                {
                    System.Diagnostics.Debug.WriteLine("Nenhum evento retornado do Supabase.");
                    return new List<Events>();
                }
                return result.Models;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao buscar eventos do Supabase: {ex.Message}");
                return new List<Events>();
            }
        }

        public async Task InsertEventoAsync(Events evento)
        {
            await _client.From<Events>().Insert(evento);
        }

        public async Task<List<HotNews>> GetHotNewsAtivasAsync()
        {
            try
            {
                if (_initTask != null)
                    await _initTask;

                var result = await _client.From<HotNews>()
                    .Filter("status", Supabase.Postgrest.Constants.Operator.Equals, "true")
                    .Get();

                if (result.Models == null || result.Models.Count == 0)
                {
                    return new List<HotNews>();
                }
                return result.Models;
            }
            catch (Exception ex)
            {
                return new List<HotNews>();
            }
        }
    }
}
