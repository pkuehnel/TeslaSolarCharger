using Moq;
using PkSoftwareService.Custom.Backend.Ble;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.Solar4CarBackend;
using TeslaSolarCharger.Server.Dtos.TeslaFleetApi;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

/// <summary>
/// The charging commands of a BLE car whose commands fall back to the Fleet API: the one minute repeat guard of charge
/// start and stop, the wake up throttle and when the last set current is remembered.
/// </summary>
[SuppressMessage("ReSharper", "UseConfigureAwaitFalse")]
public class FleetApiFallbackCommandTests(ITestOutputHelper outputHelper) : FleetApiFallbackTestBase(outputHelper)
{
    private const string OnlineVehicleJson = "{\"response\":{\"state\":\"online\"}}";

    [Theory]
    [InlineData(null, true)]
    [InlineData(61, true)]
    [InlineData(60, true)]
    [InlineData(59, false)]
    [InlineData(1, false)]
    public async Task StopCharging_IsOnlySentIfLastStopIsAtLeastOneMinuteOld(int? lastStopSecondsAgo, bool expectedSent)
    {
        var car = SetupBleCarWithFailingBle(isFleetApiLicensed: true);
        if (lastStopSecondsAgo is { } secondsAgo)
        {
            car.ChargeStopCalls.Add(Clock.UtcNow().AddSeconds(-secondsAgo));
        }
        SetupBackendResponse(Constants.ChargeStopRequestUrl, Successful());

        await CreateService().StopCharging(car.Id);

        var times = expectedSent ? Times.Once() : Times.Never();
        Mock.Mock<IBleService>().Verify(b => b.StopCharging(Vin), times);
        VerifyBackendCalled(Constants.ChargeStopRequestUrl, times);
    }

    [Fact]
    public async Task StartCharging_LastStartWithinLastMinute_SendsNothing()
    {
        var car = SetupBleCarWithFailingBle(isFleetApiLicensed: true);
        car.ChargeStartCalls.Add(Clock.UtcNow().AddSeconds(-30));

        await CreateService().StartCharging(car.Id, 10);

        Mock.Mock<IBleService>().Verify(b => b.StartCharging(It.IsAny<string>()), Times.Never);
        Mock.Mock<IBleService>().Verify(b => b.SetAmp(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        VerifyBackendCalled(Constants.VehicleRequestUrl, Times.Never());
        VerifyBackendCalled(Constants.ChargeStartRequestUrl, Times.Never());
    }

    [Fact]
    public async Task StartCharging_LastStartOlderThanOneMinute_IsSent()
    {
        var car = SetupBleCarWithFailingBle(isFleetApiLicensed: true);
        car.ChargeStartCalls.Add(Clock.UtcNow().AddMinutes(-2));
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.GetVehicleDataFromTesla()).Returns(true);
        SetupBackendResponse(Constants.VehicleRequestUrl, TeslaResponse(OnlineVehicleJson));
        SetupBackendResponse(Constants.SetChargingAmpsRequestUrl, Successful());
        SetupBackendResponse(Constants.ChargeStartRequestUrl, Successful());

        await CreateService().StartCharging(car.Id, 10);

        VerifyBackendCalled(Constants.SetChargingAmpsRequestUrl, Times.Once());
        VerifyBackendCalled(Constants.ChargeStartRequestUrl, Times.Once());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecentBleWakeUp_DoesNotBlockFleetApiWakeUp(bool isFleetApiLicensed)
    {
        //Only the backend's wake up budget throttles Fleet API wake ups. A BLE wake up shortly before BLE broke must
        //not keep the fallback from waking the car.
        var car = SetupBleCarWithFailingBle(isFleetApiLicensed);
        car.WakeUpCalls.Add(Clock.UtcNow().AddMinutes(-10));
        SetupBudget(new DtoFleetApiCommandBudget());
        SetupBackendResponse(Constants.WakeUpRequestUrl, TeslaResponse(OnlineVehicleJson));
        var service = CreateService();

        var result = await service.SendCommandToTeslaApi<DtoVehicleWakeUpResult>(Vin, service.WakeUpRequest);

        Assert.Null(result!.Error);
        VerifyBackendCalled(Constants.WakeUpRequestUrl, Times.Once());
        Assert.Equal(2, car.WakeUpCalls.Count);
    }

    [Fact]
    public async Task SetAmp_FleetApiCommandSucceeds_RemembersSetCurrent()
    {
        var car = SetupBleCarWithFailingBle(isFleetApiLicensed: true);
        SetupBackendResponse(Constants.SetChargingAmpsRequestUrl, Successful());

        await CreateService().SetAmp(car.Id, 10);

        Assert.Equal(10, car.LastSetAmp.Value);
        Assert.Equal(Clock.DateTimeOffSetUtcNow(), car.LastSetAmp.LastChanged);
    }

    [Fact]
    public async Task SetAmp_BleCommandSucceeds_RemembersSetCurrent()
    {
        var car = SetupBleCarWithFailingBle(isFleetApiLicensed: true);
        Mock.Mock<IBleService>().Setup(b => b.SetAmp(Vin, 10)).ReturnsAsync(new DtoBleCommandResult { Success = true, });

        await CreateService().SetAmp(car.Id, 10);

        Assert.Equal(10, car.LastSetAmp.Value);
        VerifyBackendCalled(Constants.SetChargingAmpsRequestUrl, Times.Never());
    }

    public static TheoryData<Result<DtoBackendApiTeslaResponse>> FailedSetAmpResponses => new()
    {
        TeslaResponse(FailedCommandJson),
        new Result<DtoBackendApiTeslaResponse>(new() { StatusCode = System.Net.HttpStatusCode.InternalServerError, JsonResponse = "{}", }, null, null),
        new Result<DtoBackendApiTeslaResponse>(null, "Backend down", null),
    };

    [Theory]
    [MemberData(nameof(FailedSetAmpResponses))]
    public async Task SetAmp_CommandFails_KeepsLastSetCurrent(Result<DtoBackendApiTeslaResponse> response)
    {
        var car = SetupBleCarWithFailingBle(isFleetApiLicensed: true);
        car.LastSetAmp.Update(Clock.DateTimeOffSetUtcNow().AddMinutes(-5), 6);
        SetupBackendResponse(Constants.SetChargingAmpsRequestUrl, response);

        await CreateService().SetAmp(car.Id, 10);

        Assert.Equal(6, car.LastSetAmp.Value);
        Assert.Equal(Clock.DateTimeOffSetUtcNow().AddMinutes(-5), car.LastSetAmp.LastChanged);
    }

    [Fact]
    public async Task SetAmp_CommandRateLimited_KeepsLastSetCurrent()
    {
        var car = SetupBleCarWithFailingBle(isFleetApiLicensed: false);
        car.LastSetAmp.Update(Clock.DateTimeOffSetUtcNow().AddMinutes(-5), 6);
        SetupBudget(new DtoFleetApiCommandBudget { NextCommandInSeconds = 1800, });

        await CreateService().SetAmp(car.Id, 10);

        Assert.Equal(6, car.LastSetAmp.Value);
    }
}
