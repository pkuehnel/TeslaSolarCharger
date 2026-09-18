using Moq;
using PkSoftwareService.Custom.Backend.Ble;
using System;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Shared.Contracts;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

/// <summary>
/// Decides whether a car is talked to over BLE at all. Getting this wrong is expensive in both directions: too eager
/// and a car that rejects TSC's key is connected to every few seconds forever on a radio every car shares, too shy
/// and a car that works is left unread.
/// </summary>
public class BleAccessGateServiceTests : TestBase
{
    private const int CarId = 1;
    private const string Container = "http://ble-container:7210";
    private static readonly DateTimeOffset Start = new(2026, 9, 18, 20, 0, 0, TimeSpan.Zero);

    public BleAccessGateServiceTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public void CarIsAskedUntilItRejectsTheKey()
    {
        var service = CreateService(Start);

        Assert.False(service.IsKeyRejected(CarId));

        service.RegisterCommandResult(CarId, KeyRejection());

        Assert.True(service.IsKeyRejected(CarId));
    }

    [Fact]
    public void AnsweringCarEndsThePause()
    {
        var service = CreateService(Start);
        service.RegisterCommandResult(CarId, KeyRejection());

        service.RegisterCommandResult(CarId, new DtoBleCommandResult { Success = true, Outcome = BleCommandOutcome.Ok, });

        Assert.False(service.IsKeyRejected(CarId));
    }

    /// <summary>
    /// A car out of range or a radio that hung up says nothing about the key, so it may neither start the pause nor
    /// end one: ending it would put the car straight back into the poll it was taken out of.
    /// </summary>
    [Theory]
    [InlineData(BleCommandOutcome.LinkFailed)]
    [InlineData(BleCommandOutcome.CarAbsent)]
    [InlineData(BleCommandOutcome.WorkerError)]
    [InlineData(null)]
    public void FailuresThatSayNothingAboutTheKeyChangeNothing(BleCommandOutcome? outcome)
    {
        var service = CreateService(Start);
        var failure = new DtoBleCommandResult { Success = false, Outcome = outcome, };

        service.RegisterCommandResult(CarId, failure);
        Assert.False(service.IsKeyRejected(CarId));

        service.RegisterCommandResult(CarId, KeyRejection());
        service.RegisterCommandResult(CarId, failure);
        Assert.True(service.IsKeyRejected(CarId));
    }

    [Fact]
    public void PausedCarIsAskedAgainAfterTheRetryInterval()
    {
        var now = Start;
        var service = CreateService(() => now);
        service.RegisterCommandResult(CarId, KeyRejection());

        now = Start + BleAccessGateService.KeyRejectionRetryInterval - TimeSpan.FromSeconds(1);
        Assert.True(service.IsKeyRejected(CarId));

        now = Start + BleAccessGateService.KeyRejectionRetryInterval;
        Assert.False(service.IsKeyRejected(CarId));
    }

    /// <summary>
    /// A car that still rejects the key must not be asked every few seconds from then on: the retry interval starts
    /// over on the rejection that the retry produced.
    /// </summary>
    [Fact]
    public void RejectionDuringARetryStartsTheIntervalOver()
    {
        var now = Start;
        var service = CreateService(() => now);
        service.RegisterCommandResult(CarId, KeyRejection());

        now = Start + BleAccessGateService.KeyRejectionRetryInterval;
        Assert.False(service.IsKeyRejected(CarId));
        service.RegisterCommandResult(CarId, KeyRejection());

        now += TimeSpan.FromMinutes(1);
        Assert.True(service.IsKeyRejected(CarId));
    }

    [Fact]
    public void ExplicitlyClearedCarIsAskedAgainRightAway()
    {
        var service = CreateService(Start);
        service.RegisterCommandResult(CarId, KeyRejection());

        service.ClearKeyRejection(CarId);

        Assert.False(service.IsKeyRejected(CarId));
    }

    [Fact]
    public void PairingPausesOnlyItsOwnContainer()
    {
        var service = CreateService(Start);

        using (service.BeginPairing(Container))
        {
            Assert.True(service.IsPairingInProgress(Container));
            Assert.False(service.IsPairingInProgress("http://other-container:7210"));
        }

        Assert.False(service.IsPairingInProgress(Container));
    }

    /// <summary>
    /// Two pairings at once on one container are unusual but possible (two cars, one antenna). The first one
    /// finishing must not put the reads back onto a radio the second one is still using.
    /// </summary>
    [Fact]
    public void ConcurrentPairingsKeepTheContainerPausedUntilTheLastOneIsDone()
    {
        var service = CreateService(Start);
        var first = service.BeginPairing(Container);
        var second = service.BeginPairing(Container);

        first.Dispose();
        Assert.True(service.IsPairingInProgress(Container));

        second.Dispose();
        Assert.False(service.IsPairingInProgress(Container));
    }

    [Fact]
    public void PairingScopeCanBeDisposedTwiceWithoutResumingTooEarly()
    {
        var service = CreateService(Start);
        var first = service.BeginPairing(Container);
        using var second = service.BeginPairing(Container);

        first.Dispose();
        first.Dispose();

        Assert.True(service.IsPairingInProgress(Container));
    }

    /// <summary>
    /// A car without a BLE url never reaches a container, but the pause must not throw on the way there either.
    /// </summary>
    [Fact]
    public void ContainerWithoutUrlIsHandledLikeAnyOther()
    {
        var service = CreateService(Start);

        using (service.BeginPairing(null))
        {
            Assert.True(service.IsPairingInProgress(null));
            Assert.False(service.IsPairingInProgress(Container));
        }

        Assert.False(service.IsPairingInProgress(null));
    }

    private static DtoBleCommandResult KeyRejection() => new()
    {
        Success = false,
        Outcome = BleCommandOutcome.KeyNotPaired,
        ResultMessage = "vehicle rejected request: your public key has not been paired with the vehicle",
    };

    private BleAccessGateService CreateService(DateTimeOffset now) => CreateService(() => now);

    private BleAccessGateService CreateService(Func<DateTimeOffset> now)
    {
        Mock.Mock<IDateTimeProvider>().Setup(d => d.DateTimeOffSetUtcNow()).Returns(now);
        return Mock.Create<BleAccessGateService>();
    }
}
