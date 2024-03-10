using System.Collections.Generic;
using System.Linq;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server;

public class ConfigJsonService : TestBase
{
    public ConfigJsonService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    //ToDo: need to be able to handle vins instead of IDs
    //[Fact]
    //public void Adds_every_new_car()
    //{
    //    var newCarIds = new List<int>() { 1, 2, 3, 4 };
    //    var cars = new List<DtoCar>();

    //    var configJsonService = Mock.Create<TeslaSolarCharger.Server.Services.ConfigJsonService>();
    //    configJsonService.AddNewCars(newCarIds, cars);

    //    Assert.Equal(newCarIds.Count, cars.Count);
    //}

    //[Fact]
    //public void Sets_correct_default_values_on_new_cars()
    //{
    //    var newCarIds = new List<int>() { 1, 2, 3, 4 };
    //    var cars = new List<DtoCar>();

    //    var configJsonService = Mock.Create<TeslaSolarCharger.Server.Services.ConfigJsonService>();
    //    configJsonService.AddNewCars(newCarIds, cars);

    //    foreach (var car in cars)
    //    {
    //        Assert.Equal(ChargeMode.PvAndMinSoc, car.CarConfiguration.ChargeMode);
    //        Assert.Equal(16, car.CarConfiguration.MaximumAmpere);
    //        Assert.Equal(1, car.CarConfiguration.MinimumAmpere);
    //        Assert.Equal(75, car.CarConfiguration.UsableEnergy);
    //        Assert.Null(car.CarState.ShouldStartChargingSince);
    //        Assert.Null(car.CarState.ShouldStopChargingSince);
    //    }
    //}

    //[Fact]
    //public void Removes_old_cars()
    //{
    //    var newCarIds = new List<int>() { 1, 2, 3, 4 };
    //    var cars = new List<DtoCar>();

    //    var configJsonService = Mock.Create<TeslaSolarCharger.Server.Services.ConfigJsonService>();
    //    configJsonService.AddNewCars(newCarIds, cars);

    //    configJsonService.RemoveOldCars(cars, new List<int>() { 1, 3 });

    //    Assert.Contains(cars, car => car.Id == 1);
    //    Assert.Contains(cars, car => car.Id == 3);
    //    Assert.DoesNotContain(cars, car => car.Id == 2);
    //    Assert.DoesNotContain(cars, car => car.Id == 4);
    //}

}
