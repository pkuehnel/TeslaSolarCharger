namespace TeslaSolarCharger.Model.Entities.TeslaMate
{
    public class Update
    {
        public int Id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Version { get; set; }
        public short CarId { get; set; }

        public virtual Car Car { get; set; } = null!;
    }
}
