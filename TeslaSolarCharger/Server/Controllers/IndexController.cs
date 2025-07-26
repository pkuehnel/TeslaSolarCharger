using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.CarValues;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class IndexController : ApiBaseController
{
    private readonly IIndexService _indexService;

    public IndexController(IIndexService indexService)
    {
        _indexService = indexService;
    }

    [HttpGet]
    public Task<List<DtoCarBaseStates>> GetCarBaseStatesOfEnabledCars() => _indexService.GetCarBaseStatesOfEnabledCars();
    
    [HttpGet]
    public DtoCarTopicValues CarDetails(int carId)
        => _indexService.GetCarDetails(carId);
    
    [HttpGet]
    public Dictionary<string, string> GetToolTipTexts() => _indexService.GetToolTipTexts();

    [HttpGet]
    public List<DtoChargingSlot> RecalculateAndGetChargingSlots(int carId) => _indexService.RecalculateAndGetChargingSlots(carId);

    [HttpGet]
    public List<DtoChargingSlot> GetChargingSlots(int carId) => _indexService.GetChargingSlots(carId);

    [HttpPost]
    public Task UpdateCarFleetApiState(int carId, TeslaCarFleetApiState fleetApiState) => _indexService.UpdateCarFleetApiState(carId, fleetApiState);
}
