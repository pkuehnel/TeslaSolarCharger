using TeslaSolarCharger.BleApi.Dtos;

namespace TeslaSolarCharger.BleApi.Services.Contracts;

public interface IPairingService
{
    Task GenerateKeyPair();
    Task<DtoBleCommandResult> PairCar(string vin, string apiRole);
}