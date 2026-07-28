using TeslaSolarCharger.BleApi.Dtos;
using TeslaSolarCharger.BleApi.Services.Contracts;

namespace TeslaSolarCharger.BleApi.Services;

public class PairingService(ILogger<PairingService> logger,
    ICommandLineExecutionService commandLineExecutionService,
    IConfiguration configuration,
    IBleAdapterGate bleAdapterGate,
    IBleDaemonService bleDaemonService) : IPairingService
{
    public async Task GenerateKeyPair()
    {
        logger.LogInformation("Generating key pair");
        var privateKeyGenerationResult = await commandLineExecutionService.ExecuteCommand("openssl", "ecparam -genkey -name prime256v1 -noout");
        var privateKeyPath = configuration.GetValue<string>("PrivateKeyPath");
        if (string.IsNullOrEmpty(privateKeyPath))
        {
            logger.LogError("PrivateKeyPath is not set in the configuration");
            throw new InvalidOperationException("Private key path not set");
        }

        if (string.IsNullOrEmpty(privateKeyGenerationResult.ResultMessage) || (!privateKeyGenerationResult.Success))
        {
            logger.LogError("Error generating private key: {error}", privateKeyGenerationResult.ResultMessage);
            throw new InvalidOperationException("Error generating private key");
        }
        await CreateFile(privateKeyPath, privateKeyGenerationResult.ResultMessage, true);

        var publicKeyGenerationResult = await commandLineExecutionService.ExecuteCommand("openssl", $"ec -in {privateKeyPath} -pubout");
        var publicKeyPath = configuration.GetValue<string>("PublicKeyPath");
        if (string.IsNullOrEmpty(publicKeyPath))
        {
            logger.LogError("PublicKeyPath is not set in the configuration");
            throw new InvalidOperationException("Public key path not set");
        }

        if (string.IsNullOrEmpty(publicKeyGenerationResult.ResultMessage) || (!publicKeyGenerationResult.Success))
        {
            logger.LogError("Error generating public key: {error}", publicKeyGenerationResult.ResultMessage);
            throw new InvalidOperationException("Error generating public key");
        }
        await CreateFile(publicKeyPath, publicKeyGenerationResult.ResultMessage, true);
    }

    public async Task<DtoBleCommandResult> PairCar(string vin, string apiRole)
    {
        logger.LogTrace("{method}({vin}, {apiRole})", nameof(PairCar), vin, apiRole);
        var publicKeyPath = configuration.GetValue<string>("PublicKeyPath");
        if (!File.Exists(publicKeyPath))
        {
            await GenerateKeyPair().ConfigureAwait(false);
        }
        //Pairing runs its own tesla-control process, which would fight the worker for the Bluetooth adapter: take the
        //adapter and stop the worker for the duration. It starts again lazily on the next command.
        var gateWaitSeconds = configuration.GetValue<int>("SemaphoreSlimWaitTimeoutSeconds");
        if (!await bleAdapterGate.WaitAsync(TimeSpan.FromSeconds(gateWaitSeconds)).ConfigureAwait(false))
        {
            logger.LogError("Bluetooth adapter did not become free in time for pairing");
            return new DtoBleCommandResult()
            {
                Success = false,
                ResultMessage = "Bluetooth adapter is busy, could not start pairing in time.",
                ErrorType = Enums.ErrorType.TeslaControl,
            };
        }
        try
        {
            await bleDaemonService.StopWorker().ConfigureAwait(false);
            var result = await commandLineExecutionService.ExecuteCommand("/app/go/tesla-control", $"-ble -vin {vin} add-key-request {publicKeyPath} {apiRole} cloud_key");
            return result;
        }
        finally
        {
            bleAdapterGate.Release();
        }
    }

    private async Task CreateFile(string fullName, string content, bool overwrite)
    {
        logger.LogTrace("{method}({fileName}, content, {overwrite})", nameof(CreateFile), fullName, overwrite);
        if (File.Exists(fullName))
        {
            if (overwrite)
            {
                File.Delete(fullName);
            }
            else
            {
                logger.LogWarning("File already exists and overwrite is set to false");
                return;
            }
        }
        await File.WriteAllTextAsync(fullName, content);
    }
}
