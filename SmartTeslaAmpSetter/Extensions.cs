using SmartTeslaAmpSetter.Dtos;

namespace SmartTeslaAmpSetter;

public static class Extensions
{
    public static T Next<T>(this T src) where T : struct
    {
        if (!typeof(T).IsEnum) throw new ArgumentException(String.Format("Argument {0} is not an Enum", typeof(T).FullName));

        T[] Arr = (T[])Enum.GetValues(src.GetType());
        int j = Array.IndexOf<T>(Arr, src) + 1;
        return (Arr.Length==j) ? Arr[0] : Arr[j];
    }

    public static string ToFriendlyString(this ChargeMode chargeMode)
    {
        switch (chargeMode)
        {
            case ChargeMode.PvOnly:
                return "Nur PV";
            case ChargeMode.MaxPower:
                return "Maximale Leistung";
            case ChargeMode.PvAndMinSoc:
                return "Min SoC + PV";
            default:
                return chargeMode.ToString();
        }
    }
}