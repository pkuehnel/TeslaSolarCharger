using Microsoft.AspNetCore.Mvc;
using PkSoftwareService.Custom.Backend.Ble;
using TeslaSolarCharger.BleApi.Abstracts;
using TeslaSolarCharger.BleApi.Services.Contracts;

namespace TeslaSolarCharger.BleApi.Controllers;

public class PresenceController(IBleWorkerService bleWorkerService) : ApiBaseController
{
    /// <summary>
    /// What the container knows about the given cars, from the permanent background scan and from the outcome of
    /// every command. Answered from memory: it never touches the radio, never wakes a car and never delays a command.
    ///
    /// Replaces the beacon scan. The answer is the age of the newest evidence, not the result of listening for a
    /// window - a car emits nothing at all while it holds a connection to us, so a window would report a car standing
    /// in the garage as absent.
    /// </summary>
    /// <param name="vins">Comma separated VINs to report on. Cars the radio heard are listed either way.</param>
    /// <param name="adapter">optional stable adapter id (BD address); omitted = container default adapter</param>
    /// <param name="keepWarmSeconds">keeps the adapter's worker, and with it the background scan, alive between polls</param>
    /// <param name="maxAgeSeconds">how long ago a car may last have been heard and still count as present</param>
    [HttpGet]
    public Task<DtoBlePresenceResult> Get(string? vins = null, string? adapter = null, int? keepWarmSeconds = null,
        int? maxAgeSeconds = null)
    {
        var vinList = (vins ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        return bleWorkerService.Presence(adapter, vinList, keepWarmSeconds, maxAgeSeconds);
    }
}
