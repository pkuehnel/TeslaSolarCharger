namespace SolarTeslaCharger.Server.Contracts;

public interface ICarDbUpdateService
{
    Task UpdateCarsFromDatabase();
}