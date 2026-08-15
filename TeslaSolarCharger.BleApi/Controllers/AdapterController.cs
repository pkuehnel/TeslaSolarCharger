using Microsoft.AspNetCore.Mvc;
using PkSoftwareService.Custom.Backend.Ble;
using TeslaSolarCharger.BleApi.Abstracts;
using TeslaSolarCharger.BleApi.Services.Contracts;

namespace TeslaSolarCharger.BleApi.Controllers;

public class AdapterController(IAdapterEnumerationService adapterEnumerationService,
    IBleWorkerService bleWorkerService) : ApiBaseController
{
    /// <summary>
    /// Lists the Bluetooth adapters of this host. Enumeration is read only (HCI control socket, never bound) and can
    /// not disturb a running worker.
    /// </summary>
    [HttpGet]
    public List<DtoBleAdapter> List()
    {
        var adapters = adapterEnumerationService.GetAdapters(bypassCache: true);
        var runningKeys = bleWorkerService.GetRunningAdapterKeys();
        foreach (var adapter in adapters)
        {
            //A worker holding the exclusive user channel leaves the device down from the kernel's perspective;
            //report ownership instead so the state is not misread as a problem.
            if (adapter.AddressKnown && runningKeys.Contains(adapter.StableId!, StringComparer.OrdinalIgnoreCase))
            {
                adapter.State = BleAdapterState.OwnedByWorker;
            }
        }
        return adapters;
    }
}
