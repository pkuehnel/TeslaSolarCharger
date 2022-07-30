namespace TeslaSolarCharger.Server.Contracts;

public interface IBaseConfigurationConverter
{
    Task ConvertAllEnvironmentVariables();
    Task ConvertBaseConfigToCurrentVersion();
}