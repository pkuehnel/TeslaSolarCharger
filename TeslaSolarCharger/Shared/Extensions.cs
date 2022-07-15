using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared;

public static class Extensions
{
    public static T Next<T>(this T src) where T : struct
    {
        if (!typeof(T).IsEnum) throw new ArgumentException(String.Format("Argument {0} is not an Enum", typeof(T).FullName));

        T[] arr = (T[])Enum.GetValues(src.GetType());
        int j = Array.IndexOf(arr, src) + 1;
        return (arr.Length==j) ? arr[0] : arr[j];
    }

    public static string ToFriendlyString(this ChargeMode chargeMode)
    {
        switch (chargeMode)
        {
            case ChargeMode.PvOnly:
                return "PV Only";
            case ChargeMode.MaxPower:
                return "Maximum Power";
            case ChargeMode.PvAndMinSoc:
                return "Min SoC + PV";
            default:
                return chargeMode.ToString();
        }
    }
}