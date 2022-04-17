using System;
using System.Collections.Generic;
using System.Linq;
using SmartTeslaAmpSetter.Shared.Dtos.Settings;
using SmartTeslaAmpSetter.Shared.Enums;
using Xunit;
using Xunit.Abstractions;

namespace SmartTeslaAmpSetter.Tests.Services;

public class ConfigJsonService : TestBase
{
    public ConfigJsonService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public void Adds_every_new_car()
    {
        var newCarIds = new List<int>() { 1, 2, 3, 4 };
        var cars = new List<Car>();

        var configJsonService = Mock.Create<Server.Services.ConfigJsonService>();
        configJsonService.AddNewCars(newCarIds, cars);

        Assert.Equal(newCarIds.Count, cars.Count);
    }

    [Fact]
    public void Sets_correct_default_values_on_new_cars()
    {
        var newCarIds = new List<int>() { 1, 2, 3, 4 };
        var cars = new List<Car>();

        var configJsonService = Mock.Create<Server.Services.ConfigJsonService>();
        configJsonService.AddNewCars(newCarIds, cars);

        foreach (var car in cars)
        {
            Assert.Equal(ChargeMode.MaxPower, car.CarConfiguration.ChargeMode);
            Assert.True(car.CarConfiguration.UpdatedSincLastWrite);
            Assert.Equal(16, car.CarConfiguration.MaximumAmpere);
            Assert.Equal(2, car.CarConfiguration.MinimumAmpere);
            Assert.Equal(75, car.CarConfiguration.UsableEnergy);
            Assert.Equal(DateTime.MaxValue, car.CarState.ShouldStartChargingSince);
            Assert.Equal(DateTime.MaxValue, car.CarState.ShouldStopChargingSince);
        }
    }

    [Fact]
    public void Removes_old_cars()
    {
        var newCarIds = new List<int>() { 1, 2, 3, 4 };
        var cars = new List<Car>();

        var configJsonService = Mock.Create<Server.Services.ConfigJsonService>();
        configJsonService.AddNewCars(newCarIds, cars);

        configJsonService.RemoveOldCars(cars, new List<int>() { 1, 3 });

        Assert.Contains(cars, car => car.Id == 1);
        Assert.Contains(cars, car => car.Id == 3);
        Assert.DoesNotContain(cars, car => car.Id == 2);
        Assert.DoesNotContain(cars, car => car.Id == 4);
    }

    [Theory]
    [InlineData("[{\"Id\":1,\"CarConfiguration\":{\"ChargeMode\":1,\"MinimumSoC\":0,\"LatestTimeToReachSoC\":\"2022-04-11T00:00:00\",\"MaximumAmpere\":16,\"MinimumAmpere\":1,\"UsableEnergy\":75}},{\"Id\":2,\"CarConfiguration\":{\"ChargeMode\":2,\"MinimumSoC\":45,\"LatestTimeToReachSoC\":\"2022-04-11T00:00:00\",\"MaximumAmpere\":16,\"MinimumAmpere\":1,\"UsableEnergy\":75}}]")]
    public void Deserializes_car_configuration(string configString)
    {
        var configJsonService = Mock.Create<Server.Services.ConfigJsonService>();
        var cars = configJsonService.DeserializeCarsFromConfigurationString(configString);

        Assert.Equal(2, cars.Count);

        var firstCar = cars.First();
        var lastCar = cars.Last();

        Assert.Equal(ChargeMode.PvOnly, firstCar.CarConfiguration.ChargeMode);
        Assert.Equal(ChargeMode.PvAndMinSoc, lastCar.CarConfiguration.ChargeMode);

        Assert.Equal(1, firstCar.Id);
        Assert.Equal(2, lastCar.Id);

        Assert.Equal(0, firstCar.CarConfiguration.MinimumSoC);
        Assert.Equal(45, lastCar.CarConfiguration.MinimumSoC);
    }
}