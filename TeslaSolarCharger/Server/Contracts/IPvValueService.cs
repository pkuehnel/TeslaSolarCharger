namespace TeslaSolarCharger.Server.Contracts;

public interface IPvValueService
{
    Task UpdatePvValues();
    int GetAveragedOverage();
}