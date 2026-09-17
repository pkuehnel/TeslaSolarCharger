using Microsoft.Extensions.Logging;
using Moq;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class BleReadCoordinatorTests
{
    private const int CarId = 1;
    private const int OtherCarId = 2;

    private static IBleReadCoordinator NewCoordinator()
        => new BleReadCoordinator(Mock.Of<ILogger<BleReadCoordinator>>());

    [Fact]
    public void OnlyOneReadPerCarAtATime()
    {
        var coordinator = NewCoordinator();
        Assert.True(coordinator.TryBeginRead(CarId));
        //The scheduled refresh and an on demand single car read must never talk to the same car at once.
        Assert.False(coordinator.TryBeginRead(CarId));
        coordinator.EndRead(CarId);
        Assert.True(coordinator.TryBeginRead(CarId));
    }

    [Fact]
    public void CarsAreCoordinatedIndependently()
    {
        var coordinator = NewCoordinator();
        Assert.True(coordinator.TryBeginRead(CarId));
        //A read in progress for one car must not block cars served by other adapters or containers.
        Assert.True(coordinator.TryBeginRead(OtherCarId));
    }

    [Fact]
    public void EndReadOfAnUnreadCarIsHarmless()
    {
        var coordinator = NewCoordinator();
        coordinator.EndRead(CarId);
        Assert.True(coordinator.TryBeginRead(CarId));
    }
}
