using System.Net.WebSockets;
using Newtonsoft.Json;

namespace SmartTeslaAmpSetter.Dtos
{
    public class Settings
    {
        public Settings()
        {
            Cars = new();
        }
        public List<Car> Cars { get; set; }
    }

    public class Car
    {
        
        private ChargeMode _chargeMode;

        public Car()
        {
            State = new State();
            UpdatedSincLastWrite = false;
        }
        public int Id { get; set; }
        [JsonIgnore]
        public State State { get; set; }

        public ChargeMode ChargeMode
        {
            get => _chargeMode;
            set
            {
                _chargeMode = value;
                UpdatedSincLastWrite = true;
            }
        }
        [JsonIgnore]
        public bool UpdatedSincLastWrite { get; set; }

        public void ChangeChargeMode()
        {
            ChargeMode = ChargeMode.Next();
        }

    }

    public class State
    {
        public string? Name { get; set; }
        public DateTime ShouldStartChargingSince { get; set; }
        public DateTime ShouldStopChargingSince { get; set; }

        public int SoC { get; set; }
        public int SocLimit { get; set; }
        public string? Geofence { get; set; }
        public TimeSpan TimeUntilFullCharge { get; set; }

        public DateTime FullChargeAtMaxAcSpeed
        {
            get
            {
                var socToCharge = (double)SocLimit - SoC;
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


    }
}