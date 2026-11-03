using System;
using TeslaSolarCharger.Shared.Dtos.Settings;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Shared;

/// <summary>
/// A car that refused a charge start because the charger supplies no power gets no further charge starts until
/// something changed that could let a new one succeed.
/// </summary>
public class DtoCarWaitingForChargerPowerTests
{
    private static readonly DateTimeOffset RefusedAt = new(2026, 11, 1, 2, 30, 0, TimeSpan.Zero);

    private static DtoCar CreateRefusedCar()
    {
        var car = new DtoCar { ChargeStartRefusedForMissingPower = RefusedAt, };
        car.PluggedIn.Update(RefusedAt.AddMinutes(-10), true);
        car.IsCharging.Update(RefusedAt.AddMinutes(-10), false);
        car.ChargerPilotCurrent.Update(RefusedAt.AddMinutes(-10), 5);
        return car;
    }

    [Fact]
    public void NoRefusal_IsNotWaiting()
    {
        var car = CreateRefusedCar();
        car.ChargeStartRefusedForMissingPower = null;

        Assert.False(car.IsWaitingForChargerPower);
    }

    [Fact]
    public void NothingChangedSinceRefusal_IsWaiting()
    {
        var car = CreateRefusedCar();
        //Values that arrive again unchanged must not end the wait.
        car.PluggedIn.Update(RefusedAt.AddMinutes(5), true);
        car.ChargerPilotCurrent.Update(RefusedAt.AddMinutes(5), 5);
        car.IsCharging.Update(RefusedAt.AddMinutes(5), false);

        Assert.True(car.IsWaitingForChargerPower);
    }

    [Fact]
    public void RefusalWithoutAnyCarValues_IsWaiting()
    {
        var car = new DtoCar { ChargeStartRefusedForMissingPower = RefusedAt, };

        Assert.True(car.IsWaitingForChargerPower);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(null)]
    public void PilotCurrentChangedAfterRefusal_IsNotWaiting(int? newPilotCurrent)
    {
        var car = CreateRefusedCar();
        car.ChargerPilotCurrent.Update(RefusedAt.AddSeconds(1), newPilotCurrent);

        Assert.False(car.IsWaitingForChargerPower);
    }

    [Fact]
    public void PilotCurrentChangedBeforeRefusal_IsWaiting()
    {
        var car = CreateRefusedCar();
        car.ChargerPilotCurrent.Update(RefusedAt.AddSeconds(-1), 16);

        Assert.True(car.IsWaitingForChargerPower);
    }

    [Fact]
    public void ReplugAfterRefusal_IsNotWaiting()
    {
        //Unplugging and plugging in again before TSC looks leaves PluggedIn true, but it changed in between.
        var car = CreateRefusedCar();
        car.PluggedIn.Update(RefusedAt.AddMinutes(1), false);
        car.PluggedIn.Update(RefusedAt.AddMinutes(2), true);

        Assert.False(car.IsWaitingForChargerPower);
    }

    [Fact]
    public void CarCharging_IsNotWaiting()
    {
        var car = CreateRefusedCar();
        car.IsCharging.Update(RefusedAt.AddMinutes(1), true);

        Assert.False(car.IsWaitingForChargerPower);
    }
}
