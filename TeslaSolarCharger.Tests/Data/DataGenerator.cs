using TeslaSolarCharger.Model.EntityFramework;

namespace TeslaSolarCharger.Tests.Data;

public static class DataGenerator
{
    public static void InitContextData(this TeslaSolarChargerContext ctx)
    {
        ctx.InitSpotPrices();
    }
}
