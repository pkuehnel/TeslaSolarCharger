using System;
using Microsoft.Extensions.Logging;
using Moq;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class BlePresenceStateServiceTests
{
    private const int CarId = 1;
    private const int OtherCarId = 2;
    private const int Threshold = BlePresenceStateService.ConsecutiveOutOfRangeResultsToConfirmAway;

    private static IBlePresenceStateService NewService()
        => new BlePresenceStateService(Mock.Of<ILogger<BlePresenceStateService>>());

    [Fact]
    public void ConfirmsAwayExactlyOnceOnReachingTheThreshold()
    {
        var service = NewService();
        for (var i = 1; i < Threshold; i++)
        {
            Assert.Equal(BleAwayConfirmation.NotConfirmed, service.RegisterOutOfRange(CarId));
        }
        Assert.Equal(BleAwayConfirmation.JustConfirmed, service.RegisterOutOfRange(CarId));
        //Further out of range results while the car stays away must not re-fire the away transition.
        Assert.Equal(BleAwayConfirmation.AlreadyConfirmed, service.RegisterOutOfRange(CarId));
        Assert.Equal(BleAwayConfirmation.AlreadyConfirmed, service.RegisterOutOfRange(CarId));
    }

    [Fact]
    public void PresenceIsUncertainOnlyBetweenFirstFailureAndAwayConfirmation()
    {
        var service = NewService();
        Assert.False(service.IsPresenceUncertain(CarId));
        for (var i = 1; i < Threshold; i++)
        {
            service.RegisterOutOfRange(CarId);
            Assert.True(service.IsPresenceUncertain(CarId));
        }
        //Once the car is confirmed away the presence is certain again: the car is not at home.
        service.RegisterOutOfRange(CarId);
        Assert.False(service.IsPresenceUncertain(CarId));
    }

    [Fact]
    public void SuccessfulReadResetsTheCounter()
    {
        var service = NewService();
        for (var i = 1; i < Threshold; i++)
        {
            service.RegisterOutOfRange(CarId);
        }
        service.RegisterSuccessfulRead(CarId);
        Assert.False(service.IsPresenceUncertain(CarId));
        //After a successful read the full number of consecutive failures is required again.
        for (var i = 1; i < Threshold; i++)
        {
            Assert.Equal(BleAwayConfirmation.NotConfirmed, service.RegisterOutOfRange(CarId));
        }
        Assert.Equal(BleAwayConfirmation.JustConfirmed, service.RegisterOutOfRange(CarId));
    }

    [Fact]
    public void ResetClearsTheState()
    {
        var service = NewService();
        service.RegisterOutOfRange(CarId);
        Assert.True(service.IsPresenceUncertain(CarId));
        service.Reset(CarId);
        Assert.False(service.IsPresenceUncertain(CarId));
    }

    [Fact]
    public void RetainOnlyDropsStateOfCarsNoLongerBlePolled()
    {
        var service = NewService();
        service.RegisterOutOfRange(CarId);
        service.RegisterOutOfRange(OtherCarId);
        service.RetainOnly(new[] { OtherCarId });
        //The dropped car must not keep a stale uncertain state that would suppress its charging commands.
        Assert.False(service.IsPresenceUncertain(CarId));
        Assert.True(service.IsPresenceUncertain(OtherCarId));
        service.RetainOnly(Array.Empty<int>());
        Assert.False(service.IsPresenceUncertain(OtherCarId));
    }

    [Fact]
    public void CarsAreTrackedIndependently()
    {
        var service = NewService();
        for (var i = 1; i <= Threshold; i++)
        {
            service.RegisterOutOfRange(CarId);
        }
        Assert.False(service.IsPresenceUncertain(CarId));
        Assert.Equal(BleAwayConfirmation.NotConfirmed, service.RegisterOutOfRange(OtherCarId));
        Assert.True(service.IsPresenceUncertain(OtherCarId));
    }
}
