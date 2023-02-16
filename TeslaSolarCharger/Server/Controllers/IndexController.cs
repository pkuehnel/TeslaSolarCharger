using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.CarValues;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Controllers;

public class IndexController : ApiBaseController
{
    private readonly IIndexService _indexService;

    public IndexController(IIndexService indexService)
    {
        _indexService = indexService;
    }

    [HttpGet]
    public DtoPvValues GetPvValues() => _indexService.GetPvValues();

    [HttpGet]
    public Task<List<DtoCarBaseStates>> GetCarBaseStatesOfEnabledCars() => _indexService.GetCarBaseStatesOfEnabledCars();

    [HttpGet]
    public Dictionary<int, DtoCarBaseSettings> GetCarBaseSettingsOfEnabledCars() => _indexService.GetCarBaseSettingsOfEnabledCars();

    [HttpGet]
    public List<DtoCarTopicValue> CarDetails(int carId)
        => _indexService.GetCarDetails(carId);

    [HttpPost]
    public Task UpdateCarBaseSettings([FromBody] DtoCarBaseSettings carBaseSettings) => _indexService.UpdateCarBaseSettings(carBaseSettings);

    [HttpGet]
    public Dictionary<string, string> GetToolTipTexts() => _indexService.GetToolTipTexts();

    [HttpGet]
    public List<DtoChargingSlot> RecalculateAndGetChargingSlots(int carId) => _indexService.RecalculateAndGetChargingSlots(carId);

    [HttpGet]
    public List<DtoChargingSlot> GetChargingSlots(int carId) => _indexService.GetChargingSlots(carId);
}
