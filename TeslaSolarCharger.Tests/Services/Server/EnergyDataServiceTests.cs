using Autofac;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Server.Helper;
using TeslaSolarCharger.Server.Helper.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class EnergyDataServiceTests : TestBase
{
    private static readonly DateTimeOffset PredictionStart = new(2023, 6, 16, 0, 0, 0, TimeSpan.Zero);
    private const int HistoricDays = 21;
    private const int NormalRadiationWhPerM2 = 500;
    private const int DailyProductionWh = 2500;

    public EnergyDataServiceTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public async Task GetPredictedSolarProduction_UniformHistory_PredictsHistoricProduction()
    {
        await SeedHistory(radiationOnMostRecentDay: NormalRadiationWhPerM2);
        var service = CreateService();

        var result = await service.GetPredictedSolarProductionByLocalHour(PredictionStart, PredictionStart.AddDays(1),
            TimeSpan.FromHours(1), CancellationToken.None);

        var predictedProductionWh = result[PredictionStart.AddHours(11)];
        Assert.InRange(predictedProductionWh, DailyProductionWh - 1, DailyProductionWh + 1);
    }

    [Fact]
    public async Task GetPredictedSolarProduction_SingleWrongLowRadiationForecast_DoesNotExplodePrediction()
    {
        //The most recent historic day had a forecast of nearly no radiation while actual production was as high as on
        //all other days. Averaging the production/radiation ratios instead of dividing the weighted sums would let
        //this single day dominate the conversion factor and predict roughly ten times too much energy for weeks.
        await SeedHistory(radiationOnMostRecentDay: 5);
        var service = CreateService();

        var result = await service.GetPredictedSolarProductionByLocalHour(PredictionStart, PredictionStart.AddDays(1),
            TimeSpan.FromHours(1), CancellationToken.None);

        var predictedProductionWh = result[PredictionStart.AddHours(11)];
        Assert.InRange(predictedProductionWh, DailyProductionWh - 100, DailyProductionWh + 500);
    }

    private async Task SeedHistory(int radiationOnMostRecentDay)
    {
        for (var daysBeforePredictionStart = 1; daysBeforePredictionStart <= HistoricDays; daysBeforePredictionStart++)
        {
            var productionHourStart = PredictionStart.AddDays(-daysBeforePredictionStart).AddHours(11);
            var baseEnergyWs = daysBeforePredictionStart * 100_000_000L;
            Context.MeterValues.Add(new MeterValue(productionHourStart, MeterValueKind.SolarGeneration, 0)
            {
                EstimatedEnergyWs = baseEnergyWs,
            });
            Context.MeterValues.Add(new MeterValue(productionHourStart.AddHours(1), MeterValueKind.SolarGeneration, 0)
            {
                EstimatedEnergyWs = baseEnergyWs + DailyProductionWh * 3600L,
            });
            Context.SolarRadiations.Add(new SolarRadiation
            {
                Start = productionHourStart,
                End = productionHourStart.AddHours(1),
                SolarRadiationWhPerM2 = daysBeforePredictionStart == 1 ? radiationOnMostRecentDay : NormalRadiationWhPerM2,
                CreatedAt = productionHourStart,
            });
        }
        //Forecast for the hour whose production should be predicted
        Context.SolarRadiations.Add(new SolarRadiation
        {
            Start = PredictionStart.AddHours(11),
            End = PredictionStart.AddHours(12),
            SolarRadiationWhPerM2 = NormalRadiationWhPerM2,
            CreatedAt = PredictionStart,
        });
        await Context.SaveChangesAsync();
        DetachAllEntities();
    }

    private TeslaSolarCharger.Server.Services.EnergyDataService CreateService()
    {
        Mock.Mock<IDateTimeProvider>().Setup(p => p.DateTimeOffSetUtcNow()).Returns(PredictionStart);
        var scopedServiceProvider = new Mock<IServiceProvider>();
        scopedServiceProvider.Setup(p => p.GetService(typeof(ITeslaSolarChargerContext))).Returns(Context);
        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(scopedServiceProvider.Object);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);
        Mock.Mock<IServiceProvider>().Setup(p => p.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactory.Object);
        return Mock.Create<TeslaSolarCharger.Server.Services.EnergyDataService>(
            TypedParameter.From<IMemoryCache>(new MemoryCache(new MemoryCacheOptions())),
            TypedParameter.From<ITimestampHelper>(Mock.Create<TimestampHelper>()));
    }
}
