using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ICarDataProvider
{
    CarType SupportedCarType { get; }
    Task RefreshCarData();
}
