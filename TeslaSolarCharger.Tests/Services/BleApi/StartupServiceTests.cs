using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using TeslaSolarCharger.BleApi.InMemoryValues.Contracts;
using TeslaSolarCharger.BleApi.Services;
using TeslaSolarCharger.BleApi.Services.Contracts;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.BleApi;

public class StartupServiceTests
{
    private readonly Mock<IPairingService> _pairingService = new();

    private StartupService CreateService() => new(
        Mock.Of<ILogger<StartupService>>(),
        Mock.Of<ISettings>(),
        new ConfigurationBuilder().Build(),
        TimeProvider.System,
        Mock.Of<IHttpClientFactory>(),
        _pairingService.Object);

    [Fact]
    public async Task EnsureKeyPair_CreatesTheKeyPairIfMissing()
    {
        await CreateService().EnsureKeyPair();

        _pairingService.Verify(s => s.EnsureKeyPair(), Times.Once);
        //Only the explicit endpoint may replace a key the cars are paired with.
        _pairingService.Verify(s => s.GenerateKeyPair(), Times.Never);
    }

    [Fact]
    public async Task EnsureKeyPair_AFailureDoesNotStopTheContainerFromStarting()
    {
        _pairingService.Setup(s => s.EnsureKeyPair()).ThrowsAsync(new InvalidOperationException("Error generating private key"));

        var exception = await Record.ExceptionAsync(() => CreateService().EnsureKeyPair());

        Assert.Null(exception);
    }
}
