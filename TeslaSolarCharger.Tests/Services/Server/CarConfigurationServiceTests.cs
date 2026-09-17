using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Car;
using TeslaSolarCharger.Shared.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class CarConfigurationServiceTests(ITestOutputHelper outputHelper) : TestBase(outputHelper)
{
    private const string AccountCarVin = "VIN_IN_ACCOUNT";
    private const string RemovedCarVin = "VIN_REMOVED_FROM_ACCOUNT";
    private const string ManualCarVin = "VIN_MANUAL";

    [Fact]
    public async Task AddAllMissingCarsFromTeslaAccount_AccountCarsCouldNotBeLoaded_ThrowsWithoutChangingCars()
    {
        await AddExistingCars();
        SetupTeslaAccountCars(new(null, "No Backend token found.", null));
        var service = Mock.Create<TeslaSolarCharger.Server.Services.CarConfigurationService>();

        var exception = await Assert.ThrowsAsync<Exception>(() => service.AddAllMissingCarsFromTeslaAccount());

        Assert.Equal("No Backend token found.", exception.Message);
        await AssertExistingCarsUnchanged();
    }

    [Fact]
    public async Task AddAllMissingCarsFromTeslaAccount_NoErrorButNoCarList_ThrowsInsteadOfMarkingCarsUnavailable()
    {
        await AddExistingCars();
        SetupTeslaAccountCars(new(null, null, null));
        var service = Mock.Create<TeslaSolarCharger.Server.Services.CarConfigurationService>();

        var exception = await Assert.ThrowsAsync<Exception>(() => service.AddAllMissingCarsFromTeslaAccount());

        Assert.Equal("Could not get new cars from Tesla account.", exception.Message);
        await AssertExistingCarsUnchanged();
    }

    [Fact]
    public async Task AddAllMissingCarsFromTeslaAccount_AccountCarsLoaded_AddsMissingCarsAndMarksRemovedCarsUnavailable()
    {
        await AddExistingCars();
        SetupTeslaAccountCars(new(
            [
                new DtoTesla { Vin = AccountCarVin, Name = "Existing", },
                new DtoTesla { Vin = "NEW_VIN", Name = "New Model Y", },
            ],
            null,
            null));
        var service = Mock.Create<TeslaSolarCharger.Server.Services.CarConfigurationService>();

        var addedCars = await service.AddAllMissingCarsFromTeslaAccount();

        Assert.Equal(1, addedCars);
        var cars = await Context.Cars.AsNoTracking().ToDictionaryAsync(c => c.Vin!);
        Assert.Equal(4, cars.Count);
        Assert.True(cars[AccountCarVin].IsAvailableInTeslaAccount);
        Assert.True(cars[AccountCarVin].ShouldBeManaged);
        Assert.False(cars[RemovedCarVin].IsAvailableInTeslaAccount);
        Assert.False(cars[RemovedCarVin].ShouldBeManaged);
        Assert.True(cars[ManualCarVin].ShouldBeManaged);
        var newCar = cars["NEW_VIN"];
        Assert.Equal("New Model Y", newCar.Name);
        Assert.Equal(CarType.Tesla, newCar.CarType);
        Assert.True(newCar.IsAvailableInTeslaAccount);
        Assert.False(newCar.ShouldBeManaged);
        Assert.Equal(4, newCar.ChargingPriority);
    }

    private async Task AddExistingCars()
    {
        Context.Cars.Add(new Car { Vin = AccountCarVin, CarType = CarType.Tesla, ShouldBeManaged = true, IsAvailableInTeslaAccount = true, ChargingPriority = 1, });
        Context.Cars.Add(new Car { Vin = RemovedCarVin, CarType = CarType.Tesla, ShouldBeManaged = true, IsAvailableInTeslaAccount = true, ChargingPriority = 3, });
        Context.Cars.Add(new Car { Vin = ManualCarVin, CarType = CarType.Manual, ShouldBeManaged = true, ChargingPriority = 2, });
        await Context.SaveChangesAsync();
        DetachAllEntities();
    }

    private void SetupTeslaAccountCars(Result<List<DtoTesla>> result)
    {
        Mock.Mock<ITeslaMateDbContextWrapper>().Setup(w => w.GetTeslaMateContextIfAvailable()).Returns((ITeslamateContext?)null);
        Mock.Mock<ITeslaFleetApiService>().Setup(s => s.GetAllCarsFromAccount()).ReturnsAsync(result);
    }

    private async Task AssertExistingCarsUnchanged()
    {
        var cars = await Context.Cars.AsNoTracking().ToListAsync();
        Assert.Equal(3, cars.Count);
        Assert.All(cars.Where(c => c.CarType == CarType.Tesla), c =>
        {
            Assert.True(c.IsAvailableInTeslaAccount);
            Assert.True(c.ShouldBeManaged);
        });
    }
}
