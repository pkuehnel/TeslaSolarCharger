using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.Setup;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources;
using TeslaSolarCharger.Shared.TimeProviding;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server.Setup;

public class SetupStateServiceTests
{
    private readonly Mock<ITscConfigurationService> _tscConfigurationService = new();
    private readonly Mock<IConfigurationWrapper> _configurationWrapper = new();
    private readonly Mock<IConfigJsonService> _configJsonService = new();
    private readonly Constants _constants = new();

    /// <summary>What the configuration table currently holds for the setup key.</summary>
    private string? _storedJson;

    private DtoBaseConfiguration _liveConfiguration = new();
    private List<CarBasicConfiguration> _existingCars = new();

    public SetupStateServiceTests()
    {
        _tscConfigurationService
            .Setup(s => s.GetConfigurationValueByKey(_constants.SetupCacheKey))
            .ReturnsAsync(() => _storedJson);
        _tscConfigurationService
            .Setup(s => s.SetConfigurationValueByKey(_constants.SetupCacheKey, It.IsAny<string>()))
            .Callback<string, string>((_, value) => _storedJson = value)
            .Returns(Task.CompletedTask);
        _configurationWrapper.Setup(w => w.GetBaseConfigurationAsync()).ReturnsAsync(() => _liveConfiguration);
        _configJsonService.Setup(s => s.GetCarBasicConfigurations(null)).ReturnsAsync(() => _existingCars);
    }

    private SetupStateService NewService() => new(
        Mock.Of<ILogger<SetupStateService>>(),
        _tscConfigurationService.Object,
        new SetupStateMigrator(Mock.Of<ILogger<SetupStateMigrator>>()),
        _configurationWrapper.Object,
        _configJsonService.Object,
        new FakeDateTimeProvider(new DateTime(2026, 9, 15, 8, 0, 0, DateTimeKind.Utc)),
        _constants);

    [Fact]
    public async Task NothingStored_MeansNoSetupInProgress()
    {
        Assert.Null(await NewService().GetSetupState());
    }

    [Fact]
    public async Task AStoredStateIsReturned()
    {
        _storedJson = JsonConvert.SerializeObject(new DtoSetupState { CurrentStep = SetupStepKey.Prices, });

        var state = await NewService().GetSetupState();

        Assert.Equal(SetupStepKey.Prices, state!.CurrentStep);
    }

    [Fact]
    public async Task AVersion1StateIsMigratedAndWrittenBackOnce()
    {
        _storedJson = JsonConvert.SerializeObject(new DtoSetupCache { CurrentStep = 4, HasPvSystem = true, });

        var state = await NewService().GetSetupState();

        Assert.Equal(SetupStepKey.Prices, state!.CurrentStep);
        //Written back in the new shape, so the migration does not have to run again on the next read.
        Assert.Contains($"\"{nameof(DtoSetupState.SchemaVersion)}\":{DtoSetupState.CurrentSchemaVersion}", _storedJson);
    }

    [Fact]
    public async Task AnUnreadableStoredStateDoesNotBlockTheAssistant()
    {
        _storedJson = "{broken";

        var state = await NewService().GetOrCreateSetupState();

        Assert.NotNull(state);
        Assert.Equal(SetupStepKey.Welcome, state.CurrentStep);
    }

    [Fact]
    public async Task AFreshStateStartsFromWhatIsAlreadyConfigured()
    {
        _liveConfiguration = new DtoBaseConfiguration { HomeGeofenceRadius = 250, PredictSolarPowerGeneration = true, };

        var state = await NewService().GetOrCreateSetupState();

        Assert.Equal(250, state.Configuration.HomeGeofenceRadius);
        Assert.True(state.Configuration.PredictSolarPowerGeneration);
    }

    [Fact]
    public async Task AnExistingInstallationsChoicesAreNotUpForGrabs()
    {
        var state = await NewService().GetOrCreateSetupState();

        //Everything the assistant could propose is marked as already decided by this installation, so a proposal
        //never quietly replaces a setting the user made in Base Configuration.
        foreach (var propertyName in SetupConfigurationOwnership.OwnedProperties)
        {
            Assert.Equal(SetupValueSource.ExistingConfiguration, state.ValueSources[propertyName]);
        }
    }

    [Fact]
    public async Task ExistingCarsBecomeDraftsSoTheyContinueIntoConfiguration()
    {
        _existingCars = new List<CarBasicConfiguration>
        {
            new(3, "Imported Tesla") { Vin = "VIN3", CarType = CarType.Tesla, UseBle = true, ShouldBeManaged = false, },
        };

        var state = await NewService().GetOrCreateSetupState();

        var draft = Assert.Single(state.CarDrafts);
        Assert.Equal(3, draft.CarId);
        Assert.Equal("Imported Tesla", draft.Configuration.Name);
        Assert.Equal(SetupCarConnectionRoute.TeslaBluetooth, draft.ConnectionRoute);
    }

    [Theory]
    [InlineData(CarType.Tesla, true, false, SetupCarConnectionRoute.TeslaBluetooth)]
    [InlineData(CarType.Tesla, false, true, SetupCarConnectionRoute.TeslaCloud)]
    [InlineData(CarType.Tesla, false, false, SetupCarConnectionRoute.Undecided)]
    [InlineData(CarType.SmartCar, false, false, SetupCarConnectionRoute.SmartCarWithChargingStation)]
    [InlineData(CarType.Manual, false, false, SetupCarConnectionRoute.ChargingStationOnly)]
    public async Task AnExistingCarsRouteIsReadBackInsteadOfAskedAgain(CarType carType,
        bool useBle,
        bool useFleetTelemetry,
        SetupCarConnectionRoute expected)
    {
        _existingCars = new List<CarBasicConfiguration>
        {
            new(1, "Car") { Vin = "VIN1", CarType = carType, UseBle = useBle, UseFleetTelemetry = useFleetTelemetry, },
        };

        var state = await NewService().GetOrCreateSetupState();

        Assert.Equal(expected, Assert.Single(state.CarDrafts).ConnectionRoute);
    }

    [Fact]
    public async Task AnAlreadyManagedCarIsNotProposedForActivationAgain()
    {
        _existingCars = new List<CarBasicConfiguration>
        {
            new(1, "Running car") { Vin = "VIN1", CarType = CarType.Tesla, UseBle = true, ShouldBeManaged = true, },
        };

        var state = await NewService().GetOrCreateSetupState();

        Assert.True(Assert.Single(state.CarDrafts).ShouldBeActivated);
    }

    [Fact]
    public async Task FailingToListTheCarsStillOpensTheAssistant()
    {
        _configJsonService.Setup(s => s.GetCarBasicConfigurations(null)).ThrowsAsync(new InvalidOperationException("db down"));

        var state = await NewService().GetOrCreateSetupState();

        Assert.NotNull(state);
        Assert.Empty(state.CarDrafts);
    }

    [Fact]
    public async Task ACarThatArrivedFromAnImportJoinsTheSetup()
    {
        var state = new DtoSetupState();
        _existingCars = new List<CarBasicConfiguration>
        {
            new(9, "Imported") { Vin = "VIN9", CarType = CarType.Tesla, UseFleetTelemetry = true, },
        };

        var synced = await NewService().SyncCarDrafts(state);

        var draft = Assert.Single(synced.CarDrafts);
        Assert.Equal(9, draft.CarId);
        Assert.Equal(SetupCarConnectionRoute.TeslaCloud, draft.ConnectionRoute);
    }

    [Fact]
    public async Task SyncingDoesNotDisturbACarTheUserIsAlreadyWorkingOn()
    {
        var existingDraft = new DtoSetupCarDraft
        {
            CarId = 9,
            Stage = SetupCarStage.ChargingSettings,
            Configuration = new CarBasicConfiguration(9, "Imported") { Vin = "VIN9", },
        };
        var state = new DtoSetupState { CarDrafts = { existingDraft, }, };
        _existingCars = new List<CarBasicConfiguration> { new(9, "Imported") { Vin = "VIN9", }, };

        var synced = await NewService().SyncCarDrafts(state);

        var draft = Assert.Single(synced.CarDrafts);
        Assert.Equal(SetupCarStage.ChargingSettings, draft.Stage);
    }

    [Fact]
    public async Task ADraftWhoseCarWasDeletedStopsAskingToBeFinished()
    {
        var state = new DtoSetupState
        {
            CarDrafts = { new DtoSetupCarDraft { CarId = 9, Configuration = new CarBasicConfiguration(9, "Gone"), }, },
        };
        _existingCars = new List<CarBasicConfiguration>();

        var synced = await NewService().SyncCarDrafts(state);

        Assert.Empty(synced.CarDrafts);
    }

    [Fact]
    public async Task ACarBeingBuiltInSetupSurvivesASync()
    {
        //A draft with no car id has not been saved yet; it exists only in this setup and must not be swept away.
        var state = new DtoSetupState { CarDrafts = { new DtoSetupCarDraft(), }, };
        _existingCars = new List<CarBasicConfiguration>();

        var synced = await NewService().SyncCarDrafts(state);

        Assert.Single(synced.CarDrafts);
    }

    [Fact]
    public async Task AnImportedCarStartsWhereThereIsSomethingToDo()
    {
        //It already has a name and an identification number, so asking who it is again would waste a screen.
        _existingCars = new List<CarBasicConfiguration>
        {
            new(3, "Known") { Vin = "VIN3", CarType = CarType.Tesla, },
        };

        var state = await NewService().GetOrCreateSetupState();

        Assert.Equal(SetupCarStage.Connection, Assert.Single(state.CarDrafts).Stage);
    }

    [Fact]
    public async Task ACarWithoutANameStartsByBeingIdentified()
    {
        _existingCars = new List<CarBasicConfiguration> { new(3, null) { Vin = "VIN3", }, };

        var state = await NewService().GetOrCreateSetupState();

        Assert.Equal(SetupCarStage.Identify, Assert.Single(state.CarDrafts).Stage);
    }

    [Fact]
    public async Task SavingStampsTheSchemaVersionAndTheTime()
    {
        var state = new DtoSetupState { SchemaVersion = 1, };

        await NewService().UpdateSetupState(state);

        Assert.Equal(DtoSetupState.CurrentSchemaVersion, state.SchemaVersion);
        Assert.NotNull(state.LastSavedAt);
    }

    [Fact]
    public async Task DeletingClearsTheStoredState()
    {
        _storedJson = JsonConvert.SerializeObject(new DtoSetupState());

        await NewService().DeleteSetupState();

        Assert.Equal(string.Empty, _storedJson);
        Assert.Null(await NewService().GetSetupState());
    }

    [Fact]
    public async Task AStateInProgressIsNotReplacedByAFreshOne()
    {
        _storedJson = JsonConvert.SerializeObject(new DtoSetupState
        {
            CurrentStep = SetupStepKey.CarsAndCharging,
            HasHomeBattery = true,
        });

        var state = await NewService().GetOrCreateSetupState();

        Assert.Equal(SetupStepKey.CarsAndCharging, state.CurrentStep);
        Assert.True(state.HasHomeBattery);
        Assert.Empty(state.CarDrafts);
    }
}
