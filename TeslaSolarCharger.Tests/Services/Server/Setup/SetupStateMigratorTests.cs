using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;
using TeslaSolarCharger.Shared.Dtos.Setup;
using TeslaSolarCharger.Shared.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server.Setup;

public class SetupStateMigratorTests
{
    private static SetupStateMigrator NewMigrator() => new(Mock.Of<ILogger<SetupStateMigrator>>());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NothingStored_ReturnsNull(string? persistedJson)
    {
        Assert.Null(NewMigrator().Migrate(persistedJson));
    }

    [Fact]
    public void UnreadableJson_ReturnsNullInsteadOfThrowing()
    {
        Assert.Null(NewMigrator().Migrate("{not json"));
    }

    [Theory]
    [InlineData(0, SetupStepKey.Welcome)]
    [InlineData(1, SetupStepKey.CloudConnection)]
    [InlineData(2, SetupStepKey.SolarAndBattery)]
    [InlineData(3, SetupStepKey.Location)]
    [InlineData(4, SetupStepKey.Prices)]
    [InlineData(5, SetupStepKey.CarsAndCharging)]
    [InlineData(6, SetupStepKey.Finish)]
    public void Version1StepIndex_BecomesTheStepItStoodFor(int storedIndex, SetupStepKey expected)
    {
        var legacy = new DtoSetupCache { CurrentStep = storedIndex, };

        var migrated = NewMigrator().Migrate(JsonConvert.SerializeObject(legacy));

        Assert.NotNull(migrated);
        Assert.Equal(expected, migrated!.CurrentStep);
    }

    [Fact]
    public void Version1StepIndexOutOfRange_FallsBackToTheStartRatherThanAnUnknownStep()
    {
        var legacy = new DtoSetupCache { CurrentStep = 42, };

        var migrated = NewMigrator().Migrate(JsonConvert.SerializeObject(legacy));

        Assert.Equal(SetupStepKey.Welcome, migrated!.CurrentStep);
    }

    [Fact]
    public void Version1Answers_SurviveTheMigration()
    {
        var legacy = new DtoSetupCache
        {
            CurrentStep = 4,
            CompletedSteps = new List<int> { 0, 1, 2, },
            HasPvSystem = true,
            HasHomeBattery = true,
            Configuration = new DtoBaseConfiguration { HomeGeofenceLatitude = 48.1, HomeGeofenceRadius = 75, },
            ChargePrice = new DtoChargePrice { GridPrice = 0.31m, },
            CarsChargingSetup = new DtoCarsChargingSetupAnswers { ElectricCarCount = 2, TeslaCount = 1, },
        };

        var migrated = NewMigrator().Migrate(JsonConvert.SerializeObject(legacy));

        Assert.NotNull(migrated);
        Assert.Equal(DtoSetupState.CurrentSchemaVersion, migrated!.SchemaVersion);
        Assert.Equal(SetupStepKey.Prices, migrated.CurrentStep);
        Assert.Equal(
            new[] { SetupStepKey.Welcome, SetupStepKey.CloudConnection, SetupStepKey.SolarAndBattery, },
            migrated.CompletedSteps);
        Assert.True(migrated.HasPvSystem);
        Assert.True(migrated.HasHomeBattery);
        Assert.Equal(48.1, migrated.Configuration.HomeGeofenceLatitude);
        Assert.Equal(75, migrated.Configuration.HomeGeofenceRadius);
        Assert.Equal(0.31m, migrated.ChargePrice!.GridPrice);
        Assert.Equal(2, migrated.CarsChargingSetup.ElectricCarCount);
        Assert.Equal(1, migrated.CarsChargingSetup.TeslaCount);
    }

    [Fact]
    public void Version1CompletedStepsOutOfRange_AreDroppedWithoutLosingTheValidOnes()
    {
        var legacy = new DtoSetupCache { CompletedSteps = new List<int> { 0, 99, 1, -1, }, };

        var migrated = NewMigrator().Migrate(JsonConvert.SerializeObject(legacy));

        Assert.Equal(new[] { SetupStepKey.Welcome, SetupStepKey.CloudConnection, }, migrated!.CompletedSteps);
    }

    [Fact]
    public void CurrentSchemaVersion_IsReturnedUnchanged()
    {
        var state = new DtoSetupState
        {
            CurrentStep = SetupStepKey.Location,
            HasPvSystem = false,
            HasHomeBattery = true,
            CarDrafts = { new DtoSetupCarDraft { ConnectionRoute = SetupCarConnectionRoute.TeslaBluetooth, }, },
        };

        var migrated = NewMigrator().Migrate(JsonConvert.SerializeObject(state));

        Assert.Equal(SetupStepKey.Location, migrated!.CurrentStep);
        Assert.False(migrated.HasPvSystem);
        Assert.True(migrated.HasHomeBattery);
        Assert.Single(migrated.CarDrafts);
        Assert.Equal(SetupCarConnectionRoute.TeslaBluetooth, migrated.CarDrafts[0].ConnectionRoute);
    }

    [Fact]
    public void StateFromANewerVersion_IsReadAsFarAsPossibleAndStampedDownToWhatWeUnderstand()
    {
        var state = new DtoSetupState { SchemaVersion = DtoSetupState.CurrentSchemaVersion + 5, CurrentStep = SetupStepKey.Prices, };

        var migrated = NewMigrator().Migrate(JsonConvert.SerializeObject(state));

        Assert.Equal(DtoSetupState.CurrentSchemaVersion, migrated!.SchemaVersion);
        Assert.Equal(SetupStepKey.Prices, migrated.CurrentStep);
    }

    [Fact]
    public void Version1WithoutAnswers_DoesNotInventThem()
    {
        //A version 1 state always carried both booleans, so the migrated state is allowed to keep them. What it
        //must not do is invent drafts or price entries that were never stored.
        var migrated = NewMigrator().Migrate(JsonConvert.SerializeObject(new DtoSetupCache()));

        Assert.Empty(migrated!.CarDrafts);
        Assert.Empty(migrated.ChargerDrafts);
        Assert.Null(migrated.ChargePrice);
        Assert.Empty(migrated.CompletedOperations);
    }
}
