using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.ServerValidators;

public class ChargingStationConnectorValidator : Shared.Dtos.ChargingStation.ChargingStationConnectorValidator
{
    public ChargingStationConnectorValidator(ITeslaSolarChargerContext teslaSolarChargerContext)
    {
        When(x => x.ShouldBeManaged, () =>
        {
            When(x => x.AutoSwitchBetween1And3PhasesEnabled, () =>
            {
                RuleFor(x => x.AutoSwitchBetween1And3PhasesEnabled)
                    .MustAsync(async (model, propertyValue, context, ct) =>
                    {
                        var canSwitchBetween1And3Phase = await teslaSolarChargerContext.OcppChargingStations
                            .Where(c => c.Id == model.ChargingStationId)
                            .Select(c => c.CanSwitchBetween1And3Phases)
                            .FirstOrDefaultAsync();
                        return canSwitchBetween1And3Phase == true;
                    })
                    .WithMessage("The charger does not support auto switching between phases.");

            });
        });
        
    }
}
