using Autofac;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PkSoftwareService.Custom.Backend.Ble;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.Solar4CarBackend;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.Shared.TimeProviding;
using TeslaSolarCharger.SharedBackend.Dtos;
using TeslaSolarCharger.SharedBackend.Enums;
using Xunit;
using FleetApiService = TeslaSolarCharger.Server.Services.TeslaFleetApiService;

namespace TeslaSolarCharger.Tests.Services.Server;

/// <summary>
/// A BLE car whose BLE commands all fail, so every command falls back to the Fleet API via the mocked Solar4Car backend.
/// </summary>
public abstract class FleetApiFallbackTestBase(ITestOutputHelper outputHelper) : TestBase(outputHelper)
{
    protected const string Vin = "LRW3E7FS2NC000001";
    protected const string AccessToken = "backendAccessToken";
    private const string EncryptionKey = "encryptionKey";
    protected const string SuccessfulCommandJson = "{\"response\":{\"result\":true,\"reason\":\"\"}}";
    protected const string FailedCommandJson = "{\"response\":{\"result\":false,\"reason\":\"vehicle rejected\"}}";

    protected IConstants Constants => Mock.Create<IConstants>();
    protected IIssueKeys IssueKeys => Mock.Create<IIssueKeys>();
    //One clock for TeslaFleetApiService and FleetApiRateLimitService, so both agree on the current time.
    protected FakeDateTimeProvider Clock => new(CurrentFakeDate.UtcDateTime);

    protected DtoCar SetupBleCarWithFailingBle(bool isFleetApiLicensed)
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
        Mock.Mock<IBleService>().Setup(b => b.StartCharging(Vin)).ReturnsAsync(bleFailure);
        Mock.Mock<IBleService>().Setup(b => b.StopCharging(Vin)).ReturnsAsync(bleFailure);
        return car;
    }

    /// <summary>
    /// Service with the real <see cref="FleetApiRateLimitService"/> asking the mocked backend for the budget.
    /// </summary>
    protected FleetApiService CreateService()
    {
        var rateLimitService = new FleetApiRateLimitService(NullLogger<FleetApiRateLimitService>.Instance, Clock,
            Mock.Mock<IBackendApiService>().Object);
        return Mock.Create<FleetApiService>(
            new TypedParameter(typeof(IDateTimeProvider), Clock),
            new TypedParameter(typeof(IFleetApiRateLimitService), rateLimitService));
    }

    protected static Result<DtoBackendApiTeslaResponse> RateLimited(string message) =>
        new(null, message, new ProblemDetails { Status = (int)HttpStatusCode.TooManyRequests, Detail = message, });

    protected static Result<DtoBackendApiTeslaResponse> Successful() => TeslaResponse(SuccessfulCommandJson);

    protected static Result<DtoBackendApiTeslaResponse> TeslaResponse(string jsonResponse) =>
        new(new() { StatusCode = HttpStatusCode.OK, JsonResponse = jsonResponse, }, null, null);

    protected void SetupBudget(DtoFleetApiCommandBudget budget) =>
        Mock.Mock<IBackendApiService>().Setup(b => b.GetFleetApiCommandBudget(Vin))
            .ReturnsAsync(new Result<DtoFleetApiCommandBudget>(budget, null, null));

    protected void SetupBackendResponse(string requestUrl, Result<DtoBackendApiTeslaResponse> response)
    {
        Mock.Mock<IBackendApiService>()
            .Setup(b => b.SendRequestToBackend<DtoBackendApiTeslaResponse>(HttpMethod.Post, AccessToken,
                It.Is<string>(uri => uri.StartsWith(requestUrl + "?")), null))
            .ReturnsAsync(response);
    }

    protected void SetupTestResponse(Result<DtoBackendApiTeslaResponse> response)
    {
        Mock.Mock<IBackendApiService>()
            .Setup(b => b.SendRequestToBackend<DtoBackendApiTeslaResponse>(HttpMethod.Post, AccessToken,
                It.Is<string>(uri => uri.StartsWith(Constants.FleetApiTestRequestUrl + "?")), null, It.IsAny<System.TimeSpan>()))
            .ReturnsAsync(response);
    }

    protected void VerifyBackendCalled(string requestUrl, Times times)
    {
        Mock.Mock<IBackendApiService>().Verify(
            b => b.SendRequestToBackend<DtoBackendApiTeslaResponse>(HttpMethod.Post, AccessToken,
                It.Is<string>(uri => uri.StartsWith(requestUrl + "?")), null),
            times);
    }

    protected void VerifyRateLimitIssueRaised(Times times)
    {
        Mock.Mock<IErrorHandlingService>().Verify(
            e => e.HandleError(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                IssueKeys.FleetApiCommandRateLimited, Vin, It.IsAny<string?>()),
            times);
    }

    protected async Task<TeslaCarFleetApiState?> GetFleetApiState() =>
        await Context.Cars.AsNoTracking().Where(c => c.Id == 1).Select(c => c.TeslaFleetApiState).FirstAsync();
}
