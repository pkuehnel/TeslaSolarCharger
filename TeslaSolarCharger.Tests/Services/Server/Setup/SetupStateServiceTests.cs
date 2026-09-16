using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
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

public class SetupStateServiceTests : TestBase
{
    private readonly Mock<ITscConfigurationService> _tscConfigurationService = new();
    private readonly Mock<IConfigurationWrapper> _configurationWrapper = new();
    private readonly Mock<IConfigJsonService> _configJsonService = new();
    private readonly Constants _constants = new();

    /// <summary>What the configuration table currently holds for the setup key.</summary>
    private string? _storedJson;

    //An installation that has been through setup before. The distinction matters: only there do the stored values
    //count as decisions the assistant must not overrule.
    private DtoBaseConfiguration _liveConfiguration = new() { IsFirstRun = false, };
    private List<CarBasicConfiguration> _existingCars = new();

    public SetupStateServiceTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
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
        Moq.Mock.Of<ILogger<SetupStateService>>(),
        _tscConfigurationService.Object,
        new SetupStateMigrator(Moq.Mock.Of<ILogger<SetupStateMigrator>>()),
        _configurationWrapper.Object,
        _configJsonService.Object,
        Context,
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
        //A setting this installation actually holds a value for is its decision, and a proposal must not quietly
        //replace it - including a switch left deliberately off.
        _liveConfiguration = new DtoBaseConfiguration
        {
            IsFirstRun = false,
            PredictSolarPowerGeneration = false,
            ShowEnergyDataOnHome = false,
        };

        var state = await NewService().GetOrCreateSetupState();

        Assert.Equal(SetupValueSource.ExistingConfiguration,
            state.ValueSources[nameof(BaseConfigurationBase.PredictSolarPowerGeneration)]);
        Assert.Equal(SetupValueSource.ExistingConfiguration,
            state.ValueSources[nameof(BaseConfigurationBase.ShowEnergyDataOnHome)]);
    }

    [Fact]
    public async Task ASettingThatWasNeverAnsweredStaysOpenToARecommendation()
    {
        //A feature that did not exist when this installation was set up has no stored answer at all. Reading that
        //as a decision would hide its recommendation forever.
        _liveConfiguration = new DtoBaseConfiguration { IsFirstRun = false, DynamicHomeBatteryMinSoc = null, };

        var state = await NewService().GetOrCreateSetupState();

        Assert.DoesNotContain(nameof(BaseConfigurationBase.DynamicHomeBatteryMinSoc), state.ValueSources.Keys);
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AnExistingCarRemembersWhetherItWasAlreadyRunning(bool isManaged)
    {
        _existingCars = new List<CarBasicConfiguration>
        {
            new(1, "Car") { Vin = "VIN1", CarType = CarType.Tesla, UseBle = true, ShouldBeManaged = isManaged, },
        };

        var state = await NewService().GetOrCreateSetupState();

        Assert.Equal(isManaged, Assert.Single(state.CarDrafts).WasManagedBeforeSetup);
    }

    [Fact]
    public async Task ACarTheUserTookOutOfSetupIsNotBroughtBackBySyncing()
    {
        //Every car in setup is switched on when it finishes. Bringing a removed car back - after importing the cars
        //of a Tesla account, for one - would switch on the car the user asked to leave alone.
        var state = new DtoSetupState { RemovedCarIds = { 9, }, };
        _existingCars = new List<CarBasicConfiguration>
        {
            new(9, "Left alone") { Vin = "VIN9", CarType = CarType.Tesla, UseFleetTelemetry = true, },
            new(10, "Newly imported") { Vin = "VIN10", CarType = CarType.Tesla, UseFleetTelemetry = true, },
        };

        var synced = await NewService().SyncCarDrafts(state);

        Assert.Equal(10, Assert.Single(synced.CarDrafts).CarId);
        Assert.Equal(new List<int> { 9, }, synced.RemovedCarIds);
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

    [Fact]
    public async Task AFirstRunsDefaultsAreNotMistakenForDecisions()
    {
        //Nothing has been configured yet, so nothing has been decided. Treating the untouched defaults as choices
        //would leave a beginner with no recommendations at all - the opposite of what the assistant is for.
        _liveConfiguration = new DtoBaseConfiguration { IsFirstRun = true, };

        var state = await NewService().GetOrCreateSetupState();

        Assert.Empty(state.ValueSources);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WhetherACarWasAlreadyRunningIsReadFromTheDatabase(bool isManaged)
    {
        //Not derived from the draft's configuration: a car the user adds in the assistant starts out saying it
        //should be managed, so deriving it would call every new car an already running one.
        _existingCars = new List<CarBasicConfiguration>
        {
            new(1, "Car") { Vin = "VIN1", CarType = CarType.Manual, ShouldBeManaged = isManaged, },
        };

        var state = await NewService().GetOrCreateSetupState();

        Assert.Equal(isManaged, Assert.Single(state.CarDrafts).WasManagedBeforeSetup);
    }

    [Fact]
    public async Task AnExistingCarsChargingConnectorsComeAlongWithItsDraft()
    {
        //Without them the draft describes the car as assigned to nothing, and finishing setup would take away
        //assignments the user made long before the assistant was opened.
        Context.OcppChargingStationConnectors.Add(new OcppChargingStationConnector("Connector 1") { Id = 7, OcppChargingStationId = 1, });
        Context.ChargingStationConnectorAllowedCars.Add(new ChargingStationConnectorAllowedCar { CarId = 4, OcppChargingStationConnectorId = 7, });
        await Context.SaveChangesAsync();
        _existingCars = new List<CarBasicConfiguration>
        {
            new(4, "Running car") { Vin = "VIN4", CarType = CarType.Manual, ShouldBeManaged = true, },
        };

        var state = await NewService().GetOrCreateSetupState();

        Assert.Equal(new[] { 7, }, Assert.Single(state.CarDrafts).AssignedChargingConnectorIds);
    }

    [Fact]
    public async Task AuthorizingSmartCarUpdatesTheDraftTheUserIsOn()
    {
        //The authorization happens outside the assistant and changes the car in the database. A draft that keeps
        //saying "manual" would keep offering to connect what is connected, and write the old type back on save.
        var state = new DtoSetupState
        {
            CarDrafts =
            {
                new DtoSetupCarDraft
                {
                    CarId = 8,
                    ConnectionRoute = SetupCarConnectionRoute.SmartCarWithChargingStation,
                    Configuration = new CarBasicConfiguration(8, "My car") { Vin = "VIN8", CarType = CarType.Manual, },
                },
            },
        };
        _existingCars = new List<CarBasicConfiguration>
        {
            new(8, "My car") { Vin = "VIN8", CarType = CarType.SmartCar, },
        };

        var synced = await NewService().SyncCarDrafts(state);

        Assert.Equal(CarType.SmartCar, Assert.Single(synced.CarDrafts).Configuration.CarType);
    }

    [Fact]
    public async Task SyncingDoesNotOverwriteAnswersTheUserTyped()
    {
        var state = new DtoSetupState
        {
            CarDrafts =
            {
                new DtoSetupCarDraft
                {
                    CarId = 8,
                    Make = "Kia",
                    Configuration = new CarBasicConfiguration(8, "My car")
                    {
                        Vin = "VIN8", CarType = CarType.Manual, UsableEnergy = 64, MaximumPhases = 3,
                    },
                },
            },
        };
        _existingCars = new List<CarBasicConfiguration>
        {
            new(8, "Renamed by import") { Vin = "VIN8", CarType = CarType.SmartCar, UsableEnergy = 0, },
        };

        var synced = await NewService().SyncCarDrafts(state);

        var draft = Assert.Single(synced.CarDrafts);
        //Only what something else owns is refreshed; the numbers and names the user entered are theirs.
        Assert.Equal(CarType.SmartCar, draft.Configuration.CarType);
        Assert.Equal(64, draft.Configuration.UsableEnergy);
        Assert.Equal("My car", draft.Configuration.Name);
        Assert.Equal("Kia", draft.Make);
    }
}
