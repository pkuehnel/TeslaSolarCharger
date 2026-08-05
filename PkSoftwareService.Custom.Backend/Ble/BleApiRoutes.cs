namespace PkSoftwareService.Custom.Backend.Ble;

/// <summary>
/// Route and query parameter names of the BLE container API. The container's controllers use convention based
/// routing ("api/[controller]/[action]"); tests on the container side assert these constants match the controller
/// and action names so the two can never drift apart.
/// </summary>
public static class BleApiRoutes
{
    public const string ExecuteCommand = "Command/ExecuteCommand";
    /// <summary>
    /// Replaces the removed Command/BeaconScan: presence is answered from the permanent background scan instead of
    /// by listening for a window.
    /// </summary>
    public const string Presence = "Presence/Get";
    public const string BeaconScan = "Command/BeaconScan";
    public const string ListCommands = "Command/ListCommands";
    public const string AdapterList = "Adapter/List";
    public const string TscVersionCompatibility = "Hello/TscVersionCompatibility";
    public const string PairCar = "Pairing/PairCar";
    public const string GenerateKeyPair = "Pairing/GenerateKeyPair";
    public const string DownloadInMemoryLogs = "Debug/DownloadInMemoryLogs";

    public const string VinQueryParam = "vin";
    public const string CommandQueryParam = "command";
    public const string DomainQueryParam = "domain";
    public const string KeepWarmSecondsQueryParam = "keepWarmSeconds";
    public const string VinsQueryParam = "vins";
    public const string MaxAgeSecondsQueryParam = "maxAgeSeconds";
    public const string WindowMsQueryParam = "windowMs";
    public const string AdapterQueryParam = "adapter";
    public const string ApiRoleQueryParam = "apiRole";
    public const string UseDebugQueryParam = "useDebug";
}
