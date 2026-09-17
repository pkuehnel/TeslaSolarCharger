using Microsoft.AspNetCore.Mvc;
using PkSoftwareService.Custom.Backend.Ble;
using TeslaSolarCharger.BleApi.Abstracts;
using TeslaSolarCharger.BleApi.Dtos;
using TeslaSolarCharger.BleApi.Services.Contracts;

namespace TeslaSolarCharger.BleApi.Controllers;

public class HelloController (IHelloService service) :ApiBaseController
{
    [HttpGet]
    public bool IsAlive() => true;

    [HttpGet]
    public Task<bool> FinallyTest() => service.IsAlive();

    [HttpGet]
    public DtoValue<Version> TscVersionCompatibility() => new(BleCompatibilityVersion.Value);
}
