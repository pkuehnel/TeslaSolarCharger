using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Ble;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Client.Services.Contracts;

public interface ICarSettingsService
{
    Task<List<CarBasicConfiguration>?> GetCarBasicConfigurations();
    Task<DtoCarLicenseInfo?> GetFleetApiLicenseInfo();
    Task<TokenState> GetFleetApiTokenState();
    Task<DtoBleCommandResult?> PairKey(string vin);
    Task<DtoBleCommandResult?> SetAmp(string vin, int amps);
    Task<DtoBleCommandResult?> WakeUp(string vin);
    Task<bool> DisconnectCarFromSmartCar(int carId);
    Task<string?> GetSmartCarOAuthRedeemUrl(string baseUrl, string vin);
}
