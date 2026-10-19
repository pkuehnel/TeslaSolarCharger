using Autofac;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PkSoftwareService.Custom.Backend.Ble;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
using TeslaSolarCharger.SharedBackend.Dtos;
using TeslaSolarCharger.SharedBackend.Enums;
using Xunit;
using FleetApiService = TeslaSolarCharger.Server.Services.TeslaFleetApiService;

namespace TeslaSolarCharger.Tests.Services.Server;

/// <summary>
/// How <see cref="FleetApiService"/> keeps Fleet API requests within the Solar4Car backend's command budget, so the
/// backend's own rate limits are never reached in normal operation, and how it handles the backend rejecting one anyway.
/// </summary>
[SuppressMessage("ReSharper", "UseConfigureAwaitFalse")]
public class BackendRateLimitHandlingTests(ITestOutputHelper outputHelper) : FleetApiFallbackTestBase(outputHelper)
{
    private const string WakeUpThrottleMessage = "Can not allow request wake for car LRW3E7FS2NC000001 as the next wake up is only allowed in 900 seconds.";
    private const string HourlyLimitMessage = "The car LRW3E7FS2NC000001 has no Car License, so only one successful command per hour is allowed.";

    [Fact]
    public async Task BudgetAllowsCommand_CommandIsSent()
    {
        var car = SetupBleCarWithFailingBle(isFleetApiLicensed: false);
        SetupBudget(new DtoFleetApiCommandBudget());
        SetupBackendResponse(Constants.SetChargingAmpsRequestUrl, Successful());

        await CreateService().SetAmp(car.Id, 10);

        VerifyBackendCalled(Constants.SetChargingAmpsRequestUrl, Times.Once());
        VerifyRateLimitIssueRaised(Times.Never());
    }

    [Fact]
    public async Task CommandsBlocked_CommandIsNotSentAndIssueRaised()
    {
        var car = SetupBleCarWithFailingBle(isFleetApiLicensed: false);
        SetupBudget(new DtoFleetApiCommandBudget { NextCommandInSeconds = 1800, });

        var result = await CreateService().SendCommandToTeslaApi<DtoVehicleCommandResult>(Vin, ChargeStopRequest());

        Assert.Equal(FleetApiService.FleetApiCommandRateLimitedError, result!.Error);
        VerifyBackendCalled(Constants.ChargeStopRequestUrl, Times.Never());
        VerifyRateLimitIssueRaised(Times.Once());
    }

    [Theory]
    [InlineData(31, true)]
    [InlineData(30, false)]
    public async Task WithinGraceWindow_CommandIsOnlySentOutsideSafetyMargin(int graceRemainingSeconds, bool expectedSent)
    {
        var car = SetupBleCarWithFailingBle(isFleetApiLicensed: false);
        SetupBudget(new DtoFleetApiCommandBudget { GraceRemainingSeconds = graceRemainingSeconds, NextCommandInSeconds = 3500, });
        SetupBackendResponse(Constants.SetChargingAmpsRequestUrl, Successful());

        await CreateService().SetAmp(car.Id, 10);

        VerifyBackendCalled(Constants.SetChargingAmpsRequestUrl, expectedSent ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task KnownBlock_FollowingCommandsDoNotAskBackendAgain()
    {
        var car = SetupBleCarWithFailingBle(isFleetApiLicensed: false);
        SetupBudget(new DtoFleetApiCommandBudget { NextCommandInSeconds = 1800, });
        var service = CreateService();

        await service.SetAmp(car.Id, 10);
        await service.StopCharging(car.Id);

        Mock.Mock<IBackendApiService>().Verify(b => b.GetFleetApiCommandBudget(Vin), Times.Once);
        VerifyBackendCalled(Constants.SetChargingAmpsRequestUrl, Times.Never());
        VerifyBackendCalled(Constants.ChargeStopRequestUrl, Times.Never());
    }

    [Fact]
    public async Task LicensedCar_CommandIsSentWithoutAskingForBudget()
    {
        var car = SetupBleCarWithFailingBle(isFleetApiLicensed: true);
        SetupBackendResponse(Constants.SetChargingAmpsRequestUrl, Successful());

        await CreateService().SetAmp(car.Id, 10);

        Mock.Mock<IBackendApiService>().Verify(b => b.GetFleetApiCommandBudget(It.IsAny<string>()), Times.Never);
        VerifyBackendCalled(Constants.SetChargingAmpsRequestUrl, Times.Once());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WakeUpBlocked_WakeUpIsNotSentAndNoIssueRaised(bool isFleetApiLicensed)
    {
        SetupBleCarWithFailingBle(isFleetApiLicensed);
        SetupBudget(new DtoFleetApiCommandBudget { NextWakeUpInSeconds = 900, });
        var service = CreateService();

        var result = await service.SendCommandToTeslaApi<DtoVehicleWakeUpResult>(Vin, service.WakeUpRequest);

        Assert.Equal(FleetApiService.WakeUpThrottledError, result!.Error);
        Mock.Mock<IBackendApiService>().Verify(b => b.GetFleetApiCommandBudget(Vin), Times.Once);
        VerifyBackendCalled(Constants.WakeUpRequestUrl, Times.Never());
        VerifyRateLimitIssueRaised(Times.Never());
    }

    [Fact]
    public async Task WakeUpOfUnlicensedCarWithoutCommandBudget_IsReportedAsRateLimitedCommand()
    {
        SetupBleCarWithFailingBle(isFleetApiLicensed: false);
        SetupBudget(new DtoFleetApiCommandBudget { NextCommandInSeconds = 1800, });
        var service = CreateService();

        var result = await service.SendCommandToTeslaApi<DtoVehicleWakeUpResult>(Vin, service.WakeUpRequest);

        Assert.Equal(FleetApiService.FleetApiCommandRateLimitedError, result!.Error);
        VerifyBackendCalled(Constants.WakeUpRequestUrl, Times.Never());
        VerifyRateLimitIssueRaised(Times.Once());
    }

    [Fact]
    public async Task SupportPageCommandOfUnlicensedCar_IsWithinBudget()
    {
        //The support page forces the Fleet API and skips the license check, but not the command budget.
        var car = SetupBleCarWithFailingBle(isFleetApiLicensed: false);
        SetupBudget(new DtoFleetApiCommandBudget { NextCommandInSeconds = 1800, });

        var result = await CreateService().SetChargingAmps(car.Id, 10);

        Assert.Equal(FleetApiService.FleetApiCommandRateLimitedError, result!.Error);
        VerifyBackendCalled(Constants.SetChargingAmpsRequestUrl, Times.Never());
    }

    [Fact]
    public async Task BudgetUnavailable_CommandIsNotSent()
    {
        SetupBleCarWithFailingBle(isFleetApiLicensed: false);
        Mock.Mock<IBackendApiService>().Setup(b => b.GetFleetApiCommandBudget(Vin))
            .ReturnsAsync(new Result<DtoFleetApiCommandBudget>(null, "Backend down", null));

        var result = await CreateService().SendCommandToTeslaApi<DtoVehicleCommandResult>(Vin, ChargeStopRequest());

        Assert.Equal(FleetApiService.FleetApiCommandBudgetUnavailableError, result!.Error);
        Assert.Equal("Backend down", result.ErrorDescription);
        VerifyBackendCalled(Constants.ChargeStopRequestUrl, Times.Never());
        Mock.Mock<IErrorHandlingService>().Verify(
            e => e.HandleError(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                IssueKeys.Solar4CarSideFleetApiNonSuccessStatusCode + Constants.ChargeStopRequestUrl, Vin, It.IsAny<string?>()),
            Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BackendWakeUpRejection_IsReportedAsWakeUpThrottled(bool isFleetApiLicensed)
    {
        SetupBleCarWithFailingBle(isFleetApiLicensed);
        SetupBudget(new DtoFleetApiCommandBudget());
        SetupBackendResponse(Constants.WakeUpRequestUrl, RateLimited(WakeUpThrottleMessage));
        var service = CreateService();

        var result = await service.SendCommandToTeslaApi<DtoVehicleWakeUpResult>(Vin, service.WakeUpRequest);

        Assert.Equal(FleetApiService.WakeUpThrottledError, result!.Error);
        Assert.Equal(WakeUpThrottleMessage, result.ErrorDescription);
        VerifyRateLimitIssueRaised(Times.Never());
    }

    [Fact]
    public async Task BackendRejectionOfCommand_IsReportedAndNextCommandAsksBudgetAgain()
    {
        //The budget was used up between asking for it and sending, e.g. by another installation for the same car.
        var car = SetupBleCarWithFailingBle(isFleetApiLicensed: false);
        SetupBudget(new DtoFleetApiCommandBudget());
        SetupBackendResponse(Constants.SetChargingAmpsRequestUrl, RateLimited(HourlyLimitMessage));
        var service = CreateService();

        await service.SetAmp(car.Id, 10);
        SetupBudget(new DtoFleetApiCommandBudget { NextCommandInSeconds = 1700, });
        await service.StopCharging(car.Id);

        VerifyRateLimitIssueRaised(Times.Exactly(2));
        Mock.Mock<IBackendApiService>().Verify(b => b.GetFleetApiCommandBudget(Vin), Times.Exactly(2));
        VerifyBackendCalled(Constants.ChargeStopRequestUrl, Times.Never());
    }

    [Fact]
    public async Task BackendErrorOtherThan429OnWakeUp_IsNotReportedAsWakeUpThrottled()
    {
        SetupBleCarWithFailingBle(isFleetApiLicensed: false);
        SetupBudget(new DtoFleetApiCommandBudget());
        SetupBackendResponse(Constants.WakeUpRequestUrl,
            new(null, "Backend down", new ProblemDetails { Status = (int)HttpStatusCode.InternalServerError, Detail = "Backend down", }));
        var service = CreateService();

        var result = await service.SendCommandToTeslaApi<DtoVehicleWakeUpResult>(Vin, service.WakeUpRequest);

        Assert.Equal("Solar4CarBackendRequestFailed", result!.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FleetApiTest_Succeeds_UsesTestEndpointWithLongTimeout(bool isFleetApiLicensed)
    {
        var car = SetupBleCarWithFailingBle(isFleetApiLicensed);
        SetupBudget(new DtoFleetApiCommandBudget());
        SetupTestResponse(Successful());

        var result = await CreateService().TestFleetApiAccess(car.Id);

        Assert.True(result.Value);
        Mock.Mock<IBackendApiService>().Verify(b => b.SendRequestToBackend<DtoBackendApiTeslaResponse>(HttpMethod.Post, AccessToken,
            It.Is<string>(uri => uri.StartsWith(Constants.FleetApiTestRequestUrl + "?")), null, TimeSpan.FromSeconds(60)), Times.Once);
        //The test replaces the former wake up and set amps commands, which used up the command budget.
        VerifyBackendCalled(Constants.WakeUpRequestUrl, Times.Never());
        VerifyBackendCalled(Constants.SetChargingAmpsRequestUrl, Times.Never());
        Assert.Equal(TeslaCarFleetApiState.Ok, await GetFleetApiState());
    }

    [Fact]
    public async Task FleetApiTest_CarRejectsCommand_Fails()
    {
        var car = SetupBleCarWithFailingBle(isFleetApiLicensed: false);
        SetupBudget(new DtoFleetApiCommandBudget());
        SetupTestResponse(TeslaResponse(FailedCommandJson));

        var result = await CreateService().TestFleetApiAccess(car.Id);

        Assert.False(result.Value);
        Assert.Equal(TeslaCarFleetApiState.NotWorking, await GetFleetApiState());
    }

    [Fact]
    public async Task FleetApiTest_TestedSuccessfullyWithinLastMinute_SucceedsWithoutTesting()
    {
        //Only successful tests limit further tests, so a blocked test proves the car accepted one a moment ago.
        var car = SetupBleCarWithFailingBle(isFleetApiLicensed: false);
        SetupBudget(new DtoFleetApiCommandBudget { NextFleetApiTestInSeconds = 40, NextCommandInSeconds = 1800, });

        var result = await CreateService().TestFleetApiAccess(car.Id);

        Assert.True(result.Value);
        Mock.Mock<IBackendApiService>().Verify(b => b.SendRequestToBackend<DtoBackendApiTeslaResponse>(It.IsAny<HttpMethod>(),
            It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<TimeSpan>()), Times.Never);
        Assert.Equal(TeslaCarFleetApiState.Ok, await GetFleetApiState());
    }

    [Fact]
    public async Task FleetApiTest_BackendRejectsAsRateLimited_Succeeds()
    {
        var car = SetupBleCarWithFailingBle(isFleetApiLicensed: false);
        SetupBudget(new DtoFleetApiCommandBudget());
        SetupTestResponse(RateLimited("Only one successful Tesla cloud access test per minute is allowed."));

        var result = await CreateService().TestFleetApiAccess(car.Id);

        Assert.True(result.Value);
        VerifyRateLimitIssueRaised(Times.Never());
    }

    private DtoFleetApiRequest ChargeStopRequest() => new()
    {
        RequestUrl = Constants.ChargeStopRequestUrl,
        BleCompatible = true,
        TeslaApiRequestType = TeslaApiRequestType.Charging,
    };
}
