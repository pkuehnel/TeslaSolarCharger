using System;
using System.Collections.Generic;
using Autofac.Extras.Moq;
using Moq;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Server.Services.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingServiceV2;

public class ShouldSkipPowerUpdatesDueToTooRecentAmpChangesOrPluginTests : TestBase
{
    private readonly ITestOutputHelper _outputHelper;

    public ShouldSkipPowerUpdatesDueToTooRecentAmpChangesOrPluginTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
        _outputHelper = outputHelper;
    }

    [Theory]
    [MemberData(nameof(GetTestScenarios))]
    public void ShouldSkipPowerUpdatesDueToTooRecentAmpChangesOrPlugin_Scenarios(
        string scenarioDescription,
        List<DtoLoadPointWithCurrentChargingValues> loadPoints,
        TimeSpan skipWindow,
        List<bool> hasTooLateChangesResults,
        bool expectedResult)
    {
        _outputHelper.WriteLine($"Running scenario: {scenarioDescription}");

        // Arrange
        var configMock = Mock.Mock<IConfigurationWrapper>();
        configMock.Setup(c => c.SkipPowerChangesOnLastAdjustmentNewerThan()).Returns(skipWindow);

        var powerCalcMock = Mock.Mock<IPowerToControlCalculationService>();

        var currentDate = CurrentFakeDate;
        var earliestAmpChange = currentDate - skipWindow;
        var earliestPlugin = currentDate - (2 * skipWindow);

        // Setup expectations for HasTooLateChanges
        for (int i = 0; i < loadPoints.Count; i++)
        {
             var lp = loadPoints[i];
             var mockResult = hasTooLateChangesResults.Count > i ? hasTooLateChangesResults[i] : false;

             powerCalcMock.Setup(x => x.HasTooLateChanges(lp, earliestAmpChange, earliestPlugin))
                 .Returns(mockResult);
        }

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();

        // Act
        var result = service.ShouldSkipPowerUpdatesDueToTooRecentAmpChangesOrPlugin(loadPoints, currentDate);

        // Assert
        Assert.Equal(expectedResult, result);

        // Verify call args for each processed loadpoint
        for(int i = 0; i < loadPoints.Count; i++)
        {
            var lp = loadPoints[i];

            bool previousWasTrue = false;
            for(int j=0; j < i; j++)
            {
                if(hasTooLateChangesResults.Count > j && hasTooLateChangesResults[j])
                {
                    previousWasTrue = true;
                    break;
                }
            }

            if (previousWasTrue)
            {
                powerCalcMock.Verify(x => x.HasTooLateChanges(lp, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()), Times.Never,
                    $"LoadPoint {i} should not be checked if a previous loadpoint already returned true.");
            }
            else
            {
                powerCalcMock.Verify(x => x.HasTooLateChanges(lp, earliestAmpChange, earliestPlugin), Times.Once,
                    $"LoadPoint {i} should be checked with correct timestamps.");
            }
        }
    }

    public static IEnumerable<object[]> GetTestScenarios()
    {
        var lp1 = new DtoLoadPointWithCurrentChargingValues { CarId = 1 };
        var lp2 = new DtoLoadPointWithCurrentChargingValues { CarId = 2 };

        var oneMin = TimeSpan.FromMinutes(1);

        yield return new object[]
        {
            "Empty list",
            new List<DtoLoadPointWithCurrentChargingValues>(),
            oneMin,
            new List<bool>(),
            false
        };

        yield return new object[]
        {
            "Single loadpoint: No late changes",
            new List<DtoLoadPointWithCurrentChargingValues> { lp1 },
            oneMin,
            new List<bool> { false },
            false
        };

        yield return new object[]
        {
            "Single loadpoint: Has late changes",
            new List<DtoLoadPointWithCurrentChargingValues> { lp1 },
            oneMin,
            new List<bool> { true },
            true
        };

        yield return new object[]
        {
            "Multiple loadpoints: None have late changes",
            new List<DtoLoadPointWithCurrentChargingValues> { lp1, lp2 },
            oneMin,
            new List<bool> { false, false },
            false
        };

        yield return new object[]
        {
            "Multiple loadpoints: First one has late changes (Early Exit)",
            new List<DtoLoadPointWithCurrentChargingValues> { lp1, lp2 },
            oneMin,
            new List<bool> { true, false },
            true
        };

        yield return new object[]
        {
            "Multiple loadpoints: Second one has late changes",
            new List<DtoLoadPointWithCurrentChargingValues> { lp1, lp2 },
            oneMin,
            new List<bool> { false, true },
            true
        };
    }
}
