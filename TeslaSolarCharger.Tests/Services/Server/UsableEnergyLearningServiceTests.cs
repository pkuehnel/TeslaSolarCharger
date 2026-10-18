using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class UsableEnergyLearningServiceTests : TestBase
{
    private const int CarId = 1;
    private const string Vin = "LRW0000000000001";
    private const int ChargeLossPercent = 15;
    private static readonly DateTimeOffset HistoryStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = HistoryStart.AddDays(60);

    private readonly ConcurrentDictionary<int, int> _learnedCarUsableEnergies = new();
    private readonly List<DtoPvSourceValue> _sourceValues = new();
    private long _carCounterWs = 1_000;
    private int _nextChargingProcessId = 1;

    public UsableEnergyLearningServiceTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
        Mock.Mock<ISettings>().Setup(s => s.LearnedCarUsableEnergiesWh).Returns(_learnedCarUsableEnergies);
        Mock.Mock<ISettings>().SetupProperty(s => s.LearnedHomeBatteryChargingEnergyWh);
        Mock.Mock<ISettings>().SetupProperty(s => s.LearnedHomeBatteryDischargingEnergyWh);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.CarChargeLoss()).Returns(ChargeLossPercent);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.UseLearnedUsableEnergy()).Returns(true);
        Mock.Mock<IGenericValueService>().Setup(g => g.GetSourceValues(It.IsAny<bool>())).Returns(_sourceValues);
        Mock.Mock<IDateTimeProvider>().Setup(d => d.UtcNow()).Returns(Now.UtcDateTime);
        Mock.Mock<IDateTimeProvider>().Setup(d => d.DateTimeOffSetUtcNow()).Returns(Now);
    }

    private UsableEnergyLearningService CreateService() => Mock.Create<UsableEnergyLearningService>();

    #region Pure calculations

    [Theory]
    [InlineData(new double[] { 1, 2, 3, 4, 5, }, 25, 2)]
    [InlineData(new double[] { 1, 2, 3, 4, 5, }, 50, 3)]
    [InlineData(new double[] { 1, 2, 3, 4, 5, }, 75, 4)]
    [InlineData(new double[] { 1, 2, 3, 4, }, 50, 2.5)]
    [InlineData(new double[] { 1, 2, 3, 4, }, 25, 1.75)]
    [InlineData(new double[] { 7, }, 75, 7)]
    public void Percentile_InterpolatesLinearlyBetweenRanks(double[] sortedValues, double percentile, double expected)
    {
        Assert.Equal(expected, UsableEnergyLearningService.Percentile(sortedValues, percentile), 6);
    }

    [Fact]
    public void Estimate_WithoutSamples_IsNotReliable()
    {
        var estimate = UsableEnergyLearningService.Estimate(UsableEnergySampleKind.CarCharge, new List<double>());

        Assert.False(estimate.IsReliable);
        Assert.Null(estimate.MedianKwh);
        Assert.Equal(0, estimate.AcceptedSampleCount);
        Assert.Equal(UsableEnergyLearningService.RequiredSampleCount, estimate.RequiredSampleCount);
    }

    [Fact]
    public void Estimate_WithOneSampleTooFew_IsNotReliable()
    {
        var estimate = UsableEnergyLearningService.Estimate(UsableEnergySampleKind.CarCharge,
            Enumerable.Repeat(60_000d, UsableEnergyLearningService.RequiredSampleCount - 1).ToList());

        Assert.False(estimate.IsReliable);
        Assert.Equal(60, estimate.MedianKwh);
    }

    [Fact]
    public void Estimate_WithEnoughConsistentSamples_IsReliableMedian()
    {
        var estimate = UsableEnergyLearningService.Estimate(UsableEnergySampleKind.CarCharge,
            new List<double> { 61_000, 59_000, 60_000, 62_000, 58_000, });

        Assert.True(estimate.IsReliable);
        Assert.Equal(60, estimate.MedianKwh);
        // Q1 = 59, Q3 = 61 => 2 / 60 = 3.3 %
        Assert.Equal(3.3, estimate.SpreadPercent);
    }

    [Fact]
    public void Estimate_IgnoresSingleOutliers()
    {
        var estimate = UsableEnergyLearningService.Estimate(UsableEnergySampleKind.CarCharge,
            new List<double> { 60_000, 60_000, 60_000, 60_000, 60_000, 150_000, 20_000, });

        Assert.True(estimate.IsReliable);
        Assert.Equal(60, estimate.MedianKwh);
    }

    [Theory]
    // Q1 = 92.5, Q3 = 107.5 => 15 / 100 = 15 % is still allowed
    [InlineData(new double[] { 85_000, 92_500, 100_000, 107_500, 115_000, }, true)]
    // Q1 = 92, Q3 = 108 => 16 % is not
    [InlineData(new double[] { 84_000, 92_000, 100_000, 108_000, 116_000, }, false)]
    public void Estimate_RequiresTheMiddleHalfToLieCloseTogether(double[] values, bool expectedReliable)
    {
        var estimate = UsableEnergyLearningService.Estimate(UsableEnergySampleKind.HomeBatteryCharge, values);

        Assert.Equal(expectedReliable, estimate.IsReliable);
    }

    [Theory]
    // 34.5 kWh from the wall with 15 % loss are 30 kWh in the battery for 50 % => 60 kWh
    [InlineData(UsableEnergySampleKind.CarCharge, 20, 70, 34_500, 60_000)]
    // The home battery counter is used as is, a discharge counts by its absolute SoC difference
    [InlineData(UsableEnergySampleKind.HomeBatteryCharge, 20, 60, 4_000, 10_000)]
    [InlineData(UsableEnergySampleKind.HomeBatteryDischarge, 80, 30, 5_000, 10_000)]
    public void GetImpliedUsableEnergyWh_ScalesEnergyTo100Percent(UsableEnergySampleKind kind, int startSoc, int endSoc, int energyWh, double expectedWh)
    {
        var sample = new UsableEnergySample { Kind = kind, StartSoc = startSoc, EndSoc = endSoc, EnergyWh = energyWh, };

        Assert.Equal(expectedWh, UsableEnergyLearningService.GetImpliedUsableEnergyWh(sample, ChargeLossPercent)!.Value, 6);
    }

    [Fact]
    public void GetImpliedUsableEnergyWh_WithoutSocDifference_IsUnknown()
    {
        var sample = new UsableEnergySample { Kind = UsableEnergySampleKind.CarCharge, StartSoc = 50, EndSoc = 50, EnergyWh = 1_000, };

        Assert.Null(UsableEnergyLearningService.GetImpliedUsableEnergyWh(sample, ChargeLossPercent));
    }

    [Theory]
    [InlineData(UsableEnergySampleKind.CarCharge, 9_999, false)]
    [InlineData(UsableEnergySampleKind.CarCharge, 10_000, true)]
    [InlineData(UsableEnergySampleKind.CarCharge, 250_000, true)]
    [InlineData(UsableEnergySampleKind.CarCharge, 250_001, false)]
    [InlineData(UsableEnergySampleKind.HomeBatteryCharge, 999, false)]
    [InlineData(UsableEnergySampleKind.HomeBatteryCharge, 1_000, true)]
    [InlineData(UsableEnergySampleKind.HomeBatteryDischarge, 200_000, true)]
    [InlineData(UsableEnergySampleKind.HomeBatteryDischarge, 200_001, false)]
    public void IsPlausible_AcceptsOnlyRealisticBatterySizes(UsableEnergySampleKind kind, double impliedWh, bool expected)
    {
        Assert.Equal(expected, UsableEnergyLearningService.IsPlausible(kind, impliedWh));
    }

    [Fact]
    public void EvaluateSample_RejectsImplausibleCapacityButKeepsItsValueForTheUi()
    {
        var sample = new UsableEnergySample { Kind = UsableEnergySampleKind.CarCharge, StartSoc = 20, EndSoc = 70, EnergyWh = 1_150, };

        var evaluated = UsableEnergyLearningService.EvaluateSample(sample, ChargeLossPercent);

        Assert.Equal(UsableEnergySampleRejectionReason.ImplausibleCapacity, evaluated.RejectionReason);
        Assert.Equal(2, evaluated.ImpliedUsableEnergyKwh);
        Assert.Equal(1.15, evaluated.EnergyKwh);
    }

    [Fact]
    public void EvaluateSample_KeepsStoredRejectionAndHidesItsValue()
    {
        var sample = new UsableEnergySample
        {
            Kind = UsableEnergySampleKind.CarCharge, StartSoc = 20, EndSoc = 30, EnergyWh = 6_900,
            RejectionReason = UsableEnergySampleRejectionReason.SocDifferenceTooSmall,
        };

        var evaluated = UsableEnergyLearningService.EvaluateSample(sample, ChargeLossPercent);

        Assert.Equal(UsableEnergySampleRejectionReason.SocDifferenceTooSmall, evaluated.RejectionReason);
        Assert.Null(evaluated.ImpliedUsableEnergyKwh);
        // The energy was never measured for such a sample
        Assert.Null(evaluated.EnergyKwh);
    }

    [Fact]
    public void EvaluateSample_ShowsTheEnergyOfARunWithTooMuchOppositeFlow()
    {
        var sample = new UsableEnergySample
        {
            Kind = UsableEnergySampleKind.HomeBatteryCharge, StartSoc = 20, EndSoc = 60, EnergyWh = 3_600,
            RejectionReason = UsableEnergySampleRejectionReason.OppositeEnergyFlow,
        };

        var evaluated = UsableEnergyLearningService.EvaluateSample(sample, ChargeLossPercent);

        Assert.Equal(3.6, evaluated.EnergyKwh);
        Assert.Null(evaluated.ImpliedUsableEnergyKwh);
    }

    [Fact]
    public void SplitIntoRuns_WithoutReadings_HasNothing()
    {
        var (closedRuns, openRunStart) = UsableEnergyLearningService.SplitIntoRuns(new List<(DateTimeOffset, int)>());

        Assert.Empty(closedRuns);
        Assert.Null(openRunStart);
    }

    [Fact]
    public void SplitIntoRuns_OnlyRising_KeepsTheRunOpen()
    {
        var readings = Readings(20, 21, 22, 23);

        var (closedRuns, openRunStart) = UsableEnergyLearningService.SplitIntoRuns(readings);

        Assert.Empty(closedRuns);
        Assert.Equal(readings[0].Timestamp, openRunStart);
    }

    [Fact]
    public void SplitIntoRuns_ClosesARunWhereTheSocTurnsAround()
    {
        var readings = Readings(20, 21, 22, 21, 20, 21);

        var (closedRuns, openRunStart) = UsableEnergyLearningService.SplitIntoRuns(readings);

        Assert.Equal(2, closedRuns.Count);
        Assert.Equal(new[] { 20, 21, 22, }, closedRuns[0].Select(r => r.Soc));
        Assert.Equal(new[] { 22, 21, 20, }, closedRuns[1].Select(r => r.Soc));
        // The turning point is the start of the still open run
        Assert.Equal(readings[4].Timestamp, openRunStart);
    }

    [Fact]
    public void SplitIntoRuns_TreatsRepeatedValuesAsPartOfTheRun()
    {
        var readings = Readings(20, 21, 21, 22, 22, 21);

        var (closedRuns, openRunStart) = UsableEnergyLearningService.SplitIntoRuns(readings);

        var run = Assert.Single(closedRuns);
        Assert.Equal(new[] { 20, 21, 21, 22, 22, }, run.Select(r => r.Soc));
        Assert.Equal(readings[4].Timestamp, openRunStart);
    }

    private static List<(DateTimeOffset Timestamp, int Soc)> Readings(params int[] socs)
    {
        return socs.Select((soc, index) => (HistoryStart.AddMinutes(index * 5), soc)).ToList();
    }

    #endregion

    #region Energy counter

    [Fact]
    public async Task GetEnergyCounterAt_InterpolatesWithThePowerOfTheNextValue()
    {
        AddMeterValue(MeterValueKind.HomeBatteryCharging, HistoryStart, 0, 10_000);
        AddMeterValue(MeterValueKind.HomeBatteryCharging, HistoryStart.AddMinutes(10), 1_000, 10_000 + (1_000 * 600));
        await Context.SaveChangesAsync();

        var counter = await CreateService().GetEnergyCounterAt(MeterValueKind.HomeBatteryCharging, null, HistoryStart.AddMinutes(4), CancellationToken.None);

        Assert.Equal(10_000 + (1_000 * 240), counter);
    }

    [Fact]
    public async Task GetEnergyCounterAt_WithoutEarlierValue_IsUnknown()
    {
        AddMeterValue(MeterValueKind.HomeBatteryCharging, HistoryStart, 0, 10_000);
        await Context.SaveChangesAsync();

        var counter = await CreateService().GetEnergyCounterAt(MeterValueKind.HomeBatteryCharging, null, HistoryStart.AddMinutes(-1), CancellationToken.None);

        Assert.Null(counter);
    }

    [Theory]
    // After a zero power value nothing flows, so the counter stays where it is
    [InlineData(0, 10_000L)]
    // While power was flowing the energy after the last value is unknown
    [InlineData(500, null)]
    public async Task GetEnergyCounterAt_AfterTheLastValue_OnlyKnownWhenNothingFlowed(int lastPower, long? expected)
    {
        AddMeterValue(MeterValueKind.HomeBatteryCharging, HistoryStart, lastPower, 10_000);
        await Context.SaveChangesAsync();

        var counter = await CreateService().GetEnergyCounterAt(MeterValueKind.HomeBatteryCharging, null, HistoryStart.AddMinutes(30), CancellationToken.None);

        Assert.Equal(expected, counter);
    }

    [Fact]
    public async Task GetEnergyCounterAt_OnlyUsesTheRequestedCar()
    {
        AddCar();
        Context.Cars.Add(new Car { Id = 2, Vin = "OTHER", });
        AddMeterValue(MeterValueKind.Car, HistoryStart, 0, 10_000, CarId);
        AddMeterValue(MeterValueKind.Car, HistoryStart.AddMinutes(1), 0, 999_999, 2);
        await Context.SaveChangesAsync();

        var counter = await CreateService().GetEnergyCounterAt(MeterValueKind.Car, CarId, HistoryStart.AddMinutes(5), CancellationToken.None);

        Assert.Equal(10_000, counter);
    }

    #endregion

    #region Car samples

    [Fact]
    public async Task RefreshAsync_LearnsCarUsableEnergyFromEnoughCharges()
    {
        AddCar();
        for (var i = 0; i < UsableEnergyLearningService.RequiredSampleCount; i++)
        {
            // 34.5 kWh wall energy for 50 % with 15 % loss => 60 kWh
            AddCharge(HistoryStart.AddDays(i), 20, 70, 34.5);
        }
        await Context.SaveChangesAsync();

        await CreateService().RefreshAsync(CancellationToken.None);

        Assert.Equal(60_000, _learnedCarUsableEnergies[CarId]);
        var samples = await Context.UsableEnergySamples.ToListAsync();
        Assert.Equal(UsableEnergyLearningService.RequiredSampleCount, samples.Count);
        Assert.All(samples, s =>
        {
            Assert.Null(s.RejectionReason);
            Assert.Equal(Vin, s.CarVin);
            Assert.Equal(34_500, s.EnergyWh);
        });
    }

    [Fact]
    public async Task RefreshAsync_WithTooFewCharges_DoesNotLearnTheCar()
    {
        AddCar();
        _learnedCarUsableEnergies[CarId] = 99_000;
        for (var i = 0; i < UsableEnergyLearningService.RequiredSampleCount - 1; i++)
        {
            AddCharge(HistoryStart.AddDays(i), 20, 70, 34.5);
        }
        await Context.SaveChangesAsync();

        await CreateService().RefreshAsync(CancellationToken.None);

        Assert.False(_learnedCarUsableEnergies.ContainsKey(CarId));
    }

    [Fact]
    public async Task RefreshAsync_MeasuresEachChargeOnlyOnce()
    {
        AddCar();
        AddCharge(HistoryStart, 20, 70, 34.5);
        await Context.SaveChangesAsync();
        var service = CreateService();

        await service.RefreshAsync(CancellationToken.None);
        await service.RefreshAsync(CancellationToken.None);

        Assert.Equal(1, await Context.UsableEnergySamples.CountAsync());
    }

    [Fact]
    public async Task RefreshAsync_StoresWhyAChargeCanNotBeMeasured()
    {
        AddCar();
        var smallCharge = AddCharge(HistoryStart, 20, 35, 10);
        var noSocCharge = AddCharge(HistoryStart.AddDays(1), 20, 70, 34.5, addSocReadings: false);
        var notFinishedId = _nextChargingProcessId++;
        Context.ChargingProcesses.Add(new ChargingProcess { Id = notFinishedId, CarId = CarId, StartDate = HistoryStart.AddDays(2).UtcDateTime, });
        await Context.SaveChangesAsync();

        await CreateService().RefreshAsync(CancellationToken.None);

        var samples = await Context.UsableEnergySamples.ToDictionaryAsync(s => s.ChargingProcessId!.Value);
        Assert.Equal(UsableEnergySampleRejectionReason.SocDifferenceTooSmall, samples[smallCharge].RejectionReason);
        Assert.Equal(UsableEnergySampleRejectionReason.MissingSoc, samples[noSocCharge].RejectionReason);
        Assert.False(samples.ContainsKey(notFinishedId));
    }

    [Fact]
    public async Task CreateCarSample_IgnoresEstimatedSoc()
    {
        AddCar();
        var start = HistoryStart;
        var end = start.AddHours(3);
        AddSoc(start.AddMinutes(1), 20, CarValueSource.FleetApi);
        AddSoc(end.AddMinutes(-1), 70, CarValueSource.Estimation);
        await Context.SaveChangesAsync();

        var sample = await CreateService().CreateCarSample(CarId, Vin, 1, start.UtcDateTime, end.UtcDateTime, CancellationToken.None);

        Assert.Equal(UsableEnergySampleRejectionReason.MissingSoc, sample.RejectionReason);
    }

    [Fact]
    public async Task CreateCarSample_WithoutMeterValues_IsRejected()
    {
        AddCar();
        var start = HistoryStart;
        var end = start.AddHours(3);
        AddSoc(start.AddMinutes(1), 20, CarValueSource.FleetApi);
        AddSoc(end.AddMinutes(-1), 70, CarValueSource.Ble);
        await Context.SaveChangesAsync();

        var sample = await CreateService().CreateCarSample(CarId, Vin, 1, start.UtcDateTime, end.UtcDateTime, CancellationToken.None);

        Assert.Equal(UsableEnergySampleRejectionReason.MissingMeterValues, sample.RejectionReason);
        Assert.Equal(20, sample.StartSoc);
        Assert.Equal(70, sample.EndSoc);
    }

    [Fact]
    public async Task RefreshAsync_ADifferentVin_ForgetsTheOldVehicleAndLearnsOnlyNewerCharges()
    {
        AddCar();
        for (var i = 0; i < UsableEnergyLearningService.RequiredSampleCount; i++)
        {
            AddCharge(HistoryStart.AddDays(i), 20, 70, 34.5);
        }
        await Context.SaveChangesAsync();
        var service = CreateService();
        await service.RefreshAsync(CancellationToken.None);
        Assert.True(_learnedCarUsableEnergies.ContainsKey(CarId));

        await Context.Cars.ExecuteUpdateAsync(c => c.SetProperty(car => car.Vin, "LRW0000000000002"));
        // A charge after the VIN change is learned, all earlier ones belong to the other vehicle
        AddCharge(Now.AddDays(1), 20, 70, 23);
        await Context.SaveChangesAsync();
        await service.RefreshAsync(CancellationToken.None);

        var sample = Assert.Single(await Context.UsableEnergySamples.ToListAsync());
        Assert.Equal("LRW0000000000002", sample.CarVin);
        Assert.False(_learnedCarUsableEnergies.ContainsKey(CarId));
        var car = await Context.Cars.AsNoTracking().SingleAsync();
        Assert.Equal(Now.UtcDateTime, car.UsableEnergyLearnedSince);
    }

    [Fact]
    public async Task ResetCarAsync_ForgetsSamplesAndDoesNotRelearnOlderCharges()
    {
        AddCar();
        for (var i = 0; i < UsableEnergyLearningService.RequiredSampleCount; i++)
        {
            AddCharge(HistoryStart.AddDays(i), 20, 70, 34.5);
        }
        await Context.SaveChangesAsync();
        var service = CreateService();
        await service.RefreshAsync(CancellationToken.None);

        await service.ResetCarAsync(CarId, CancellationToken.None);
        await service.RefreshAsync(CancellationToken.None);

        Assert.Empty(await Context.UsableEnergySamples.ToListAsync());
        Assert.False(_learnedCarUsableEnergies.ContainsKey(CarId));
    }

    [Fact]
    public async Task GetCarStateAsync_UsesOnlyTheNewestSamples()
    {
        AddCar();
        // Old charges of a degraded estimate must no longer count once enough newer ones exist
        for (var i = 0; i < 10; i++)
        {
            AddStoredCarSample(HistoryStart.AddDays(i), 80_000);
        }
        for (var i = 0; i < UsableEnergyLearningService.MaxSampleCount; i++)
        {
            AddStoredCarSample(HistoryStart.AddDays(20 + i), 60_000);
        }
        await Context.SaveChangesAsync();

        var state = await CreateService().GetCarStateAsync(CarId, CancellationToken.None);

        Assert.Equal(60, state.LearnedUsableEnergyKwh);
        Assert.True(state.IsUsed);
        var estimate = Assert.Single(state.Estimates);
        Assert.Equal(UsableEnergyLearningService.MaxSampleCount, estimate.AcceptedSampleCount);
        Assert.Equal(UsableEnergyLearningService.MaxSampleCount, state.Samples.Count(s => s.IsUsedForEstimate));
        Assert.True(state.Samples.First().EndTime > state.Samples.Last().EndTime);
    }

    [Fact]
    public async Task GetCarStateAsync_WithLearningSwitchedOff_IsNotUsed()
    {
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.UseLearnedUsableEnergy()).Returns(false);
        AddCar();
        for (var i = 0; i < UsableEnergyLearningService.RequiredSampleCount; i++)
        {
            AddStoredCarSample(HistoryStart.AddDays(i), 60_000);
        }
        await Context.SaveChangesAsync();

        var state = await CreateService().GetCarStateAsync(CarId, CancellationToken.None);

        Assert.False(state.LearningEnabled);
        Assert.Equal(60, state.LearnedUsableEnergyKwh);
        Assert.False(state.IsUsed);
    }

    [Fact]
    public async Task GetCarStateAsync_AppliesTheCurrentChargeLossToOldSamples()
    {
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.CarChargeLoss()).Returns(0);
        AddCar();
        for (var i = 0; i < UsableEnergyLearningService.RequiredSampleCount; i++)
        {
            AddStoredCarSample(HistoryStart.AddDays(i), 60_000);
        }
        await Context.SaveChangesAsync();

        var state = await CreateService().GetCarStateAsync(CarId, CancellationToken.None);

        // Stored samples hold the wall energy of 69 kWh for 100 %, without loss that is the usable energy
        Assert.Equal(69, state.LearnedUsableEnergyKwh);
    }

    private void AddCar()
    {
        Context.Cars.Add(new Car { Id = CarId, Vin = Vin, CarType = CarType.Tesla, ShouldBeManaged = true, UsableEnergy = 75, });
    }

    private void AddSoc(DateTimeOffset timestamp, int soc, CarValueSource source)
    {
        Context.CarValueLogs.Add(new CarValueLog
        {
            CarId = CarId, Timestamp = timestamp.UtcDateTime, Type = CarValueType.StateOfCharge, IntValue = soc, Source = source,
        });
    }

    /// <returns>The id of the charging process.</returns>
    private int AddCharge(DateTimeOffset start, int startSoc, int endSoc, double wallEnergyKwh, bool addSocReadings = true)
    {
        // The SoC is read one minute after the start and one minute before the end, so exactly 2 hours of charging
        // lie between the two readings.
        var end = start.AddHours(2).AddMinutes(2);
        var power = (int)(wallEnergyKwh * 1000 / 2);
        var id = _nextChargingProcessId++;
        Context.ChargingProcesses.Add(new ChargingProcess { Id = id, CarId = CarId, StartDate = start.UtcDateTime, EndDate = end.UtcDateTime, });
        if (addSocReadings)
        {
            AddSoc(start.AddMinutes(1), startSoc, CarValueSource.FleetTelemetry);
            AddSoc(end.AddMinutes(-1), endSoc, CarValueSource.FleetTelemetry);
        }
        AddMeterValue(MeterValueKind.Car, start, 0, _carCounterWs, CarId);
        _carCounterWs += (long)power * (long)(end - start).TotalSeconds;
        AddMeterValue(MeterValueKind.Car, end, power, _carCounterWs, CarId);
        AddMeterValue(MeterValueKind.Car, end.AddMilliseconds(1), 0, _carCounterWs, CarId);
        return id;
    }

    /// <param name="wallUsableEnergyWh">Usable energy the sample implies before the charge loss is removed.</param>
    private void AddStoredCarSample(DateTimeOffset end, int wallUsableEnergyWh)
    {
        Context.UsableEnergySamples.Add(new UsableEnergySample
        {
            Kind = UsableEnergySampleKind.CarCharge,
            CarId = CarId,
            CarVin = Vin,
            StartTime = end.AddHours(-3),
            EndTime = end,
            StartSoc = 20,
            EndSoc = 70,
            EnergyWh = (int)(wallUsableEnergyWh * (1 + (ChargeLossPercent / 100.0)) / 2),
        });
    }

    #endregion

    #region Home battery samples

    [Fact]
    public async Task RefreshAsync_LearnsHomeBatteryChargingAndDischargingSeparately()
    {
        var battery = new HomeBatteryHistory(this, HistoryStart, 20);
        for (var i = 0; i < UsableEnergyLearningService.RequiredSampleCount; i++)
        {
            battery.MoveTo(60, 10_000);
            battery.MoveTo(20, 9_000);
        }
        // Turning around once more finishes the last discharge
        battery.MoveTo(21, 10_000);
        await Context.SaveChangesAsync();

        await CreateService().RefreshAsync(CancellationToken.None);

        Assert.Equal(10_000, Mock.Mock<ISettings>().Object.LearnedHomeBatteryChargingEnergyWh);
        Assert.Equal(9_000, Mock.Mock<ISettings>().Object.LearnedHomeBatteryDischargingEnergyWh);
        var samples = await Context.UsableEnergySamples.ToListAsync();
        Assert.Equal(UsableEnergyLearningService.RequiredSampleCount, samples.Count(s => s.Kind == UsableEnergySampleKind.HomeBatteryCharge && s.RejectionReason == null));
        Assert.Equal(UsableEnergyLearningService.RequiredSampleCount, samples.Count(s => s.Kind == UsableEnergySampleKind.HomeBatteryDischarge && s.RejectionReason == null));
        Mock.Mock<ITscConfigurationService>().Verify(t => t.SetConfigurationValueByKey(
            It.IsAny<string>(), battery.LastTurningPoint.ToString("O", System.Globalization.CultureInfo.InvariantCulture)), Times.Once);
    }

    [Fact]
    public async Task GetHomeBatteryStateAsync_ShowsBothLearnedDirections()
    {
        AddStoredHomeBatterySamples(UsableEnergySampleKind.HomeBatteryCharge, 10_000);
        AddStoredHomeBatterySamples(UsableEnergySampleKind.HomeBatteryDischarge, 9_000);
        await Context.SaveChangesAsync();

        var state = await CreateService().GetHomeBatteryStateAsync(CancellationToken.None);

        Assert.Equal(10, state.LearnedChargingEnergyKwh);
        Assert.Equal(9, state.LearnedDischargingEnergyKwh);
        Assert.Null(state.LearnedUsableEnergyKwh);
        Assert.True(state.IsUsed);
        Assert.False(state.DischargingExceedsCharging);
    }

    [Fact]
    public async Task UpdateLearnedValuesAsync_DeliveringMoreThanCharged_UsesNeitherDirection()
    {
        Mock.Mock<ISettings>().Object.LearnedHomeBatteryChargingEnergyWh = 1;
        Mock.Mock<ISettings>().Object.LearnedHomeBatteryDischargingEnergyWh = 1;
        AddStoredHomeBatterySamples(UsableEnergySampleKind.HomeBatteryCharge, 9_000);
        AddStoredHomeBatterySamples(UsableEnergySampleKind.HomeBatteryDischarge, 10_000);
        await Context.SaveChangesAsync();
        var service = CreateService();

        await service.UpdateLearnedValuesAsync(CancellationToken.None);

        Assert.Null(Mock.Mock<ISettings>().Object.LearnedHomeBatteryChargingEnergyWh);
        Assert.Null(Mock.Mock<ISettings>().Object.LearnedHomeBatteryDischargingEnergyWh);
        var state = await service.GetHomeBatteryStateAsync(CancellationToken.None);
        Assert.True(state.DischargingExceedsCharging);
        Assert.False(state.IsUsed);
    }

    [Fact]
    public async Task UpdateLearnedValuesAsync_EqualDirections_AreUsed()
    {
        // A battery without any measurable loss is unusual but not impossible
        AddStoredHomeBatterySamples(UsableEnergySampleKind.HomeBatteryCharge, 10_000);
        AddStoredHomeBatterySamples(UsableEnergySampleKind.HomeBatteryDischarge, 10_000);
        await Context.SaveChangesAsync();

        await CreateService().UpdateLearnedValuesAsync(CancellationToken.None);

        Assert.Equal(10_000, Mock.Mock<ISettings>().Object.LearnedHomeBatteryChargingEnergyWh);
        Assert.Equal(10_000, Mock.Mock<ISettings>().Object.LearnedHomeBatteryDischargingEnergyWh);
    }

    private void AddStoredHomeBatterySamples(UsableEnergySampleKind kind, int usableEnergyWh)
    {
        for (var i = 0; i < UsableEnergyLearningService.RequiredSampleCount; i++)
        {
            var end = HistoryStart.AddDays(i).AddHours(kind == UsableEnergySampleKind.HomeBatteryCharge ? 12 : 23);
            Context.UsableEnergySamples.Add(new UsableEnergySample
            {
                Kind = kind,
                StartTime = end.AddHours(-4),
                EndTime = end,
                StartSoc = kind == UsableEnergySampleKind.HomeBatteryCharge ? 20 : 60,
                EndSoc = kind == UsableEnergySampleKind.HomeBatteryCharge ? 60 : 20,
                EnergyWh = usableEnergyWh * 40 / 100,
            });
        }
    }

    [Fact]
    public async Task RefreshAsync_WithOnlyOneDirectionReliable_DoesNotLearnTheHomeBattery()
    {
        var battery = new HomeBatteryHistory(this, HistoryStart, 20);
        // The discharges vary far too much to trust them
        foreach (var dischargeUsableEnergyWh in new[] { 6_000, 9_000, 12_000, 15_000, 18_000, })
        {
            battery.MoveTo(60, 10_000);
            battery.MoveTo(20, dischargeUsableEnergyWh);
        }
        battery.MoveTo(21, 10_000);
        await Context.SaveChangesAsync();

        var service = CreateService();
        await service.RefreshAsync(CancellationToken.None);

        Assert.Null(Mock.Mock<ISettings>().Object.LearnedHomeBatteryChargingEnergyWh);
        Assert.Null(Mock.Mock<ISettings>().Object.LearnedHomeBatteryDischargingEnergyWh);
        var state = await service.GetHomeBatteryStateAsync(CancellationToken.None);
        Assert.Equal(2, state.Estimates.Count);
        Assert.True(state.Estimates.Single(e => e.Kind == UsableEnergySampleKind.HomeBatteryCharge).IsReliable);
        Assert.False(state.Estimates.Single(e => e.Kind == UsableEnergySampleKind.HomeBatteryDischarge).IsReliable);
    }

    [Fact]
    public async Task RefreshAsync_ContinuesAfterTheLastFinishedRun()
    {
        var battery = new HomeBatteryHistory(this, HistoryStart, 20);
        battery.MoveTo(60, 10_000);
        battery.MoveTo(20, 10_000);
        battery.MoveTo(21, 10_000);
        await Context.SaveChangesAsync();
        Mock.Mock<ITscConfigurationService>()
            .Setup(t => t.GetConfigurationValueByKey(It.IsAny<string>()))
            .ReturnsAsync(battery.LastTurningPoint.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

        await CreateService().RefreshAsync(CancellationToken.None);

        // Both runs ended before the stored point, only the open run after it is looked at
        Assert.Empty(await Context.UsableEnergySamples.ToListAsync());
    }

    [Fact]
    public async Task RefreshAsync_WithSeveralHomeBatteries_LearnsNothing()
    {
        _sourceValues.Add(new DtoPvSourceValue { ConfigurationType = ConfigurationType.ModbusSolarValue, SourceId = 1, UsedFor = ValueUsage.HomeBatterySoc, });
        _sourceValues.Add(new DtoPvSourceValue { ConfigurationType = ConfigurationType.ModbusSolarValue, SourceId = 2, UsedFor = ValueUsage.HomeBatterySoc, });
        var battery = new HomeBatteryHistory(this, HistoryStart, 20);
        battery.MoveTo(60, 10_000);
        battery.MoveTo(20, 10_000);
        await Context.SaveChangesAsync();

        var service = CreateService();
        await service.RefreshAsync(CancellationToken.None);

        Assert.Empty(await Context.UsableEnergySamples.ToListAsync());
        Assert.True((await service.GetHomeBatteryStateAsync(CancellationToken.None)).HasMultipleHomeBatteries);
    }

    [Fact]
    public async Task CreateHomeBatterySample_UsesTheNetEnergy()
    {
        var battery = new HomeBatteryHistory(this, HistoryStart, 20);
        battery.MoveTo(60, 10_000, oppositePower: 50);
        var run = battery.Readings.ToList();
        battery.MoveTo(59, 10_000);
        await Context.SaveChangesAsync();

        var sample = await CreateService().CreateHomeBatterySample(run, CancellationToken.None);

        Assert.Equal(UsableEnergySampleKind.HomeBatteryCharge, sample.Kind);
        Assert.Null(sample.RejectionReason);
        // 4 kWh charged, 40 steps of 5 min with 50 W discharged = 166.7 Wh
        Assert.Equal(4_000 - 166, sample.EnergyWh);
    }

    [Fact]
    public async Task CreateHomeBatterySample_WithTooMuchOppositeEnergy_IsRejected()
    {
        var battery = new HomeBatteryHistory(this, HistoryStart, 60);
        // 1200 W discharge per step, 100 W charging is more than 5 % of it
        battery.MoveTo(20, 10_000, oppositePower: 100);
        var run = battery.Readings.ToList();
        battery.MoveTo(21, 10_000);
        await Context.SaveChangesAsync();

        var sample = await CreateService().CreateHomeBatterySample(run, CancellationToken.None);

        Assert.Equal(UsableEnergySampleKind.HomeBatteryDischarge, sample.Kind);
        Assert.Equal(UsableEnergySampleRejectionReason.OppositeEnergyFlow, sample.RejectionReason);
    }

    [Fact]
    public async Task CreateHomeBatterySample_WithSocJump_IsRejected()
    {
        var battery = new HomeBatteryHistory(this, HistoryStart, 20);
        battery.MoveTo(40, 10_000);
        battery.JumpTo(46);
        battery.MoveTo(60, 10_000);
        await Context.SaveChangesAsync();

        var sample = await CreateService().CreateHomeBatterySample(battery.Readings, CancellationToken.None);

        Assert.Equal(UsableEnergySampleRejectionReason.SocJump, sample.RejectionReason);
    }

    [Fact]
    public async Task CreateHomeBatterySample_WithMeterValueGap_IsRejected()
    {
        var battery = new HomeBatteryHistory(this, HistoryStart, 20);
        battery.MoveTo(40, 10_000);
        battery.MoveTo(60, 10_000, stepMinutes: 20);
        var run = battery.Readings.ToList();
        battery.MoveTo(59, 10_000);
        await Context.SaveChangesAsync();

        var sample = await CreateService().CreateHomeBatterySample(run, CancellationToken.None);

        Assert.Equal(UsableEnergySampleRejectionReason.MeterValueGap, sample.RejectionReason);
    }

    [Fact]
    public async Task ResetHomeBatteryAsync_ForgetsSamplesAndLearnsFromNowOn()
    {
        Context.UsableEnergySamples.Add(new UsableEnergySample
        {
            Kind = UsableEnergySampleKind.HomeBatteryCharge, StartTime = HistoryStart, EndTime = HistoryStart.AddHours(1), StartSoc = 20, EndSoc = 60, EnergyWh = 4_000,
        });
        AddCar();
        AddStoredCarSample(HistoryStart, 60_000);
        await Context.SaveChangesAsync();

        await CreateService().ResetHomeBatteryAsync(CancellationToken.None);

        var remaining = Assert.Single(await Context.UsableEnergySamples.ToListAsync());
        Assert.Equal(UsableEnergySampleKind.CarCharge, remaining.Kind);
        Mock.Mock<ITscConfigurationService>().Verify(t => t.SetConfigurationValueByKey(
            It.IsAny<string>(), Now.ToString("O", System.Globalization.CultureInfo.InvariantCulture)), Times.Once);
    }

    private void AddMeterValue(MeterValueKind kind, DateTimeOffset timestamp, int power, long counterWs, int? carId = null)
    {
        Context.MeterValues.Add(new MeterValue(timestamp, kind, power) { CarId = carId, EstimatedEnergyWs = counterWs, });
    }

    /// <summary>
    /// Writes home battery SoC logs and meter values as TSC records them: one SoC log per changed percent and
    /// meter values whose power applies to the time since the previous meter value.
    /// </summary>
    private class HomeBatteryHistory
    {
        private readonly UsableEnergyLearningServiceTests _tests;
        private DateTimeOffset _time;
        private int _soc;
        private long _chargedWs;
        private long _dischargedWs;

        public List<(DateTimeOffset Timestamp, int Soc)> Readings { get; } = new();
        public DateTimeOffset LastTurningPoint { get; private set; }

        public HomeBatteryHistory(UsableEnergyLearningServiceTests tests, DateTimeOffset start, int soc)
        {
            _tests = tests;
            _time = start;
            _soc = soc;
            Add(0, 0);
        }

        public void MoveTo(int targetSoc, int usableEnergyWh, int oppositePower = 0, int stepMinutes = 5)
        {
            LastTurningPoint = _time;
            var direction = Math.Sign(targetSoc - _soc);
            // Power that moves 1 % in one step
            var power = (int)(usableEnergyWh / 100.0 * 60 / stepMinutes);
            while (_soc != targetSoc)
            {
                _time = _time.AddMinutes(stepMinutes);
                _soc += direction;
                var chargingPower = direction > 0 ? power : oppositePower;
                var dischargingPower = direction > 0 ? oppositePower : power;
                _chargedWs += (long)chargingPower * stepMinutes * 60;
                _dischargedWs += (long)dischargingPower * stepMinutes * 60;
                Add(chargingPower, dischargingPower);
            }
        }

        public void JumpTo(int soc)
        {
            _time = _time.AddMinutes(5);
            _soc = soc;
            Add(0, 0);
        }

        private void Add(int chargingPower, int dischargingPower)
        {
            _tests.Context.PvValueLogs.Add(new PvValueLog { Timestamp = _time, Type = PvValueType.HomeBatterySoc, IntValue = _soc, });
            _tests.AddMeterValue(MeterValueKind.HomeBatteryCharging, _time, chargingPower, _chargedWs);
            _tests.AddMeterValue(MeterValueKind.HomeBatteryDischarging, _time, dischargingPower, _dischargedWs);
            Readings.Add((_time, _soc));
        }
    }

    #endregion
}
