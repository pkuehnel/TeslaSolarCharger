namespace TeslaSolarCharger.Server.Contracts;

public interface IEnvironmentVariableConverter
{
    Task ConvertAllValues();
}