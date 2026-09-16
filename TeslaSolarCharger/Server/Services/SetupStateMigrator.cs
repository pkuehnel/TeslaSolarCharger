using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Setup;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class SetupStateMigrator(ILogger<SetupStateMigrator> logger) : ISetupStateMigrator
{
    /// <summary>
    /// Order of the steps in the version 1 stepper. A version 1 state stored the position in that list, so the
    /// index is translated back into the stable key it stood for. The list must never be reordered.
    /// </summary>
    private static readonly SetupStepKey[] V1StepOrder =
    [
        SetupStepKey.Welcome,
        SetupStepKey.CloudConnection,
        SetupStepKey.SolarAndBattery,
        SetupStepKey.Location,
        SetupStepKey.Prices,
        SetupStepKey.CarsAndCharging,
        SetupStepKey.Finish,
    ];

    public DtoSetupState? Migrate(string? persistedJson)
    {
        logger.LogTrace("{method}(...)", nameof(Migrate));
        if (string.IsNullOrWhiteSpace(persistedJson))
        {
            return null;
        }

        JObject parsed;
        try
        {
            parsed = JObject.Parse(persistedJson);
        }
        catch (JsonException exception)
        {
            //A state we cannot read at all is worse than no state: continuing with it would show the user answers
            //that are not actually stored. Starting over is the only honest option, so say so in the log.
            logger.LogWarning(exception, "Persisted setup state could not be parsed and is ignored.");
            return null;
        }

        //Version 1 predates the schema version, so a missing property identifies it.
        var schemaVersion = parsed.Value<int?>(nameof(DtoSetupState.SchemaVersion)) ?? 1;
        if (schemaVersion >= DtoSetupState.CurrentSchemaVersion)
        {
            var current = parsed.ToObject<DtoSetupState>();
            if (current == null)
            {
                logger.LogWarning("Persisted setup state of version {schemaVersion} deserialized to null and is ignored.", schemaVersion);
                return null;
            }
            //A state written by a newer version may contain properties this version does not know. Keeping the
            //stored version number would make this version's writes look newer than they are, so stamp it down to
            //what we actually understand.
            current.SchemaVersion = DtoSetupState.CurrentSchemaVersion;
            return current;
        }

        return MigrateFromVersion1(parsed);
    }

    private DtoSetupState MigrateFromVersion1(JObject parsed)
    {
        logger.LogInformation("Migrating setup state from schema version 1.");
        var legacy = parsed.ToObject<DtoSetupCache>() ?? new DtoSetupCache();
        var state = new DtoSetupState
        {
            SchemaVersion = DtoSetupState.CurrentSchemaVersion,
            //An index we cannot place (a state written by a build with a different step list) must not strand the
            //user on an unknown step, so fall back to the start rather than to nothing.
            CurrentStep = MapStepIndex(legacy.CurrentStep) is var mapped && mapped != SetupStepKey.Unknown
                ? mapped
                : SetupStepKey.Welcome,
            CompletedSteps = legacy.CompletedSteps
                .Select(MapStepIndex)
                .Where(k => k != SetupStepKey.Unknown)
                .Distinct()
                .ToList(),
            //Version 1 could not tell "answered no" from "never asked", so its value is taken at face value: it was
            //only ever written once the user had seen the question.
            HasPvSystem = legacy.HasPvSystem,
            HasHomeBattery = legacy.HasHomeBattery,
            Configuration = legacy.Configuration,
            ChargePrice = legacy.ChargePrice,
            FixedPrices = legacy.FixedPrices,
            CarsChargingSetup = legacy.CarsChargingSetup,
        };

        return state;
    }

    private static SetupStepKey MapStepIndex(int stepIndex) =>
        stepIndex >= 0 && stepIndex < V1StepOrder.Length ? V1StepOrder[stepIndex] : SetupStepKey.Unknown;
}
