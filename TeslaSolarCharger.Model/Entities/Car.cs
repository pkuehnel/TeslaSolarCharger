namespace TeslaSolarCharger.Model.Entities
{
    public class Car
    {
        public Car()
        {
            ChargingProcesses = new HashSet<ChargingProcess>();
            Drives = new HashSet<Drive>();
            Positions = new HashSet<Position>();
            States = new HashSet<State>();
            Updates = new HashSet<Update>();
        }

        public short Id { get; set; }
        public long Eid { get; set; }
        public long Vid { get; set; }
        public string? Model { get; set; }
        public double? Efficiency { get; set; }
        public DateTime InsertedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? Vin { get; set; }
        public string? Name { get; set; }
        public string? TrimBadging { get; set; }
        public long SettingsId { get; set; }
        public string? ExteriorColor { get; set; }
        public string? SpoilerType { get; set; }
        public string? WheelType { get; set; }
        public short DisplayPriority { get; set; }

        public virtual CarSetting Settings { get; set; } = null!;
        public virtual ICollection<ChargingProcess> ChargingProcesses { get; set; }
        public virtual ICollection<Drive> Drives { get; set; }
        public virtual ICollection<Position> Positions { get; set; }
        public virtual ICollection<State> States { get; set; }
        public virtual ICollection<Update> Updates { get; set; }
    }
}
