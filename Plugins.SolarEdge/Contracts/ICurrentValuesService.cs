namespace Plugins.SolarEdge.Contracts;

public interface ICurrentValuesService
{
    Task<int> GetCurrentPowerToGrid();
    Task<int> GetInverterPower();
}