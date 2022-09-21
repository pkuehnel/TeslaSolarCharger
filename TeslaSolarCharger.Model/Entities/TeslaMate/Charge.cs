namespace TeslaSolarCharger.Model.Entities.TeslaMate
{
    public class Charge
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public bool? BatteryHeaterOn { get; set; }
        public short? BatteryLevel { get; set; }
        public decimal ChargeEnergyAdded { get; set; }
        public short? ChargerActualCurrent { get; set; }
        public short? ChargerPhases { get; set; }
        public short? ChargerPilotCurrent { get; set; }
        public short ChargerPower { get; set; }
        public short? ChargerVoltage { get; set; }
        public bool? FastChargerPresent { get; set; }
        public string? ConnChargeCable { get; set; }
        public string? FastChargerBrand { get; set; }
        public string? FastChargerType { get; set; }
        public decimal IdealBatteryRangeKm { get; set; }
        public bool? NotEnoughPowerToHeat { get; set; }
        public decimal? OutsideTemp { get; set; }
        public int ChargingProcessId { get; set; }
        public bool? BatteryHeater { get; set; }
        public bool? BatteryHeaterNoPower { get; set; }
        public decimal? RatedBatteryRangeKm { get; set; }
        public short? UsableBatteryLevel { get; set; }

        public virtual ChargingProcess ChargingProcess { get; set; } = null!;
    }
}
