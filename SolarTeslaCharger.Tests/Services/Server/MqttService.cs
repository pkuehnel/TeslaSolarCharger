using System;
using System.Collections.Generic;
using System.Linq;
using SolarTeslaCharger.Server.Services;
using SolarTeslaCharger.Shared.Dtos.Contracts;
using SolarTeslaCharger.Shared.Dtos.Settings;
using Xunit;
using Xunit.Abstractions;

namespace SolarTeslaCharger.Tests.Services.Server;

public class MqttService : TestBase
{
    public MqttService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Theory]
    [InlineData("1")]
    [InlineData("3")]
    [InlineData("4")]
    [InlineData("5")]
    [InlineData("8")]
    public void ReducesActualCurrentToLastSetAmpIfDifferenceIsOneAndBelow5A(string value)
    {
        var cars = new List<Car>()
        {
            new Car()
            {
                Id = 1,
                CarState = new CarState()
                {
                    LastSetAmp = 3,
                },
            },
        };
        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(cars);

        var mqttService = Mock.Create<SolarTeslaCharger.Server.Services.MqttService>();

        var teslamateValue = new TeslaMateValue()
        {
            CarId = 1,
            Topic = "charger_actual_current",
            Value = value,
        };
        mqttService.UpdateCar(teslamateValue);

        switch (value)
        {
            case "1":
                Assert.Equal(1, cars.First().CarState.ChargerActualCurrent);
                break;
            case "3":
            case "4":
                Assert.Equal(3, cars.First().CarState.ChargerActualCurrent);
                break;
            case "5":
                Assert.Equal(5, cars.First().CarState.ChargerActualCurrent);
                break;
            case "8":
                Assert.Equal(8, cars.First().CarState.ChargerActualCurrent);
                break;
            default:
                throw new NotImplementedException();
        }
    }
}