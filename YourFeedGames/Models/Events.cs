using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace YourFeedGames.Models
{
    [Table("events")]
    public class Events : BaseModel
    {
        public Guid id { get; set; }
        public string titulo { get; set; }
        public string descricao { get; set; }
        public DateTime data { get; set; }
        public string local { get; set; }
        public string imagem_url { get; set; }
        public string url { get; set; }
    }
}
