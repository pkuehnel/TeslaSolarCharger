using MudBlazor;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Ble;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Localization;
using TeslaSolarCharger.Shared.Localization.Contracts;
using TeslaSolarCharger.Shared.Localization.Registries;
using TeslaSolarCharger.Shared.Localization.Registries.Pages;

namespace TeslaSolarCharger.Client.Services;

public class CarSettingsService(ILogger<CarSettingsService> logger,
    IHttpClientHelper httpClientHelper,
    ISnackbar snackbar,
    ITextLocalizationService textLocalizer) : ICarSettingsService
{
    private string TF(string key, params object[] args) =>
        textLocalizer.GetFormat<CarSettingsPageLocalizationRegistry>(key, args, typeof(SharedComponentLocalizationRegistry));

    public async Task<List<CarBasicConfiguration>?> GetCarBasicConfigurations()
    {
        logger.LogTrace("{method}()", nameof(GetCarBasicConfigurations));
        return await httpClientHelper.SendGetRequestWithSnackbarAsync<List<CarBasicConfiguration>>("/api/Config/GetCarBasicConfigurations");
    }

    public async Task<DtoCarLicenseInfo?> GetFleetApiLicenseInfo()
    {
        logger.LogTrace("{method}()", nameof(GetFleetApiLicenseInfo));
        return await httpClientHelper.SendGetRequestWithSnackbarAsync<DtoCarLicenseInfo>("api/BackendApi/GetFleetApiLicenseInfo");
    }

    public async Task<TokenState?> GetFleetApiTokenState()
    {
        logger.LogTrace("{method}()", nameof(GetFleetApiTokenState));
        var response = await httpClientHelper.SendGetRequestWithSnackbarAsync<DtoValue<TokenState>>("api/FleetApi/FleetApiTokenState");
        return response?.Value;
    }

    public async Task<CarBasicConfiguration?> UpdateCarBasicConfiguration(int id, CarBasicConfiguration configuration)
    {
        logger.LogTrace("{method}({id})", nameof(UpdateCarBasicConfiguration), id);
        return await httpClientHelper.SendPostRequestWithSnackbarAsync<CarBasicConfiguration>($"api/Config/UpdateCarBasicConfiguration?carId={id}", configuration);
    }

    public async Task DeleteCar(int id)
    {
        logger.LogTrace("{method}({id})", nameof(DeleteCar), id);
        await httpClientHelper.SendDeleteRequestWithSnackbarAsync<object>($"api/Config/DeleteCar?carId={id}");
    }

    public async Task<DtoBleCommandResult?> PairKey(string vin)
    {
        logger.LogTrace("{method}({vin})", nameof(PairKey), vin);
        var url = $"/api/Ble/PairKey?vin={vin}&apiRole=charging_manager";
        // PairKey in original code used HttpClient.GetStringAsync and manual deserialization. 
        // We use SendGetRequestWithSnackbarAsync for consistency and error handling.
        return await httpClientHelper.SendGetRequestWithSnackbarAsync<DtoBleCommandResult>(url);
    }

    public async Task<DtoBleCommandResult?> SetAmp(string vin, int amps)
    {
        logger.LogTrace("{method}({vin}, {amps})", nameof(SetAmp), vin, amps);
        var url = $"/api/Ble/SetAmp?vin={vin}&amps={amps}";
        return await httpClientHelper.SendGetRequestWithSnackbarAsync<DtoBleCommandResult>(url);
    }

    public async Task<DtoBleCommandResult?> WakeUp(string vin)
    {
        logger.LogTrace("{method}({vin})", nameof(WakeUp), vin);
        var url = $"/api/Ble/WakeUp?vin={vin}";
        return await httpClientHelper.SendGetRequestWithSnackbarAsync<DtoBleCommandResult>(url);
    }

    public async Task<bool> DisconnectCarFromSmartCar(int carId)
    {
        logger.LogTrace("{method}({carId})", nameof(DisconnectCarFromSmartCar), carId);
        var url = $"/api/Config/DisconnectCarFromSmartCar?carId={Uri.EscapeDataString(carId.ToString())}";
        var result = await httpClientHelper.SendPostRequestAsync<object>(url, null);
        if (result.HasError)
        {
            snackbar.Add(TF(TranslationKeys.CarSettingsSmartCarDisconnectError, result.ErrorMessage ?? string.Empty), Severity.Error);
        }
        return !result.HasError;
    }

    public async Task<string?> GetSmartCarOAuthRedeemUrl(string baseUrl, string vin)
    {
        logger.LogTrace("{method}({baseUrl}, {vin})", nameof(GetSmartCarOAuthRedeemUrl), baseUrl, vin);
        var url = $"/api/BackendApi/GetSmartCarOAuthRedeemUrl?baseUrl={Uri.EscapeDataString(baseUrl)}&vin={Uri.EscapeDataString(vin)}";
        var response = await httpClientHelper.SendGetRequestWithSnackbarAsync<DtoValue<string>>(url);
        return response?.Value;
    }
}
