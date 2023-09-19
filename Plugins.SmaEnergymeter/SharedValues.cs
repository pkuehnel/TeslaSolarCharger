using Plugins.SmaEnergymeter.Dtos;

namespace Plugins.SmaEnergymeter;

public class SharedValues
{
    public Dictionary<uint, DtoEnergyMeterValue> EnergyMeterValues = new();
}
