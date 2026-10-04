using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PkSoftwareService.Custom.Backend.Ble;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using TeslaSolarCharger.BleApi.Services;
using TeslaSolarCharger.BleApi.Services.Contracts;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.BleApi;

/// <summary>
/// The container's key is what every paired car trusts. It has to exist before the first pairing, because without it
/// the BLE worker can not start at all, and it must never be replaced behind the user's back, because every car paired
/// with it would silently stop accepting commands.
/// </summary>
public class PairingServiceTests : IDisposable
{
    private const string PrivateKeyCommand = "ecparam -genkey -name prime256v1 -noout";
    private const string GeneratedPrivateKey = "-----BEGIN EC PRIVATE KEY-----\ngenerated\n-----END EC PRIVATE KEY-----\n";
    private const string ExistingPrivateKey = "-----BEGIN EC PRIVATE KEY-----\nexisting\n-----END EC PRIVATE KEY-----\n";
    private const string ExistingPublicKey = "-----BEGIN PUBLIC KEY-----\nexisting\n-----END PUBLIC KEY-----\n";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PairingServiceTests", Guid.NewGuid().ToString("N"));
    private readonly Mock<ICommandLineExecutionService> _commandLineExecutionService = new();
    private readonly Mock<IBleWorkerService> _bleWorkerService = new();

    private string PrivateKeyPath => Path.Combine(_directory, "privateKey.pem");
    private string PublicKeyPath => Path.Combine(_directory, "publicKey.pem");

    public PairingServiceTests()
    {
        Directory.CreateDirectory(_directory);
        _commandLineExecutionService.Setup(s => s.ExecuteCommand("openssl", PrivateKeyCommand))
            .ReturnsAsync(new DtoBleCommandResult { Success = true, ResultMessage = GeneratedPrivateKey, });
        //The public key is read from the private key file, so the fake says which private key it was derived from.
        _commandLineExecutionService.Setup(s => s.ExecuteCommand("openssl", It.Is<string>(p => p.StartsWith("ec -in "))))
            .ReturnsAsync((string _, string parameters) => new DtoBleCommandResult
            {
                Success = true,
                ResultMessage = "public of " + File.ReadAllText(parameters["ec -in ".Length..^" -pubout".Length]),
            });
        _commandLineExecutionService.Setup(s => s.ExecuteCommand("/app/go/tesla-control", It.IsAny<string>()))
            .ReturnsAsync(new DtoBleCommandResult { Success = true, });
        _bleWorkerService.Setup(s => s.RunWithExclusiveAdapter(It.IsAny<string?>(), It.IsAny<Func<string, Task<DtoBleCommandResult>>>()))
            .Returns((string? _, Func<string, Task<DtoBleCommandResult>> action) => action("hci0"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private PairingService CreateService(ICommandLineExecutionService? commandLineExecutionService = null,
        string? privateKeyPath = null, string? publicKeyPath = null) => new(
        Mock.Of<ILogger<PairingService>>(),
        commandLineExecutionService ?? _commandLineExecutionService.Object,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PrivateKeyPath"] = privateKeyPath ?? PrivateKeyPath,
            ["PublicKeyPath"] = publicKeyPath ?? PublicKeyPath,
            ["ProcessExecutionTimeoutSeconds"] = "23",
        }).Build(),
        _bleWorkerService.Object);

    private int PrivateKeyGenerations() => _commandLineExecutionService.Invocations
        .Count(i => (string)i.Arguments[0] == "openssl" && (string)i.Arguments[1] == PrivateKeyCommand);

    private int PublicKeyDerivations() => _commandLineExecutionService.Invocations
        .Count(i => (string)i.Arguments[0] == "openssl" && ((string)i.Arguments[1]).StartsWith("ec -in "));

    [Fact]
    public async Task EnsureKeyPair_WithoutAnyKey_CreatesAMatchingKeyPair()
    {
        await CreateService().EnsureKeyPair();

        Assert.Equal(GeneratedPrivateKey, await File.ReadAllTextAsync(PrivateKeyPath));
        Assert.Equal("public of " + GeneratedPrivateKey, await File.ReadAllTextAsync(PublicKeyPath));
    }

    [Fact]
    public async Task EnsureKeyPair_WithBothKeys_LeavesThemAlone()
    {
        await File.WriteAllTextAsync(PrivateKeyPath, ExistingPrivateKey);
        await File.WriteAllTextAsync(PublicKeyPath, ExistingPublicKey);

        await CreateService().EnsureKeyPair();

        Assert.Equal(ExistingPrivateKey, await File.ReadAllTextAsync(PrivateKeyPath));
        Assert.Equal(ExistingPublicKey, await File.ReadAllTextAsync(PublicKeyPath));
        Assert.Empty(_commandLineExecutionService.Invocations);
    }

    [Fact]
    public async Task EnsureKeyPair_WithOnlyThePrivateKey_DerivesThePublicKeyWithoutReplacingThePrivateKey()
    {
        //Pairing used to create a whole new pair whenever the public key was missing, which unpaired every car.
        await File.WriteAllTextAsync(PrivateKeyPath, ExistingPrivateKey);

        await CreateService().EnsureKeyPair();

        Assert.Equal(ExistingPrivateKey, await File.ReadAllTextAsync(PrivateKeyPath));
        Assert.Equal("public of " + ExistingPrivateKey, await File.ReadAllTextAsync(PublicKeyPath));
        Assert.Equal(0, PrivateKeyGenerations());
    }

    [Fact]
    public async Task EnsureKeyPair_WithOnlyAPublicKey_ReplacesItWithANewPair()
    {
        //A public key without its private key belongs to no usable key.
        await File.WriteAllTextAsync(PublicKeyPath, ExistingPublicKey);

        await CreateService().EnsureKeyPair();

        Assert.Equal(GeneratedPrivateKey, await File.ReadAllTextAsync(PrivateKeyPath));
        Assert.Equal("public of " + GeneratedPrivateKey, await File.ReadAllTextAsync(PublicKeyPath));
    }

    [Fact]
    public async Task EnsureKeyPair_WithAnEmptyPrivateKeyFile_CreatesANewPair()
    {
        await File.WriteAllTextAsync(PrivateKeyPath, string.Empty);
        await File.WriteAllTextAsync(PublicKeyPath, ExistingPublicKey);

        await CreateService().EnsureKeyPair();

        Assert.Equal(GeneratedPrivateKey, await File.ReadAllTextAsync(PrivateKeyPath));
        Assert.Equal("public of " + GeneratedPrivateKey, await File.ReadAllTextAsync(PublicKeyPath));
    }

    [Fact]
    public async Task EnsureKeyPair_WithAnEmptyPublicKeyFile_DerivesItAgain()
    {
        await File.WriteAllTextAsync(PrivateKeyPath, ExistingPrivateKey);
        await File.WriteAllTextAsync(PublicKeyPath, string.Empty);

        await CreateService().EnsureKeyPair();

        Assert.Equal(ExistingPrivateKey, await File.ReadAllTextAsync(PrivateKeyPath));
        Assert.Equal("public of " + ExistingPrivateKey, await File.ReadAllTextAsync(PublicKeyPath));
    }

    [Fact]
    public async Task EnsureKeyPair_WithoutTheKeyDirectory_CreatesIt()
    {
        //Without the volume from the docker compose file the directory does not exist.
        var directory = Path.Combine(_directory, "externalFiles");

        await CreateService(privateKeyPath: Path.Combine(directory, "privateKey.pem"),
            publicKeyPath: Path.Combine(directory, "publicKey.pem")).EnsureKeyPair();

        Assert.True(File.Exists(Path.Combine(directory, "privateKey.pem")));
        Assert.True(File.Exists(Path.Combine(directory, "publicKey.pem")));
    }

    [Fact]
    public async Task EnsureKeyPair_WhenOpenSslFails_ThrowsAndWritesNoKey()
    {
        _commandLineExecutionService.Setup(s => s.ExecuteCommand("openssl", PrivateKeyCommand))
            .ReturnsAsync(new DtoBleCommandResult { Success = false, ResultMessage = "openssl: not found", });

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().EnsureKeyPair());

        Assert.False(File.Exists(PrivateKeyPath));
        Assert.False(File.Exists(PublicKeyPath));
    }

    [Fact]
    public async Task EnsureKeyPair_WithoutConfiguredPaths_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(privateKeyPath: string.Empty).EnsureKeyPair());
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(publicKeyPath: string.Empty).EnsureKeyPair());
        Assert.Empty(_commandLineExecutionService.Invocations);
    }

    [Fact]
    public async Task PairCar_WithoutAnyKey_CreatesTheKeyAndPairsItsPublicKey()
    {
        await CreateService().PairCar("VIN1", "charging_manager", null);

        Assert.True(File.Exists(PrivateKeyPath));
        _commandLineExecutionService.Verify(s => s.ExecuteCommand("/app/go/tesla-control",
            $"-ble -bt-adapter hci0 -vin VIN1 add-key-request {PublicKeyPath} charging_manager cloud_key"), Times.Once);
    }

    [Fact]
    public async Task PairCar_WithOnlyThePrivateKey_KeepsThePrivateKeyTheOtherCarsArePairedWith()
    {
        await File.WriteAllTextAsync(PrivateKeyPath, ExistingPrivateKey);

        await CreateService().PairCar("VIN1", "charging_manager", null);

        Assert.Equal(ExistingPrivateKey, await File.ReadAllTextAsync(PrivateKeyPath));
        Assert.Equal(0, PrivateKeyGenerations());
        Assert.Equal(1, PublicKeyDerivations());
    }

    [Fact]
    public async Task PairCar_WithBothKeys_OnlyPairs()
    {
        await File.WriteAllTextAsync(PrivateKeyPath, ExistingPrivateKey);
        await File.WriteAllTextAsync(PublicKeyPath, ExistingPublicKey);

        await CreateService().PairCar("VIN1", "charging_manager", null);

        Assert.Equal(0, PrivateKeyGenerations());
        Assert.Equal(0, PublicKeyDerivations());
        Assert.Equal(ExistingPrivateKey, await File.ReadAllTextAsync(PrivateKeyPath));
    }

    [Fact]
    public async Task GenerateKeyPair_AlwaysReplacesTheExistingKeys()
    {
        //The explicit endpoint is the one way to start over with a new key on purpose.
        await File.WriteAllTextAsync(PrivateKeyPath, ExistingPrivateKey);
        await File.WriteAllTextAsync(PublicKeyPath, ExistingPublicKey);

        await CreateService().GenerateKeyPair();

        Assert.Equal(GeneratedPrivateKey, await File.ReadAllTextAsync(PrivateKeyPath));
        Assert.Equal("public of " + GeneratedPrivateKey, await File.ReadAllTextAsync(PublicKeyPath));
    }

    [Fact]
    public async Task EnsureKeyPair_WithRealOpenSsl_CreatesAPrime256v1KeyPairThatBelongsTogether()
    {
        Assert.SkipUnless(IsOpenSslAvailable(), "openssl is not installed on this machine");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ProcessExecutionTimeoutSeconds"] = "23",
        }).Build();
        var commandLineExecutionService = new CommandLineExecutionService(Mock.Of<ILogger<CommandLineExecutionService>>(), configuration);

        await CreateService(commandLineExecutionService).EnsureKeyPair();

        //tesla-control reads the private key in the SEC1 format openssl writes, and pairs the public key as SPKI.
        var privateKeyPem = await File.ReadAllTextAsync(PrivateKeyPath);
        var publicKeyPem = await File.ReadAllTextAsync(PublicKeyPath);
        Assert.Contains("-----BEGIN EC PRIVATE KEY-----", privateKeyPem, StringComparison.Ordinal);
        Assert.Contains("-----BEGIN PUBLIC KEY-----", publicKeyPem, StringComparison.Ordinal);
        using var privateKey = ECDsa.Create();
        privateKey.ImportFromPem(privateKeyPem);
        using var publicKey = ECDsa.Create();
        publicKey.ImportFromPem(publicKeyPem);
        var privateParameters = privateKey.ExportParameters(false);
        var publicParameters = publicKey.ExportParameters(false);
        Assert.Equal(ECCurve.NamedCurves.nistP256.Oid.Value, privateParameters.Curve.Oid.Value);
        Assert.Equal(privateParameters.Q.X, publicParameters.Q.X);
        Assert.Equal(privateParameters.Q.Y, publicParameters.Q.Y);
    }

    private static bool IsOpenSslAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("openssl", "version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process == null)
            {
                return false;
            }
            process.WaitForExit(TimeSpan.FromSeconds(10));
            return process.HasExited && process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }
}
