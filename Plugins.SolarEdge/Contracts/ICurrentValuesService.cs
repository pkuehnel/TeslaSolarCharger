namespace Plugins.SolarEdge.Contracts;

public interface ICurrentValuesService
{
    Task<int> GetCurrentPower();
}