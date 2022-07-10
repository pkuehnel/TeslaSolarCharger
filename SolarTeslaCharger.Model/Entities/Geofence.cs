namespace SolarTeslaCharger.Model.Entities
{
    public class Geofence
    {
        public Geofence()
        {
            ChargingProcesses = new HashSet<ChargingProcess>();
            DriveEndGeofences = new HashSet<Drive>();
            DriveStartGeofences = new HashSet<Drive>();
        }

        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public short Radius { get; set; }
        public DateTime InsertedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public decimal? CostPerUnit { get; set; }
        public decimal? SessionFee { get; set; }

        public ICollection<ChargingProcess> ChargingProcesses { get; set; }
        public ICollection<Drive> DriveEndGeofences { get; set; }
        public ICollection<Drive> DriveStartGeofences { get; set; }
    }
}
