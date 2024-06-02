using Newtonsoft.Json;

namespace TeslaSolarCharger.Shared.Dtos.ChargingCost;

public class DtoChargeSummary
{
    public decimal ChargedGridEnergy { get; set; }
    public decimal ChargedHomeBatteryEnergy { get; set; }
    public decimal ChargedSolarEnergy { get; set; }
    public decimal ChargeCost { get; set; }
    [JsonIgnore]
    public decimal SolarPortionPercent
    {
        get
        {
            var chargeSum = ChargedSolarEnergy + ChargedGridEnergy + ChargedHomeBatteryEnergy;
            if (chargeSum > 0)
            {
                return (ChargedSolarEnergy / chargeSum) * 100;
            }

            return 100;
        }
    }

    [JsonIgnore]
    public decimal HomeBatteryPortionPercent
    {
        get
        {
            var chargeSum = ChargedSolarEnergy + ChargedGridEnergy + ChargedHomeBatteryEnergy;
            if (chargeSum > 0)
            {
                return (ChargedHomeBatteryEnergy / chargeSum) * 100;
            }

            return 100;
        }
    }

    [JsonIgnore]
    public decimal AverageEnergyPrice
    {
        get
        {
            var chargeSum = ChargedSolarEnergy + ChargedGridEnergy + ChargedHomeBatteryEnergy;
            if (chargeSum > 0)
            {
                return (ChargeCost / chargeSum);
            }

            return 0;
        }
    }
}
