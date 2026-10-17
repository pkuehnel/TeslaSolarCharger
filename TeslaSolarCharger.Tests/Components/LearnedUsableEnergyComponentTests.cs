using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor.Services;
using MudExtensions.Services;
using TeslaSolarCharger.Client.Components;
using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Dtos.UsableEnergy;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Localization;
using TeslaSolarCharger.Shared.Localization.Contracts;
using TeslaSolarCharger.Shared.Localization.Registries;
using TeslaSolarCharger.Shared.Localization.Registries.Components;
using Xunit;

namespace TeslaSolarCharger.Tests.Components;

/// <summary>
/// The line below an entered usable energy: it has to say plainly whether the learned value is used and, if not,
/// what is still missing.
/// </summary>
public class LearnedUsableEnergyComponentTests : Bunit.TestContext
{
    private const int CarId = 3;

    private readonly Mock<IHttpClientHelper> _httpClientHelper = new();
    private DtoLearnedUsableEnergy _state = new() { LearningEnabled = true, };

    public LearnedUsableEnergyComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddMudExtensions();
        Services.AddSharedDependencies();
        _httpClientHelper.Setup(h => h.SendGetRequestAsync<DtoLearnedUsableEnergy>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new Result<DtoLearnedUsableEnergy>(_state, null, null));
        _httpClientHelper.Setup(h => h.SendPostRequestAsync(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<object>(null, null, null));
        Services.AddSingleton(_httpClientHelper.Object);
    }

    private string T(string key) =>
        Services.GetRequiredService<ITextLocalizationService>()
            .Get<LearnedUsableEnergyComponentLocalizationRegistry>(key, typeof(SharedComponentLocalizationRegistry))!;

    private string Format(string key, params object?[] arguments) => string.Format(CultureInfo.CurrentCulture, T(key), arguments);

    private static DtoUsableEnergyEstimate CarEstimate(int acceptedSampleCount, bool isReliable, double? spreadPercent = null) => new()
    {
        Kind = UsableEnergySampleKind.CarCharge,
        AcceptedSampleCount = acceptedSampleCount,
        RequiredSampleCount = 5,
        SpreadPercent = spreadPercent,
        MaxSpreadPercent = 15,
        IsReliable = isReliable,
    };

    private IRenderedComponent<LearnedUsableEnergyComponent> RenderForCar(double enteredKwh = 60) =>
        Render<LearnedUsableEnergyComponent>(p => p
            .Add(c => c.CarId, CarId)
            .Add(c => c.EnteredUsableEnergyKwh, enteredKwh));

    [Fact]
    public void AnUsedLearnedValueIsShownWithTheNumberOfMeasurements()
    {
        _state = new DtoLearnedUsableEnergy
        {
            LearningEnabled = true, IsUsed = true, LearnedUsableEnergyKwh = 57.5, Estimates = { CarEstimate(12, true), },
        };

        var component = RenderForCar();

        component.WaitForAssertion(() => Assert.Contains(
            Format(TranslationKeys.UsableEnergyLearnedInUse, 12, 57.5.ToString("0.#", CultureInfo.CurrentCulture)), component.Markup));
        Assert.Empty(component.FindAll(".mud-alert"));
        _httpClientHelper.Verify(h => h.SendGetRequestAsync<DtoLearnedUsableEnergy>($"api/UsableEnergy/GetCarState?carId={CarId}", It.IsAny<CancellationToken>()));
    }

    [Theory]
    // 45 instead of 60 kWh is 25 % off and probably a wrong entry or charge loss
    [InlineData(45, true)]
    // 50 instead of 60 kWh is within what degradation and an estimated entry explain
    [InlineData(50, false)]
    public void ALargeDifferenceToTheEntryIsPointedOut(double learnedKwh, bool expectWarning)
    {
        _state = new DtoLearnedUsableEnergy
        {
            LearningEnabled = true, IsUsed = true, LearnedUsableEnergyKwh = learnedKwh, Estimates = { CarEstimate(12, true), },
        };

        var component = RenderForCar(enteredKwh: 60);

        component.WaitForAssertion(() => Assert.Contains(T(TranslationKeys.UsableEnergyLearnedInUse).Split('{')[0], component.Markup));
        Assert.Equal(expectWarning, component.FindAll(".mud-alert").Any());
    }

    [Fact]
    public void TheProgressIsShownWhileTooFewChargesWereMeasured()
    {
        _state = new DtoLearnedUsableEnergy { LearningEnabled = true, Estimates = { CarEstimate(3, false), }, };

        var component = RenderForCar();

        component.WaitForAssertion(() => Assert.Contains(
            Format(TranslationKeys.UsableEnergyLearningProgress, T(TranslationKeys.UsableEnergyKindCarCharge), 3, 5), component.Markup));
    }

    [Fact]
    public void InconsistentMeasurementsAreExplained()
    {
        _state = new DtoLearnedUsableEnergy { LearningEnabled = true, Estimates = { CarEstimate(8, false, 22.5), }, };

        var component = RenderForCar();

        component.WaitForAssertion(() => Assert.Contains(
            Format(TranslationKeys.UsableEnergyLearningInconsistent, T(TranslationKeys.UsableEnergyKindCarCharge),
                22.5.ToString("0.#", CultureInfo.CurrentCulture), "15"),
            component.Markup));
    }

    [Fact]
    public void TheHomeBatteryShowsTheProgressOfBothDirections()
    {
        _state = new DtoLearnedUsableEnergy
        {
            LearningEnabled = true,
            Estimates =
            {
                new DtoUsableEnergyEstimate { Kind = UsableEnergySampleKind.HomeBatteryCharge, AcceptedSampleCount = 7, RequiredSampleCount = 5, IsReliable = true, },
                new DtoUsableEnergyEstimate { Kind = UsableEnergySampleKind.HomeBatteryDischarge, AcceptedSampleCount = 2, RequiredSampleCount = 5, },
            },
        };

        var component = Render<LearnedUsableEnergyComponent>(p => p.Add(c => c.EnteredUsableEnergyKwh, 10));

        component.WaitForAssertion(() => Assert.Contains(
            Format(TranslationKeys.UsableEnergyLearningProgress, T(TranslationKeys.UsableEnergyKindHomeBatteryDischarge), 2, 5), component.Markup));
        // The reliable direction is not listed as missing
        Assert.DoesNotContain(Format(TranslationKeys.UsableEnergyLearningProgress, T(TranslationKeys.UsableEnergyKindHomeBatteryCharge), 7, 5), component.Markup);
        _httpClientHelper.Verify(h => h.SendGetRequestAsync<DtoLearnedUsableEnergy>("api/UsableEnergy/GetHomeBatteryState", It.IsAny<CancellationToken>()));
    }

    [Fact]
    public void SwitchedOffLearningSaysSo()
    {
        _state = new DtoLearnedUsableEnergy { LearningEnabled = false, Estimates = { CarEstimate(1, false), }, };

        var component = RenderForCar();

        component.WaitForAssertion(() => Assert.Contains(T(TranslationKeys.UsableEnergyLearningOff), component.Markup));
    }

    [Fact]
    public void SeveralHomeBatteriesAreExplained()
    {
        _state = new DtoLearnedUsableEnergy { LearningEnabled = true, HasMultipleHomeBatteries = true, };

        var component = Render<LearnedUsableEnergyComponent>(p => p.Add(c => c.EnteredUsableEnergyKwh, 10));

        component.WaitForAssertion(() => Assert.Contains(T(TranslationKeys.UsableEnergyMultipleHomeBatteries), component.Markup));
    }

    [Fact]
    public void TheMeasurementsExplainWhyOneWasIgnored()
    {
        _state = new DtoLearnedUsableEnergy
        {
            LearningEnabled = true,
            Estimates = { CarEstimate(1, false), },
            Samples = new List<DtoUsableEnergySample>
            {
                new() { Kind = UsableEnergySampleKind.CarCharge, EndTime = DateTimeOffset.UtcNow, StartSoc = 40, EndSoc = 50, EnergyKwh = 7, RejectionReason = UsableEnergySampleRejectionReason.SocDifferenceTooSmall, },
                new() { Kind = UsableEnergySampleKind.CarCharge, EndTime = DateTimeOffset.UtcNow.AddDays(-1), StartSoc = 20, EndSoc = 70, EnergyKwh = 34.5, ImpliedUsableEnergyKwh = 60, IsUsedForEstimate = true, },
            },
        };
        var component = RenderForCar();
        component.WaitForAssertion(() => Assert.NotEmpty(component.FindAll("button")));

        component.FindAll("button").Single(b => b.TextContent.Contains(T(TranslationKeys.UsableEnergyShowDetails))).Click();

        Assert.Contains(T(TranslationKeys.UsableEnergyRejectedSocDifferenceTooSmall), component.Markup);
        Assert.Contains(T(TranslationKeys.UsableEnergyStatusUsed), component.Markup);
        Assert.Equal(2, component.FindAll("tbody tr").Count);
    }

    [Fact]
    public void ResettingForgetsTheMeasurementsOfThisCar()
    {
        _state = new DtoLearnedUsableEnergy
        {
            LearningEnabled = true,
            Estimates = { CarEstimate(1, false), },
            Samples = new List<DtoUsableEnergySample>
            {
                new() { Kind = UsableEnergySampleKind.CarCharge, EndTime = DateTimeOffset.UtcNow, StartSoc = 20, EndSoc = 70, EnergyKwh = 34.5, ImpliedUsableEnergyKwh = 60, IsUsedForEstimate = true, },
            },
        };
        var component = RenderForCar();
        component.WaitForAssertion(() => Assert.NotEmpty(component.FindAll("button")));
        component.FindAll("button").Single(b => b.TextContent.Contains(T(TranslationKeys.UsableEnergyShowDetails))).Click();
        _state = new DtoLearnedUsableEnergy { LearningEnabled = true, Estimates = { CarEstimate(0, false), }, };

        component.FindAll("button").Single(b => b.TextContent.Contains(T(TranslationKeys.UsableEnergyResetButton))).Click();

        _httpClientHelper.Verify(h => h.SendPostRequestAsync($"api/UsableEnergy/ResetCar?carId={CarId}", null, It.IsAny<CancellationToken>()), Times.Once);
        component.WaitForAssertion(() => Assert.Contains(T(TranslationKeys.UsableEnergyNoSamples), component.Markup));
    }
}
