namespace SolarTeslaCharger.Model.Entities
{
    public class Position
    {
        public Position()
        {
            ChargingProcesses = new HashSet<ChargingProcess>();
            DriveEndPositions = new HashSet<Drive>();
            DriveStartPositions = new HashSet<Drive>();
        }

        public int Id { get; set; }
        public DateTime Date { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public short? Speed { get; set; }
        public short? Power { get; set; }
        public double? Odometer { get; set; }
        public decimal? IdealBatteryRangeKm { get; set; }
        public short? BatteryLevel { get; set; }
        public decimal? OutsideTemp { get; set; }
        public short? Elevation { get; set; }
        public int? FanStatus { get; set; }
        public decimal? DriverTempSetting { get; set; }
        public decimal? PassengerTempSetting { get; set; }
        public bool? IsClimateOn { get; set; }
        public bool? IsRearDefrosterOn { get; set; }
        public bool? IsFrontDefrosterOn { get; set; }
        public short CarId { get; set; }
        public int? DriveId { get; set; }
        public decimal? InsideTemp { get; set; }
        public bool? BatteryHeater { get; set; }
        public bool? BatteryHeaterOn { get; set; }
        public bool? BatteryHeaterNoPower { get; set; }
        public decimal? EstBatteryRangeKm { get; set; }
        public decimal? RatedBatteryRangeKm { get; set; }
        public short? UsableBatteryLevel { get; set; }

        public Car Car { get; set; } = null!;
        public Drive? Drive { get; set; }
        public ICollection<ChargingProcess> ChargingProcesses { get; set; }
        public ICollection<Drive> DriveEndPositions { get; set; }
        public ICollection<Drive> DriveStartPositions { get; set; }
    }
}
