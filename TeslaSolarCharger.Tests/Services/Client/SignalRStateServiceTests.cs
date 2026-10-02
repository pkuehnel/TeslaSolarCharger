using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging.Abstractions;
using TeslaSolarCharger.Client.Services;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;
using TeslaSolarCharger.Shared.Helper;
using TeslaSolarCharger.Shared.SignalRClients;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Client;

/// <summary>
/// What subscribers get when the connection comes back, e.g. after a phone had the app in the background.
/// No hub is running, so the service stays disconnected and only its own bookkeeping is exercised.
/// </summary>
public class SignalRStateServiceTests : IAsyncDisposable
{
    private readonly SignalRStateService _service = new(new UnreachableNavigationManager(),
        NullLogger<SignalRStateService>.Instance,
        new EntityKeyGenerationHelper());

    [Fact]
    public async Task ATriggerSubscriberIsCalledOnceWhenTheConnectionIsRestored()
    {
        //A trigger sent while disconnected is lost and never re-sent, so the subscriber must reload on its own.
        var calls = 0;
        await _service.SubscribeToTrigger(DataTypeConstants.DynamicHomeBatteryMinSocChangeTrigger, () => calls++);

        await _service.OnConnectionRestoredAsync();

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task EveryTriggerSubscriberOfEveryDataTypeAndEntityIsCalled()
    {
        var firstCalls = 0;
        var secondCalls = 0;
        var entityCalls = 0;
        await _service.SubscribeToTrigger(DataTypeConstants.DynamicHomeBatteryMinSocChangeTrigger, () => firstCalls++);
        await _service.SubscribeToTrigger(DataTypeConstants.DynamicHomeBatteryMinSocChangeTrigger, () => secondCalls++);
        await _service.SubscribeToTrigger("OtherTrigger", () => entityCalls++, "7");

        await _service.OnConnectionRestoredAsync();

        Assert.Equal(1, firstCalls);
        Assert.Equal(1, secondCalls);
        Assert.Equal(1, entityCalls);
    }

    [Fact]
    public async Task EachRestoredConnectionCallsTheSubscriberAgain()
    {
        var calls = 0;
        await _service.SubscribeToTrigger(DataTypeConstants.DynamicHomeBatteryMinSocChangeTrigger, () => calls++);

        await _service.OnConnectionRestoredAsync();
        await _service.OnConnectionRestoredAsync();

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ADisposedTriggerSubscriptionIsNotCalled()
    {
        var calls = 0;
        var subscription = await _service.SubscribeToTrigger(DataTypeConstants.DynamicHomeBatteryMinSocChangeTrigger, () => calls++);
        subscription.Dispose();

        await _service.OnConnectionRestoredAsync();

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task AFailingTriggerSubscriberDoesNotKeepTheOthersFromBeingCalled()
    {
        var calls = 0;
        await _service.SubscribeToTrigger(DataTypeConstants.DynamicHomeBatteryMinSocChangeTrigger,
            () => throw new InvalidOperationException("broken subscriber"));
        await _service.SubscribeToTrigger(DataTypeConstants.DynamicHomeBatteryMinSocChangeTrigger, () => calls++);
        var connectionStateChanges = 0;
        _service.OnConnectionStateChanged += () => connectionStateChanges++;

        await _service.OnConnectionRestoredAsync();

        Assert.Equal(1, calls);
        Assert.Equal(1, connectionStateChanges);
    }

    [Fact]
    public async Task WithoutTriggerSubscribersTheConnectionStateChangeIsStillRaised()
    {
        var connectionStateChanges = 0;
        _service.OnConnectionStateChanged += () => connectionStateChanges++;

        await _service.OnConnectionRestoredAsync();

        Assert.Equal(1, connectionStateChanges);
    }

    [Fact]
    public async Task AStateSubscriberIsNotCalledWithoutState()
    {
        //States are re-sent by the server and handed over on arrival; a restored connection alone has nothing to pass on.
        var calls = 0;
        await _service.Subscribe<DtoPvValues>(DataTypeConstants.PvValues, _ => calls++);

        await _service.OnConnectionRestoredAsync();

        Assert.Equal(0, calls);
    }

    public async ValueTask DisposeAsync()
    {
        await _service.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private sealed class UnreachableNavigationManager : NavigationManager
    {
        public UnreachableNavigationManager() => Initialize("http://127.0.0.1:1/", "http://127.0.0.1:1/");
    }
}
