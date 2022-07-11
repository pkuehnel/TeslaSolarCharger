namespace TeslaSolarCharger.Model.Entities
{
    public class Token
    {
        public int Id { get; set; }
        public string Access { get; set; } = null!;
        public string Refresh { get; set; } = null!;
        public DateTime InsertedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
