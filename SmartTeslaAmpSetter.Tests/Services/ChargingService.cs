using System;
using Autofac;
using SmartTeslaAmpSetter.Shared.Dtos.Settings;
using SmartTeslaAmpSetter.Shared.Enums;
using SmartTeslaAmpSetter.Shared.TimeProviding;
using Xunit;
using Xunit.Abstractions;

namespace SmartTeslaAmpSetter.Tests.Services;

public class ChargingService : TestBase
{
    public ChargingService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Theory]
    [InlineData(ChargeMode.PvAndMinSoc)]
    [InlineData(ChargeMode.PvOnly)]
    public void Autoenables_full_speed_charge_if_min_soc_not_reachable(ChargeMode chargeMode)
    {
        var chargingService = Mock.Create<Server.Services.ChargingService>();
        var currentTimeProvider = Mock.Create<FakeDateTimeProvider>(
            new NamedParameter("dateTime", new DateTime(2022, 4, 1, 14, 0, 0)));
        var currentTime = currentTimeProvider.Now();

        var timeSpanToLatestTimeToReachMinSoc = TimeSpan.FromMinutes(60);
        var timeSpanToReachMinSoCAtFullSpeedCharge = TimeSpan.FromMinutes(62);

        var car = CreateDemoCar(chargeMode, currentTime + timeSpanToLatestTimeToReachMinSoc, 50, 60);
        chargingService.EnableFullSpeedChargeIfMinimumSocNotReachable(car, currentTime + timeSpanToReachMinSoCAtFullSpeedCharge);

        Assert.True(car.CarState.AutoFullSpeedCharge);
    }


    [Theory]
    [InlineData(ChargeMode.PvAndMinSoc)]
    [InlineData(ChargeMode.PvOnly)]
    public void Does_only_autoenable_full_speed_charge_if_soc_lower_min_soc_on_pvAndMinSoc_charge_mode(ChargeMode chargeMode)
    {
        var chargingService = Mock.Create<Server.Services.ChargingService>();
        var currentTimeProvider = Mock.Create<FakeDateTimeProvider>(
            new NamedParameter("dateTime", new DateTime(2022, 4, 1, 14, 0, 0)));
        var currentTime = currentTimeProvider.Now();

        var timeSpanToLatestTimeToReachMinSoc = TimeSpan.FromMinutes(60);
        var timeSpanToReachMinSoCAtFullSpeedCharge = TimeSpan.FromMinutes(58);

        var car = CreateDemoCar(chargeMode, currentTime + timeSpanToLatestTimeToReachMinSoc, 50, 60);
        chargingService.EnableFullSpeedChargeIfMinimumSocNotReachable(car, currentTime + timeSpanToReachMinSoCAtFullSpeedCharge);


        switch (chargeMode)
        {
            case ChargeMode.PvAndMinSoc:
                Assert.True(car.CarState.AutoFullSpeedCharge);
                break;

            case ChargeMode.PvOnly:
                Assert.False(car.CarState.AutoFullSpeedCharge);
                break;

            default:
                throw new NotImplementedException("This test does not handle this charge mode");
        }
    }

    [Theory]
    [InlineData(ChargeMode.PvAndMinSoc, 60)]
    [InlineData(ChargeMode.PvAndMinSoc, 62)]
    [InlineData(ChargeMode.PvOnly, 60)]
    [InlineData(ChargeMode.PvOnly, 62)]
    public void Does_not_autoenable_full_speed_charge_if_soc_higher_min_soc(ChargeMode chargeMode, int soc)
    {
        var chargingService = Mock.Create<Server.Services.ChargingService>();
        var currentTimeProvider = Mock.Create<FakeDateTimeProvider>(
            new NamedParameter("dateTime", new DateTime(2022, 4, 1, 14, 0, 0)));
        var currentTime = currentTimeProvider.Now();

        var timeSpanToLatestTimeToReachMinSoc = TimeSpan.FromMinutes(60);
        var timeSpanToReachMinSoCAtFullSpeedCharge = TimeSpan.FromMinutes(58);

        var car = CreateDemoCar(chargeMode, currentTime + timeSpanToLatestTimeToReachMinSoc, soc, 60);
        chargingService.EnableFullSpeedChargeIfMinimumSocNotReachable(car, currentTime + timeSpanToReachMinSoCAtFullSpeedCharge);

        Assert.False(car.CarState.AutoFullSpeedCharge);
    }

    private Car CreateDemoCar(ChargeMode chargeMode, DateTime latestTimeToReachSoC, int soC, int minimumSoC)
    {
        var car = new Car()
        {
            CarState = new CarState()
            {
                AutoFullSpeedCharge = false,
                SoC = soC,
            },
            CarConfiguration = new CarConfiguration()
            {
                LatestTimeToReachSoC = latestTimeToReachSoC,
                MinimumSoC = minimumSoC,
                ChargeMode = chargeMode,
            },
        };
        return car;
    }
}