using TeslaSolarCharger.Model.Enums;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IMeterValueLogService
{
    Task LogPvValues();
}
