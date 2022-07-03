namespace SmartTeslaAmpSetter.Server.Contracts;

public interface ICarDbUpdateService
{
    Task UpdateCarsFromDatabase();
}