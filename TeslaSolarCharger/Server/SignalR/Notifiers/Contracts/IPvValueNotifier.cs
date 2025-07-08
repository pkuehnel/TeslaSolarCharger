using TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;

namespace TeslaSolarCharger.Server.SignalR.Notifiers.Contracts;

public interface IPvValueNotifier
{
    Task NotifyNewValuesAsync(DtoPvValues newValues);
}
