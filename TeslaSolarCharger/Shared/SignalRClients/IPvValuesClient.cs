using TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;

namespace TeslaSolarCharger.Shared.SignalRClients;

public interface IPvValuesClient
{
    Task ReceivePvValues(DtoPvValues values);
}
