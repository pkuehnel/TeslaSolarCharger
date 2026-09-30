using PkSoftwareService.Custom.Backend.Ble;

namespace TeslaSolarCharger.BleApi.Services.Contracts;

public interface IPairingService
{
    /// <summary>Always creates a new key pair, which replaces the key every car is paired with.</summary>
    Task GenerateKeyPair();

    /// <summary>
    /// Creates the key pair if there is none, and the public key if only that is missing. An existing private key is
    /// never replaced.
    /// </summary>
    Task EnsureKeyPair();

    Task<DtoBleCommandResult> PairCar(string vin, string apiRole, string? adapter);
}
