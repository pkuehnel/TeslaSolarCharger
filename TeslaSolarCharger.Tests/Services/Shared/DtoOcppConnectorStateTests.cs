using System;
using TeslaSolarCharger.Shared.Dtos.Settings;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Shared;

public class DtoOcppConnectorStateTests
{
    [Theory]
    [InlineData(true, 1, 3, 1)]
    [InlineData(true, 3, 1, 3)]
    // Unknown last set phases, e.g. after a restart of TSC
    [InlineData(true, null, 3, 3)]
    [InlineData(true, null, null, null)]
    // Connectors without automatic phase switching might reject a number of phases
    [InlineData(false, 1, 3, null)]
    [InlineData(false, null, 3, null)]
    public void PhasesToKeepWhileCharging_KeepsLastSetPhasesOnlyOnConnectorsWithPhaseSwitching(bool autoSwitchEnabled,
        int? lastSetPhases, int? phasesIfUnknown, int? expected)
    {
        var state = new DtoOcppConnectorState
        {
            LastSetPhases = new DtoTimeStampedValue<int?>(DateTimeOffset.MinValue, lastSetPhases),
        };

        var result = state.PhasesToKeepWhileCharging(autoSwitchEnabled, phasesIfUnknown);

        Assert.Equal(expected, result);
    }
}
