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

    [HttpPost]
    public Task UpdateCarFleetApiState(int carId, TeslaCarFleetApiState fleetApiState) => _indexService.UpdateCarFleetApiState(carId, fleetApiState);
}
