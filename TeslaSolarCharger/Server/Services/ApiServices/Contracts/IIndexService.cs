using TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;

namespace TeslaSolarCharger.Server.Services.ApiServices.Contracts;

public interface IIndexService
{
    DtoPvValues GetPvValues();
}
