using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.Ocpp;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IOcppChargePointConfigurationService
{
    Task<Result<GetConfigurationResponse>> GetOcppConfigurations(string chargepointId, CancellationToken cancellationToken);

    Task<Result<ChangeConfigurationResponse>> SetMeterValuesSampledDataConfiguration(string chargePointId,
        CancellationToken cancellationToken);

    Task<Result<ChangeConfigurationResponse>> SetMeterValuesSampleIntervalConfiguration(string chargePointId,
        CancellationToken cancellationToken);

    Task<Result<object>> RebootCharger(string chargepointId, CancellationToken cancellationToken);
}
