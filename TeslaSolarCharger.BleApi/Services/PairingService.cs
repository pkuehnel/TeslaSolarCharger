using PkSoftwareService.Custom.Backend.Ble;
using TeslaSolarCharger.BleApi.Services.Contracts;

namespace TeslaSolarCharger.BleApi.Services;

public class PairingService(ILogger<PairingService> logger,
    ICommandLineExecutionService commandLineExecutionService,
    IConfiguration configuration,
    IBleWorkerService bleWorkerService) : IPairingService
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

    public async Task<DtoBleCommandResult> PairCar(string vin, string apiRole, string? adapter)
    {
        logger.LogTrace("{method}({vin}, {apiRole}, {adapter})", nameof(PairCar), vin, apiRole, adapter);
        var publicKeyPath = configuration.GetValue<string>("PublicKeyPath");
        if (!File.Exists(publicKeyPath))
        {
            await GenerateKeyPair().ConfigureAwait(false);
        }
        //Pairing runs its own tesla-control process, which would fight the worker for the Bluetooth adapter: the
        //worker of the target adapter is stopped for the duration and restarts lazily on the next request. Workers
        //on other adapters keep serving.
        return await bleWorkerService.RunWithExclusiveAdapter(adapter, async hciId =>
        {
            var adapterParameter = string.IsNullOrEmpty(hciId) ? string.Empty : $"-bt-adapter {hciId} ";
            return await commandLineExecutionService.ExecuteCommand("/app/go/tesla-control",
                $"-ble {adapterParameter}-vin {vin} add-key-request {publicKeyPath} {apiRole} cloud_key").ConfigureAwait(false);
        }).ConfigureAwait(false);
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
