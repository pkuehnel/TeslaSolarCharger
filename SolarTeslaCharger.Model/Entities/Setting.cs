namespace SolarTeslaCharger.Model.Entities
{
    public class Setting
    {
        public long Id { get; set; }
        public DateTime InsertedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? BaseUrl { get; set; }
        public string? GrafanaUrl { get; set; }
        public string Language { get; set; } = null!;
    }
}
