using System.Net.WebSockets;

namespace SmartTeslaAmpSetter.Dtos
{
    public class Settings
    {
        public Settings()
        {
            Cars = new List<Car>();
        }
        public List<Car> Cars { get; set; }
    }

    public class Car
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime ShouldStartChargingSince { get; set; }
        public DateTime ShouldStopChargingSince { get; set; }
        public ChargeMode ChargeMode { get; set; }
        public int SoC { get; set; }
        public int SocLimit { get; set; }
        public string Geofence { get; set; }
        public TimeSpan TimeUntilFullCharge { get; set; }

        public DateTime FullChargeAtMaxAcSpeed
        {
            get
            {
                var socToCharge = (double) SocLimit - SoC;
                if (socToCharge < 0)
                {
                    return DateTime.UtcNow + TimeSpan.Zero;
                }

                return DateTime.UtcNow + TimeSpan.FromHours(socToCharge / 15);
            }
        }

        public DateTime LatestDateToReachMinimumSoC { get; set; }
        public int MinimumSoC { get; set; }
        public int LastSetAmp { get; set; }

        public void ChangeChargeMode()
        {
            ChargeMode = ChargeMode.Next();
        }
    }
}