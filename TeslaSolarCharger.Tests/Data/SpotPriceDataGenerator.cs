using System;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.EntityFramework;

namespace TeslaSolarCharger.Tests.Data;

public static class SpotPriceDataGenerator
{
    public static TeslaSolarChargerContext InitSpotPrices(this TeslaSolarChargerContext context)
    {
        context.SpotPrices.Add(new SpotPrice()
        {
            StartDate = new DateTime(2023, 1, 22, 17, 0, 0),
            EndDate = new DateTime(2023, 1, 22, 18, 0, 0), Price = new decimal(0.11)
        });
        return context;
    }
}
