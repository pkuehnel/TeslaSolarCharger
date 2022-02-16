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
        private int _minimumSoC;

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

        public int MinimumSoC
        {
            get => _minimumSoC;
            set
            {
                _minimumSoC = value;
                UpdatedSincLastWrite = true;
            }
        }

        [JsonIgnore]
        public int LatestHourToReachSoC 
        {
            get => LatestTimeToReachSoC.Hour;
            set
            {
                var date = LatestTimeToReachSoC.Date;
                var minute = LatestTimeToReachSoC.Minute;
                LatestTimeToReachSoC = date.AddHours(value).AddMinutes(minute);
            }
        }
        [JsonIgnore]
        public int LatestMinuteToReachSoC
        {
            get => LatestTimeToReachSoC.Minute;
            set
            {
                var date = LatestTimeToReachSoC.Date;
                var hour = LatestTimeToReachSoC.Hour;
                LatestTimeToReachSoC = date.AddHours(hour).AddMinutes(value);
            }
        }

        public DateTime LatestTimeToReachSoC { get; set; }

        [JsonIgnore]
        public bool UpdatedSincLastWrite { get; set; }

        public void ChangeChargeMode()
        {
            ChargeMode = ChargeMode.Next();
        }

        public void UpdateMinimumSoc(int minimumSoc, int hour, int minute)
        {
            MinimumSoC = minimumSoc;
            LatestHourToReachSoC = hour;
            LatestMinuteToReachSoC = minute;
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
                    return DateTime.Now + TimeSpan.Zero;
                }

                return DateTime.Now + TimeSpan.FromHours(socToCharge / 15);
            }
        }

        public DateTime LatestDateToReachMinimumSoC { get; set; }
        public int MinimumSoC { get; set; }
        public int LastSetAmp { get; set; }


    }
}