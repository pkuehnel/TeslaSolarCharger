using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;

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
}
