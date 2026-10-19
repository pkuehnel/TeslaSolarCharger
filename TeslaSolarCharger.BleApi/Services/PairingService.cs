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
        var privateKeyPath = GetRequiredPath("PrivateKeyPath");
        var publicKeyPath = GetRequiredPath("PublicKeyPath");
        await GeneratePrivateKey(privateKeyPath).ConfigureAwait(false);
        await DerivePublicKey(privateKeyPath, publicKeyPath).ConfigureAwait(false);
    }

    public async Task EnsureKeyPair()
    {
        logger.LogTrace("{method}()", nameof(EnsureKeyPair));
        var privateKeyPath = GetRequiredPath("PrivateKeyPath");
        var publicKeyPath = GetRequiredPath("PublicKeyPath");
        if (!HasContent(privateKeyPath))
        {
            //Without a private key the worker can not even start, so nothing can listen for the cars before the first
            //pairing. A public key left over without its private key belongs to no key anymore and is replaced too.
            logger.LogInformation("No private key found at {privateKeyPath}, generating a new key pair", privateKeyPath);
            await GeneratePrivateKey(privateKeyPath).ConfigureAwait(false);
            await DerivePublicKey(privateKeyPath, publicKeyPath).ConfigureAwait(false);
            return;
        }
        if (!HasContent(publicKeyPath))
        {
            //Never generate a new private key here: every car already paired with the existing one would lose it.
            logger.LogInformation("No public key found at {publicKeyPath}, deriving it from the existing private key", publicKeyPath);
            await DerivePublicKey(privateKeyPath, publicKeyPath).ConfigureAwait(false);
        }
    }

    public async Task<DtoBleCommandResult> PairCar(string vin, string apiRole, string? adapter)
    {
        logger.LogTrace("{method}({vin}, {apiRole}, {adapter})", nameof(PairCar), vin, apiRole, adapter);
        await EnsureKeyPair().ConfigureAwait(false);
        var publicKeyPath = GetRequiredPath("PublicKeyPath");
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

    private async Task GeneratePrivateKey(string privateKeyPath)
    {
        var privateKeyGenerationResult = await commandLineExecutionService.ExecuteCommand("openssl", "ecparam -genkey -name prime256v1 -noout");
        if (string.IsNullOrEmpty(privateKeyGenerationResult.ResultMessage) || (!privateKeyGenerationResult.Success))
        {
            logger.LogError("Error generating private key: {error}", privateKeyGenerationResult.ResultMessage);
            throw new InvalidOperationException("Error generating private key");
        }
        await CreateFile(privateKeyPath, privateKeyGenerationResult.ResultMessage, true);
    }

    private async Task DerivePublicKey(string privateKeyPath, string publicKeyPath)
    {
        var publicKeyGenerationResult = await commandLineExecutionService.ExecuteCommand("openssl", $"ec -in {privateKeyPath} -pubout");
        if (string.IsNullOrEmpty(publicKeyGenerationResult.ResultMessage) || (!publicKeyGenerationResult.Success))
        {
            logger.LogError("Error generating public key: {error}", publicKeyGenerationResult.ResultMessage);
            throw new InvalidOperationException("Error generating public key");
        }
        await CreateFile(publicKeyPath, publicKeyGenerationResult.ResultMessage, true);
    }

    private string GetRequiredPath(string configurationKey)
    {
        var path = configuration.GetValue<string>(configurationKey);
        if (string.IsNullOrEmpty(path))
        {
            logger.LogError("{configurationKey} is not set in the configuration", configurationKey);
            throw new InvalidOperationException($"{configurationKey} is not set in the configuration");
        }
        return path;
    }

    /// <summary>
    /// An empty file is what an interrupted write leaves behind; it is as unusable as no file at all.
    /// </summary>
    private static bool HasContent(string path) => File.Exists(path) && new FileInfo(path).Length > 0;

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
        var directory = Path.GetDirectoryName(fullName);
        if (!string.IsNullOrEmpty(directory))
        {
            //Without the volume from the docker compose file the directory does not exist yet.
            Directory.CreateDirectory(directory);
        }
        await File.WriteAllTextAsync(fullName, content);
    }
}
