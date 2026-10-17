using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class CarValueEstimationServiceTests : TestBase
{
    private const int CarId = 1;

    public CarValueEstimationServiceTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Theory]
    // 12 kWh from the wall with 15 % loss are 10.43 kWh in a 50 kWh battery: 50 % + 20.9 % = 70 % (74 % when the loss was ignored)
    [InlineData(15, 12, 70)]
    // Without loss the wall energy is the battery energy: 50 % + 10.25 kWh / 50 kWh = 70.5 %
    [InlineData(0, 10.25, 70)]
    // Nothing charged keeps the last known SoC
    [InlineData(15, 0, 50)]
    public async Task UpdateAllCarValueEstimations_RemovesChargeLossBeforeConvertingEnergyToSoc(int chargeLossPercent, double chargedWallEnergyKwh, int expectedSoc)
    {
        var socTimestamp = CurrentFakeDate.AddHours(-2);
        Context.Cars.Add(new Car { Id = CarId, CarType = CarType.Manual, ShouldBeManaged = true, UsableEnergy = 50, });
        Context.CarValueLogs.Add(new CarValueLog
        {
            CarId = CarId,
            Timestamp = socTimestamp.UtcDateTime,
            Type = CarValueType.StateOfCharge,
            IntValue = 50,
            Source = CarValueSource.Manual,
        });
        const long startCounter = 1_000;
        Context.MeterValues.Add(new MeterValue(socTimestamp.AddMinutes(1), MeterValueKind.Car, 0) { CarId = CarId, EstimatedEnergyWs = startCounter, });
        Context.MeterValues.Add(new MeterValue(socTimestamp.AddMinutes(90), MeterValueKind.Car, 0)
        {
            CarId = CarId,
            EstimatedEnergyWs = startCounter + (long)(chargedWallEnergyKwh * 3_600_000),
        });
        await Context.SaveChangesAsync();
        DetachAllEntities();

        var dtoCar = new DtoCar { Id = CarId, };
        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar> { dtoCar, });
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.CarChargeLoss()).Returns(chargeLossPercent);
        var service = Mock.Create<CarValueEstimationService>();

        await service.UpdateAllCarValueEstimations(CancellationToken.None);

        Assert.Equal(expectedSoc, dtoCar.SoC.Value);
        var estimatedLog = await Context.CarValueLogs.SingleAsync(l => l.Source == CarValueSource.Estimation);
        Assert.Equal(expectedSoc, estimatedLog.IntValue);
    }
}
