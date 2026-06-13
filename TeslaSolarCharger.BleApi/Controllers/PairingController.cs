using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.BleApi.Abstracts;
using TeslaSolarCharger.BleApi.Dtos;
using TeslaSolarCharger.BleApi.Services.Contracts;

namespace TeslaSolarCharger.BleApi.Controllers;

public class PairingController(IPairingService service) : ApiBaseController
{
    [HttpGet]
    public Task GenerateKeyPair() => service.GenerateKeyPair();

    /// <summary>
    /// Pair a key with the car
    /// </summary>
    /// <param name="vin">VIN of the car</param>
    /// <param name="apiRole">API role, like documented in https://github.com/teslamotors/vehicle-command/blob/05bc5dd8d0649b4ccb45a765b9127d06f1050a6f/pkg/protocol/protocol.md</param>
    /// <returns></returns>
    [HttpGet]
    public Task<DtoBleCommandResult> PairCar(string vin, string apiRole) => service.PairCar(vin, apiRole);
}