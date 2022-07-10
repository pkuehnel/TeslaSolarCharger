namespace SolarTeslaCharger.Model.Entities
{
    public class State
    {
        public int Id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public short CarId { get; set; }

        public virtual Car Car { get; set; } = null!;
    }
}
