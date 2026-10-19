using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.Solar4CarBackend;
using TeslaSolarCharger.Server.Enums;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.TimeProviding;
using Xunit;
using RateLimitService = TeslaSolarCharger.Server.Services.FleetApiRateLimitService;

namespace TeslaSolarCharger.Tests.Services.Server;

public class FleetApiRateLimitServiceTests
{
    private const string Vin = "LRW3E7FS2NC000001";
    //Winter date so no DST change can occur within the tested time ranges
    private static readonly DateTime BaseTime = new(2026, 2, 2, 8, 0, 0);
    private static readonly DateTime Now = new FakeDateTimeProvider(BaseTime).UtcNow();
    private static readonly FleetApiBudgetKind[] CommandsKind = [FleetApiBudgetKind.Commands];

    private readonly Mock<IBackendApiService> _backendApiService = new();

    [Fact]
    public void ToBlocks_NothingLimited_BlocksNothing()
    {
        var blocks = RateLimitService.ToBlocks(new DtoFleetApiCommandBudget(), Now);

        Assert.Equal(new DtoFleetApiBudgetBlocks(), blocks);
    }

    [Fact]
    public void ToBlocks_WithinGraceWindow_DoesNotBlockCommands()
    {
        var blocks = RateLimitService.ToBlocks(new DtoFleetApiCommandBudget { GraceRemainingSeconds = 31, NextCommandInSeconds = 3500, }, Now);

        Assert.Null(blocks.CommandsBlockedUntil);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(1)]
    [InlineData(null)]
    public void ToBlocks_GraceWindowWithinSafetyMarginOrOver_BlocksCommandsUntilWindowEndPlusMargin(int? graceRemainingSeconds)
    {
        var blocks = RateLimitService.ToBlocks(new DtoFleetApiCommandBudget { GraceRemainingSeconds = graceRemainingSeconds, NextCommandInSeconds = 1800, }, Now);

        Assert.Equal(Now.AddSeconds(1800) + RateLimitService.SafetyMargin, blocks.CommandsBlockedUntil);
    }

    [Fact]
    public void ToBlocks_WakeUpAndTestLimits_BlockUntilEndPlusMargin()
    {
        var blocks = RateLimitService.ToBlocks(new DtoFleetApiCommandBudget { NextWakeUpInSeconds = 600, NextFleetApiTestInSeconds = 40, }, Now);

        Assert.Null(blocks.CommandsBlockedUntil);
        Assert.Equal(Now.AddSeconds(600) + RateLimitService.SafetyMargin, blocks.WakeUpBlockedUntil);
        Assert.Equal(Now.AddSeconds(40) + RateLimitService.SafetyMargin, blocks.FleetApiTestBlockedUntil);
    }

    [Fact]
    public async Task GetBlocks_NothingKnown_AsksBackendAndRemembersBlocks()
    {
        var car = new DtoCar { Vin = Vin, };
        SetupBudget(new DtoFleetApiCommandBudget { NextCommandInSeconds = 1800, });

        var result = await CreateService().GetBlocks(car, CommandsKind);

        Assert.False(result.HasError);
        Assert.Equal(Now.AddSeconds(1800) + RateLimitService.SafetyMargin, result.Data!.CommandsBlockedUntil);
        Assert.Equal(result.Data, car.FleetApiBudgetBlocks);
    }

    [Fact]
    public async Task GetBlocks_AllowedBudget_AsksBackendEveryTime()
    {
        //Anything allowed can be used up by the next command, so only blocks are remembered.
        var car = new DtoCar { Vin = Vin, };
        SetupBudget(new DtoFleetApiCommandBudget());
        var service = CreateService();

        await service.GetBlocks(car, CommandsKind);
        await service.GetBlocks(car, CommandsKind);

        _backendApiService.Verify(b => b.GetFleetApiCommandBudget(Vin), Times.Exactly(2));
    }

    [Fact]
    public async Task GetBlocks_RunningBlockOfRequestedKind_DoesNotAskBackend()
    {
        var knownBlocks = new DtoFleetApiBudgetBlocks(CommandsBlockedUntil: Now.AddMinutes(10));
        var car = new DtoCar { Vin = Vin, FleetApiBudgetBlocks = knownBlocks, };

        var result = await CreateService().GetBlocks(car, [FleetApiBudgetKind.Commands, FleetApiBudgetKind.WakeUp,]);

        Assert.Equal(knownBlocks, result.Data);
        _backendApiService.Verify(b => b.GetFleetApiCommandBudget(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetBlocks_RunningBlockOfOtherKind_AsksBackend()
    {
        var car = new DtoCar { Vin = Vin, FleetApiBudgetBlocks = new(WakeUpBlockedUntil: Now.AddMinutes(10)), };
        SetupBudget(new DtoFleetApiCommandBudget { NextWakeUpInSeconds = 300, });

        var result = await CreateService().GetBlocks(car, CommandsKind);

        _backendApiService.Verify(b => b.GetFleetApiCommandBudget(Vin), Times.Once);
        Assert.Equal(Now.AddSeconds(300) + RateLimitService.SafetyMargin, result.Data!.WakeUpBlockedUntil);
    }

    [Fact]
    public async Task GetBlocks_BlockIsOver_AsksBackendAndReturnsNoBlock()
    {
        var car = new DtoCar { Vin = Vin, FleetApiBudgetBlocks = new(CommandsBlockedUntil: Now), };
        SetupBudget(new DtoFleetApiCommandBudget());

        var result = await CreateService().GetBlocks(car, CommandsKind);

        _backendApiService.Verify(b => b.GetFleetApiCommandBudget(Vin), Times.Once);
        Assert.Null(result.Data!.CommandsBlockedUntil);
    }

    [Fact]
    public async Task GetBlocks_BackendError_ReturnsErrorAndKeepsKnownBlocks()
    {
        var knownBlocks = new DtoFleetApiBudgetBlocks(WakeUpBlockedUntil: Now.AddMinutes(10));
        var car = new DtoCar { Vin = Vin, FleetApiBudgetBlocks = knownBlocks, };
        var problemDetails = new ProblemDetails { Status = 403, Detail = "Not your car", };
        _backendApiService.Setup(b => b.GetFleetApiCommandBudget(Vin))
            .ReturnsAsync(new Result<DtoFleetApiCommandBudget>(null, "Not your car", problemDetails));

        var result = await CreateService().GetBlocks(car, CommandsKind);

        Assert.True(result.HasError);
        Assert.Equal("Not your car", result.ErrorMessage);
        Assert.Same(problemDetails, result.ProblemDetails);
        Assert.Equal(knownBlocks, car.FleetApiBudgetBlocks);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void IsKnownToBeBlocked_OnlyWhileBlockRuns(int blockEndsInSeconds, bool expected)
    {
        var car = new DtoCar { Vin = Vin, FleetApiBudgetBlocks = new(CommandsBlockedUntil: Now.AddSeconds(blockEndsInSeconds)), };

        Assert.Equal(expected, CreateService().IsKnownToBeBlocked(car, FleetApiBudgetKind.Commands));
        Assert.False(CreateService().IsKnownToBeBlocked(car, FleetApiBudgetKind.WakeUp));
        _backendApiService.Verify(b => b.GetFleetApiCommandBudget(It.IsAny<string>()), Times.Never);
    }

    private RateLimitService CreateService() =>
        new(NullLogger<RateLimitService>.Instance, new FakeDateTimeProvider(BaseTime), _backendApiService.Object);

    private void SetupBudget(DtoFleetApiCommandBudget budget) =>
        _backendApiService.Setup(b => b.GetFleetApiCommandBudget(Vin))
            .ReturnsAsync(new Result<DtoFleetApiCommandBudget>(budget, null, null));
}
