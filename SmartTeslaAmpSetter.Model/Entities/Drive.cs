namespace SmartTeslaAmpSetter.Model.Entities
{
    public class Drive
    {
        public Drive()
        {
            Positions = new HashSet<Position>();
        }

        public int Id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal? OutsideTempAvg { get; set; }
        public short? SpeedMax { get; set; }
        public short? PowerMax { get; set; }
        public short? PowerMin { get; set; }
        public decimal? StartIdealRangeKm { get; set; }
        public decimal? EndIdealRangeKm { get; set; }
        public double? StartKm { get; set; }
        public double? EndKm { get; set; }
        public double? Distance { get; set; }
        public short? DurationMin { get; set; }
        public short CarId { get; set; }
        public decimal? InsideTempAvg { get; set; }
        public int? StartAddressId { get; set; }
        public int? EndAddressId { get; set; }
        public decimal? StartRatedRangeKm { get; set; }
        public decimal? EndRatedRangeKm { get; set; }
        public int? StartPositionId { get; set; }
        public int? EndPositionId { get; set; }
        public int? StartGeofenceId { get; set; }
        public int? EndGeofenceId { get; set; }

        public Car Car { get; set; } = null!;
        public Address? EndAddress { get; set; }
        public Geofence? EndGeofence { get; set; }
        public Position? EndPosition { get; set; }
        public Address? StartAddress { get; set; }
        public Geofence? StartGeofence { get; set; }
        public Position? StartPosition { get; set; }
        public ICollection<Position> Positions { get; set; }
    }
}
