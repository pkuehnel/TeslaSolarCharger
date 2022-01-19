using System.Text.Json.Serialization;

namespace SmartTeslaAmpSetter.Dtos.TeslaMate
{
    public class Car
    {
        public int car_id { get; set; }
        public string car_name { get; set; }
    }

    public class CarStatus
    {
        public bool healthy { get; set; }
        public bool locked { get; set; }
        public bool sentry_mode { get; set; }
        public bool windows_open { get; set; }
        public bool doors_open { get; set; }
        public bool trunk_open { get; set; }
        public bool frunk_open { get; set; }
        public bool is_user_present { get; set; }
    }

    public class CarDetails
    {
        public string model { get; set; }
        public string trim_badging { get; set; }
    }

    public class CarExterior
    {
        public string exterior_color { get; set; }
        public string spoiler_type { get; set; }
        public string wheel_type { get; set; }
    }

    public class CarGeodata
    {
        public string geofence { get; set; }
        public double latitude { get; set; }
        public double longitude { get; set; }
    }

    public class CarVersions
    {
        public string version { get; set; }
        public bool update_available { get; set; }
        public string update_version { get; set; }
    }

    public class DrivingDetails
    {
        public string shift_state { get; set; }
        public int power { get; set; }
        public int speed { get; set; }
        public int heading { get; set; }
        public int elevation { get; set; }
    }

    public class ClimateDetails
    {
        public bool is_climate_on { get; set; }
        public double inside_temp { get; set; }
        public double outside_temp { get; set; }
        public bool is_preconditioning { get; set; }
    }

    public class BatteryDetails
    {
        public double est_battery_range { get; set; }
        public double rated_battery_range { get; set; }
        public double ideal_battery_range { get; set; }
        public int battery_level { get; set; }
        public int usable_battery_level { get; set; }
    }

    public class ChargingDetails
    {
        public bool plugged_in { get; set; }
        public double charge_energy_added { get; set; }
        public int charge_limit_soc { get; set; }
        public bool charge_port_door_open { get; set; }
        public int charger_actual_current { get; set; }
        public int charger_phases { get; set; }
        public int charger_power { get; set; }
        public int charger_voltage { get; set; }
        public DateTime scheduled_charging_start_time { get; set; }
        public double time_to_full_charge { get; set; }

        [JsonIgnore]
        public int ChargingPower {
            get
            {
                var phases = charger_phases > 1 ? 3 : 1;
                var power = charger_actual_current * charger_voltage * phases;
                return power;
            }

        }
    }

    public class Status
    {
        public string display_name { get; set; }
        public string state { get; set; }
        public DateTime state_since { get; set; }
        public double odometer { get; set; }
        public CarStatus car_status { get; set; }
        public CarDetails car_details { get; set; }
        public CarExterior car_exterior { get; set; }
        public CarGeodata car_geodata { get; set; }
        public CarVersions car_versions { get; set; }
        public DrivingDetails driving_details { get; set; }
        public ClimateDetails climate_details { get; set; }
        public BatteryDetails battery_details { get; set; }
        public ChargingDetails charging_details { get; set; }
    }

    public class Units
    {
        public string unit_of_length { get; set; }
        public string unit_of_temperature { get; set; }
    }

    public class Data
    {
        public Car car { get; set; }
        public Status status { get; set; }
        public Units units { get; set; }
    }

    public class TeslaMateState
    {
        public Data data { get; set; }
    }
}