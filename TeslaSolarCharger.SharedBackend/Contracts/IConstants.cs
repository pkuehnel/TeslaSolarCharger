namespace TeslaSolarCharger.SharedBackend.Contracts;

public interface IConstants
{
    string CarStateKey { get; }
    string CarConfigurationKey { get; }
    int MinSocLimit { get; }
    int DefaultOverage { get; }
}
