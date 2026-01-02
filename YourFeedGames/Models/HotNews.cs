using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace YourFeedGames.Models
{
    [Table("hotnews")]
    public class HotNews : BaseModel
    {
        public Guid id { get; set; }
        public DateTime created_at { get; set; }
        public string descricao { get; set; }
        public bool status { get; set; }
    }
}
