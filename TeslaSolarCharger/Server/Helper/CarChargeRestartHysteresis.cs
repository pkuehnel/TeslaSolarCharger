using TeslaSolarCharger.Server.Helper.Contracts;

namespace TeslaSolarCharger.Server.Helper;

public class CarChargeRestartHysteresis : ICarChargeRestartHysteresis
{
    public int Percent => 3;

    public bool PreventsChargeStart(int? soc, int? socLimit)
    {
        return soc > socLimit - Percent;
    }
}
