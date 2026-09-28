using Autofac;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PkSoftwareService.Custom.Backend.Ble;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.Solar4CarBackend;
using TeslaSolarCharger.Server.Dtos.TeslaFleetApi;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.Shared.TimeProviding;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

/// <summary>
/// How <see cref="TeslaSolarCharger.Server.Services.TeslaFleetApiService"/> handles the Solar4Car backend rejecting a
/// Fleet API fallback command with 429: the hourly command limit of cars without Fleet API license blocks further
/// commands, the backend's own wake up throttle must not.
/// </summary>
[SuppressMessage("ReSharper", "UseConfigureAwaitFalse")]
public class BackendRateLimitHandlingTests(ITestOutputHelper outputHelper) : TestBase(outputHelper)
{
    private const string Vin = "LRW3E7FS2NC000001";
    private const string AccessToken = "backendAccessToken";
    private const string EncryptionKey = "encryptionKey";
    private const string WakeUpThrottleMessage = "Can not allow request wake for car LRW3E7FS2NC000001 as last try was 02.02.2023 07:45:00 +00:00";
    private const string HourlyLimitMessage = "The car LRW3E7FS2NC000001 has no Fleet API license, so only one successful command per hour is allowed.";
    private const string SuccessfulCommandJson = "{\"response\":{\"result\":true,\"reason\":\"\"}}";

    private IConstants Constants => Mock.Create<IConstants>();
    private IIssueKeys IssueKeys => Mock.Create<IIssueKeys>();
    //One clock for TeslaFleetApiService and the real FleetApiRateLimitService, so both agree on the current time.
    private FakeDateTimeProvider Clock => new(CurrentFakeDate.UtcDateTime);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BackendWakeUpRejection_IsReportedAsWakeUpThrottled(bool isFleetApiLicensed)
    {
        SetupBleCarWithFailingBle(isFleetApiLicensed);
        SetupBackendResponse(Constants.WakeUpRequestUrl, RateLimited(WakeUpThrottleMessage));
        var service = CreateService();

        var result = await service.SendCommandToTeslaApi<DtoVehicleWakeUpResult>(Vin, service.WakeUpRequest);

        Assert.NotNull(result);
        Assert.Equal(TeslaSolarCharger.Server.Services.TeslaFleetApiService.WakeUpThrottledError, result.Error);
        Assert.Equal(WakeUpThrottleMessage, result.ErrorDescription);
        Mock.Mock<IFleetApiRateLimitService>().Verify(r => r.RecordRateLimited(It.IsAny<DtoCar>()), Times.Never);
        VerifyRateLimitIssueRaised(Times.Never());
    }

    [Fact]
    public async Task BackendWakeUpRejection_DoesNotBlockFollowingCommands()
    {
        var car = SetupBleCarWithFailingBle(isFleetApiLicensed: false);
        SetupBackendResponse(Constants.WakeUpRequestUrl, RateLimited(WakeUpThrottleMessage));
        SetupBackendResponse(Constants.SetChargingAmpsRequestUrl, Successful());
        var service = CreateServiceWithRealRateLimit();

        await service.SendCommandToTeslaApi<DtoVehicleWakeUpResult>(Vin, service.WakeUpRequest);
        await service.SetAmp(car.Id, 10);

        VerifyBackendCalled(Constants.SetChargingAmpsRequestUrl, Times.Once());
        //The set amps command is the first counted command of the hour, not a block anchored before it.
        Assert.Equal(Clock.UtcNow(), car.LastCountedFleetApiCommand);
        VerifyRateLimitIssueRaised(Times.Never());
    }

    [Fact]
    public async Task BackendRateLimitOnNonWakeUpCommand_BlocksFollowingCommands()
    {
        var car = SetupBleCarWithFailingBle(isFleetApiLicensed: false);
        SetupBackendResponse(Constants.SetChargingAmpsRequestUrl, RateLimited(HourlyLimitMessage));
        var service = CreateServiceWithRealRateLimit();

        await service.SetAmp(car.Id, 10);
        await service.StopCharging(car.Id);

        VerifyBackendCalled(Constants.SetChargingAmpsRequestUrl, Times.Once());
        VerifyBackendCalled(Constants.ChargeStopRequestUrl, Times.Never());
        Assert.NotNull(car.LastCountedFleetApiCommand);
        //Once for the backend rejection, once for the charge stop that was not sent.
        VerifyRateLimitIssueRaised(Times.Exactly(2));
    }

    [Fact]
    public async Task HourlyLimitHitByWakeUp_IsRecordedByNextCommand()
    {
        var car = SetupBleCarWithFailingBle(isFleetApiLicensed: false);
        SetupBackendResponse(Constants.WakeUpRequestUrl, RateLimited(HourlyLimitMessage));
        SetupBackendResponse(Constants.SetChargingAmpsRequestUrl, RateLimited(HourlyLimitMessage));
        var service = CreateServiceWithRealRateLimit();

        await service.SendCommandToTeslaApi<DtoVehicleWakeUpResult>(Vin, service.WakeUpRequest);
        await service.SetAmp(car.Id, 10);
        await service.StopCharging(car.Id);
        var secondWakeUp = await service.SendCommandToTeslaApi<DtoVehicleWakeUpResult>(Vin, service.WakeUpRequest);

        VerifyBackendCalled(Constants.WakeUpRequestUrl, Times.Once());
        VerifyBackendCalled(Constants.SetChargingAmpsRequestUrl, Times.Once());
        VerifyBackendCalled(Constants.ChargeStopRequestUrl, Times.Never());
        Assert.NotNull(secondWakeUp);
        Assert.Equal("FleetApiCommandRateLimited", secondWakeUp.Error);
    }

    [Fact]
    public async Task BackendErrorOtherThan429OnWakeUp_IsNotReportedAsWakeUpThrottled()
    {
        SetupBleCarWithFailingBle(isFleetApiLicensed: false);
        SetupBackendResponse(Constants.WakeUpRequestUrl,
            new(null, "Backend down", new ProblemDetails { Status = (int)HttpStatusCode.InternalServerError, Detail = "Backend down", }));
        var service = CreateService();

        var result = await service.SendCommandToTeslaApi<DtoVehicleWakeUpResult>(Vin, service.WakeUpRequest);

        Assert.NotNull(result);
        Assert.Equal("Solar4CarBackendRequestFailed", result.Error);
        Mock.Mock<IFleetApiRateLimitService>().Verify(r => r.RecordRateLimited(It.IsAny<DtoCar>()), Times.Never);
    }

    /// <summary>
    /// A BLE car whose BLE commands all fail, so every command falls back to the Fleet API.
    /// </summary>
    private DtoCar SetupBleCarWithFailingBle(bool isFleetApiLicensed)
    {
        Context.Cars.Add(new Car
        {
            Id = 1,
            Vin = Vin,
            CarType = CarType.Tesla,
            ShouldBeManaged = true,
            UseBle = true,
            UseFleetTelemetry = false,
        });
        Context.BackendTokens.Add(new BackendToken(AccessToken, "backendRefreshToken") { ExpiresAtUtc = CurrentFakeDate.AddHours(1), });
        Context.SaveChangesAsync().GetAwaiter().GetResult();
        DetachAllEntities();

        var car = new DtoCar
        {
            Id = 1,
            Vin = Vin,
            UseBle = true,
        };
        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar> { car });
        Mock.Mock<ITokenHelper>().Setup(t => t.GetBackendTokenState(true)).ReturnsAsync(TokenState.UpToDate);
        Mock.Mock<IBackendApiService>().Setup(b => b.IsBaseAppLicensed(true)).ReturnsAsync(new Result<bool?>(true, null, null));
        Mock.Mock<IBackendApiService>().Setup(b => b.IsFleetApiLicensed(Vin, true)).ReturnsAsync(isFleetApiLicensed);
        Mock.Mock<ITscConfigurationService>().Setup(c => c.GetConfigurationValueByKey(Constants.TeslaTokenEncryptionKeyKey)).ReturnsAsync(EncryptionKey);

        var bleFailure = new DtoBleCommandResult
        {
            Success = false,
            Outcome = BleCommandOutcome.CarAbsent,
            ResultMessage = "Car not in range",
        };
        Mock.Mock<IBleService>().Setup(b => b.WakeUpCar(Vin)).ReturnsAsync(bleFailure);
        Mock.Mock<IBleService>().Setup(b => b.SetAmp(Vin, It.IsAny<int>())).ReturnsAsync(bleFailure);
        Mock.Mock<IBleService>().Setup(b => b.StopCharging(Vin)).ReturnsAsync(bleFailure);
        return car;
    }

    /// <summary>
    /// Service with an auto mocked <see cref="IFleetApiRateLimitService"/>, which never blocks a command.
    /// </summary>
    private TeslaSolarCharger.Server.Services.TeslaFleetApiService CreateService() =>
        Mock.Create<TeslaSolarCharger.Server.Services.TeslaFleetApiService>(
            new TypedParameter(typeof(IDateTimeProvider), Clock));

    /// <summary>
    /// Service with the real <see cref="FleetApiRateLimitService"/>, so a recorded rate limit blocks later commands.
    /// </summary>
    private TeslaSolarCharger.Server.Services.TeslaFleetApiService CreateServiceWithRealRateLimit()
    {
        var rateLimitService = new FleetApiRateLimitService(NullLogger<FleetApiRateLimitService>.Instance, Clock);
        return Mock.Create<TeslaSolarCharger.Server.Services.TeslaFleetApiService>(
            new TypedParameter(typeof(IDateTimeProvider), Clock),
            new TypedParameter(typeof(IFleetApiRateLimitService), rateLimitService));
    }

    private static Result<DtoBackendApiTeslaResponse> RateLimited(string message) =>
        new(null, message, new ProblemDetails { Status = (int)HttpStatusCode.TooManyRequests, Detail = message, });

    private static Result<DtoBackendApiTeslaResponse> Successful() =>
        new(new() { StatusCode = HttpStatusCode.OK, JsonResponse = SuccessfulCommandJson, }, null, null);

    private void SetupBackendResponse(string requestUrl, Result<DtoBackendApiTeslaResponse> response)
    {
        Mock.Mock<IBackendApiService>()
            .Setup(b => b.SendRequestToBackend<DtoBackendApiTeslaResponse>(HttpMethod.Post, AccessToken,
                It.Is<string>(uri => uri.StartsWith(requestUrl + "?")), null))
            .ReturnsAsync(response);
    }

    private void VerifyBackendCalled(string requestUrl, Times times)
    {
        Mock.Mock<IBackendApiService>().Verify(
            b => b.SendRequestToBackend<DtoBackendApiTeslaResponse>(HttpMethod.Post, AccessToken,
                It.Is<string>(uri => uri.StartsWith(requestUrl + "?")), null),
            times);
    }

    private void VerifyRateLimitIssueRaised(Times times)
    {
        Mock.Mock<IErrorHandlingService>().Verify(
            e => e.HandleError(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                IssueKeys.FleetApiCommandRateLimited, Vin, It.IsAny<string?>()),
            times);
    }
}
