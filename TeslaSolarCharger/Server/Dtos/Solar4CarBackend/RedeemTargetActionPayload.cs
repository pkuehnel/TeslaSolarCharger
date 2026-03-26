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
