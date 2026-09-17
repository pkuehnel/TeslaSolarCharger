using PkSoftwareService.Custom.Backend.Ble;
using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Ble;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Client.Services.Contracts;

public interface ICarSettingsService
{
    Task<List<CarBasicConfiguration>?> GetCarBasicConfigurations();
    Task<DtoCarLicenseInfo?> GetFleetApiLicenseInfo();
    Task<Result<object>> UpdateCarBasicConfiguration(int id, CarBasicConfiguration configuration);
    Task DeleteCar(int id);
    Task<DtoCarDeletionProgress?> GetCarDeletionProgress(int id);
    Task<TokenState?> GetFleetApiTokenState();
    Task<DtoBleCommandResult?> PairKey(string vin);

    /// <summary>
    /// Checks whether TSC can control the car via BLE. Uses the car's saved BLE configuration, so the car has to be
    /// saved before testing.
    /// </summary>
    Task<DtoBleConnectionTestResult?> TestBleConnection(string vin);
    Task<DtoBleCommandResult?> SetAmp(string vin, int amps);
    Task<DtoBleCommandResult?> WakeUp(string vin);
    Task<bool> DisconnectCarFromSmartCar(int carId);
    Task<string?> GetSmartCarOAuthRedeemUrl(string baseUrl, string vin);
    Task<string?> GetSmartCarOAuthRedeemUrlForNewCar(string baseUrl);
    Task<List<DtoSmartCarCompatibleVehicle>?> GetSmartCarCompatibleVehicles();
    Task SyncSmartCarCars();

    /// <summary>
    /// Imports all cars of the connected Tesla account that are not known to TSC yet.
    /// </summary>
    /// <returns>The number of newly added cars, or <c>null</c> if the import failed.</returns>
    Task<int?> RefreshTeslaCarsFromAccount();

    /// <summary>
    /// Localized message telling the user how many cars were added by <see cref="RefreshTeslaCarsFromAccount"/>.
    /// </summary>
    string GetAddedTeslasMessage(int addedCarsCount);
}
