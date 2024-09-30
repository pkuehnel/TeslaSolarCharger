using TeslaSolarCharger.Model.Contracts;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ITeslaMateDbContextWrapper
{
    ITeslamateContext? GetTeslaMateContextIfAvailable();
}
