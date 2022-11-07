namespace TeslaSolarCharger.Server.Contracts;

public interface IBaseConfigurationConverter
{
    Task ConvertAllEnvironmentVariables();
    Task ConvertBaseConfigToV1_0();
}