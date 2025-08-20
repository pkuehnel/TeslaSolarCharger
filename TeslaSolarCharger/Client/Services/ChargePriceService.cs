using MudBlazor;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;

namespace TeslaSolarCharger.Client.Services;

public class ChargePriceService : IChargePriceService
{
    private readonly ILogger<ChargePriceService> _logger;
    private readonly IHttpClientHelper _httpClientHelper;
    private readonly ISnackbar _snackbar;

    public ChargePriceService(ILogger<ChargePriceService> logger, IHttpClientHelper httpClientHelper, ISnackbar snackbar)
    {
        _logger = logger;
        _httpClientHelper = httpClientHelper;
        _snackbar = snackbar;
    }

    public async Task<DtoChargePrice?> GetDtoChargePrice(int id)
    {
        _logger.LogTrace("{method}({id})", nameof(GetDtoChargePrice), id);
        var result = await _httpClientHelper.SendGetRequestAsync<DtoChargePrice>($"api/ChargingCost/GetChargePriceById?id={id}");
        if (result.HasError)
        {
            _logger.LogError("Failed to get charge price by id {id}: {error}", id, result.ErrorMessage);
            if (result.ErrorMessage != default)
            {
                _snackbar.Add(result.ErrorMessage, Severity.Error);
            }
        }
        return result.Data;
    }

    public async Task UpdateChargePrice(DtoChargePrice chargePrice)
    {
        _logger.LogTrace("{method}({@chargePrice})", nameof(UpdateChargePrice), chargePrice);
        var result = await _httpClientHelper.SendPostRequestAsync<object?>("api/ChargingCost/UpdateChargePrice", chargePrice);
        if (result.HasError)
        {
            _snackbar.Add("Error while updating charge price: " + result.ErrorMessage, Severity.Error);
        }
        else
        {
            _snackbar.Add("Charge price updated successfully", Severity.Success);
        }
    }

    public async Task<DtoProgress?> GetChargePriceUpdateProgress()
    {
        _logger.LogTrace("{method}()", nameof(GetChargePriceUpdateProgress));
        var result = await _httpClientHelper.SendGetRequestAsync<DtoProgress?>($"api/ChargingCost/GetChargePriceUpdateProgress");
        if (result.HasError)
        {
            _logger.LogError("Failed to get progress: {errorMessage}", result.ErrorMessage);
            if (result.ErrorMessage != default)
            {
                _snackbar.Add(result.ErrorMessage, Severity.Error);
            }
        }
        return result.Data;
    }
}
