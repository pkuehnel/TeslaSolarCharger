namespace TeslaSolarCharger.Server.Dtos.Solar4CarBackend;

public abstract class RedeemTargetActionPayload
{
    protected RedeemTargetActionPayload(string localRedirectUrl)
    {
        LocalRedirectUrl = localRedirectUrl;
    }

    public string LocalRedirectUrl { get; set; }
}

public class RedeemTargetActionPayloadTeslaAuthentication : RedeemTargetActionPayload
{
    public RedeemTargetActionPayloadTeslaAuthentication(string encryptionKey, string localRedirectUrl) : base(localRedirectUrl)
    {
        EncryptionKey = encryptionKey;
    }

    public string EncryptionKey { get; set; }
}

public class RedeemTargetActionPayloadSmartCarAuthentication : RedeemTargetActionPayload
{
    public RedeemTargetActionPayloadSmartCarAuthentication(string localRedirectUrl, string? vin) : base(localRedirectUrl)
    {
        Vin = vin;
    }

    // Null when adding a new SmartCar via the wizard (vehicle is picked in SmartCar's panel),
    // non-null when connecting an already-added car to a specific VIN.
    public string? Vin { get; set; }
}
