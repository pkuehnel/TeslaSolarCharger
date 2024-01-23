using Newtonsoft.Json;

namespace TeslaSolarCharger.Server.Dtos.TeslaFleetApi;

public class DtoVehicleDataResult
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("user_id")]
    public long UserId { get; set; }

    [JsonProperty("vehicle_id")]
    public long VehicleId { get; set; }

    [JsonProperty("vin")]
    public string Vin { get; set; }

    [JsonProperty("color")]
    public object Color { get; set; }

    [JsonProperty("access_type")]
    public string AccessType { get; set; }

    [JsonProperty("granular_access")]
    public GranularAccess GranularAccess { get; set; }

    [JsonProperty("tokens")]
    public List<string> Tokens { get; set; }

    [JsonProperty("state")]
    public object State { get; set; }

    [JsonProperty("in_service")]
    public bool InService { get; set; }

    [JsonProperty("id_s")]
    public string IdS { get; set; }

    [JsonProperty("calendar_enabled")]
    public bool CalendarEnabled { get; set; }

    [JsonProperty("api_version")]
    public int ApiVersion { get; set; }

    [JsonProperty("backseat_token")]
    public object BackseatToken { get; set; }

    [JsonProperty("backseat_token_updated_at")]
    public object BackseatTokenUpdatedAt { get; set; }

    [JsonProperty("charge_state")]
    public ChargeState ChargeState { get; set; }

    [JsonProperty("climate_state")]
    public ClimateState ClimateState { get; set; }

    [JsonProperty("drive_state")]
    public DriveState DriveState { get; set; }

    [JsonProperty("gui_settings")]
    public GuiSettings GuiSettings { get; set; }

    [JsonProperty("vehicle_config")]
    public VehicleConfig VehicleConfig { get; set; }

    [JsonProperty("vehicle_state")]
    public VehicleState VehicleState { get; set; }
}

public class ChargeState
{
    [JsonProperty("battery_heater_on")]
    public bool BatteryHeaterOn { get; set; }

    [JsonProperty("battery_level")]
    public int BatteryLevel { get; set; }

    [JsonProperty("battery_range")]
    public double BatteryRange { get; set; }

    [JsonProperty("charge_amps")]
    public int ChargeAmps { get; set; }

    [JsonProperty("charge_current_request")]
    public int ChargeCurrentRequest { get; set; }

    [JsonProperty("charge_current_request_max")]
    public int ChargeCurrentRequestMax { get; set; }

    [JsonProperty("charge_enable_request")]
    public bool ChargeEnableRequest { get; set; }

    [JsonProperty("charge_energy_added")]
    public double ChargeEnergyAdded { get; set; }

    [JsonProperty("charge_limit_soc")]
    public int ChargeLimitSoc { get; set; }

    [JsonProperty("charge_limit_soc_max")]
    public int ChargeLimitSocMax { get; set; }

    [JsonProperty("charge_limit_soc_min")]
    public int ChargeLimitSocMin { get; set; }

    [JsonProperty("charge_limit_soc_std")]
    public int ChargeLimitSocStd { get; set; }

    [JsonProperty("charge_miles_added_ideal")]
    public float ChargeMilesAddedIdeal { get; set; }

    [JsonProperty("charge_miles_added_rated")]
    public float ChargeMilesAddedRated { get; set; }

    [JsonProperty("charge_port_cold_weather_mode")]
    public bool ChargePortColdWeatherMode { get; set; }

    [JsonProperty("charge_port_color")]
    public string ChargePortColor { get; set; }

    [JsonProperty("charge_port_door_open")]
    public bool ChargePortDoorOpen { get; set; }

    [JsonProperty("charge_port_latch")]
    public string ChargePortLatch { get; set; }

    [JsonProperty("charge_rate")]
    public float ChargeRate { get; set; }

    [JsonProperty("charger_actual_current")]
    public int ChargerActualCurrent { get; set; }

    [JsonProperty("charger_phases")]
    public object ChargerPhases { get; set; }

    [JsonProperty("charger_pilot_current")]
    public int ChargerPilotCurrent { get; set; }

    [JsonProperty("charger_power")]
    public int ChargerPower { get; set; }

    [JsonProperty("charger_voltage")]
    public int ChargerVoltage { get; set; }

    [JsonProperty("charging_state")]
    public string ChargingState { get; set; }

    [JsonProperty("conn_charge_cable")]
    public string ConnChargeCable { get; set; }

    [JsonProperty("est_battery_range")]
    public double EstBatteryRange { get; set; }

    [JsonProperty("fast_charger_brand")]
    public string FastChargerBrand { get; set; }

    [JsonProperty("fast_charger_present")]
    public bool FastChargerPresent { get; set; }

    [JsonProperty("fast_charger_type")]
    public string FastChargerType { get; set; }

    [JsonProperty("ideal_battery_range")]
    public double IdealBatteryRange { get; set; }

    [JsonProperty("managed_charging_active")]
    public bool ManagedChargingActive { get; set; }

    [JsonProperty("managed_charging_start_time")]
    public object ManagedChargingStartTime { get; set; }

    [JsonProperty("managed_charging_user_canceled")]
    public bool ManagedChargingUserCanceled { get; set; }

    [JsonProperty("max_range_charge_counter")]
    public int MaxRangeChargeCounter { get; set; }

    [JsonProperty("minutes_to_full_charge")]
    public int MinutesToFullCharge { get; set; }

    [JsonProperty("not_enough_power_to_heat")]
    public object NotEnoughPowerToHeat { get; set; }

    [JsonProperty("off_peak_charging_enabled")]
    public bool OffPeakChargingEnabled { get; set; }

    [JsonProperty("off_peak_charging_times")]
    public string OffPeakChargingTimes { get; set; }

    [JsonProperty("off_peak_hours_end_time")]
    public int OffPeakHoursEndTime { get; set; }

    [JsonProperty("preconditioning_enabled")]
    public bool PreconditioningEnabled { get; set; }

    [JsonProperty("preconditioning_times")]
    public string PreconditioningTimes { get; set; }

    [JsonProperty("scheduled_charging_mode")]
    public string ScheduledChargingMode { get; set; }

    [JsonProperty("scheduled_charging_pending")]
    public bool ScheduledChargingPending { get; set; }

    [JsonProperty("scheduled_charging_start_time")]
    public object ScheduledChargingStartTime { get; set; }

    [JsonProperty("scheduled_departure_time")]
    public int? ScheduledDepartureTime { get; set; }

    [JsonProperty("scheduled_departure_time_minutes")]
    public int? ScheduledDepartureTimeMinutes { get; set; }

    [JsonProperty("supercharger_session_trip_planner")]
    public bool SuperchargerSessionTripPlanner { get; set; }

    [JsonProperty("time_to_full_charge")]
    public float TimeToFullCharge { get; set; }

    [JsonProperty("timestamp")]
    public long Timestamp { get; set; }

    [JsonProperty("trip_charging")]
    public bool TripCharging { get; set; }

    [JsonProperty("usable_battery_level")]
    public int UsableBatteryLevel { get; set; }

    [JsonProperty("user_charge_enable_request")]
    public object UserChargeEnableRequest { get; set; }
}

public class ClimateState
{
    [JsonProperty("allow_cabin_overheat_protection")]
    public bool AllowCabinOverheatProtection { get; set; }

    [JsonProperty("auto_seat_climate_left")]
    public bool AutoSeatClimateLeft { get; set; }

    [JsonProperty("auto_seat_climate_right")]
    public bool AutoSeatClimateRight { get; set; }

    [JsonProperty("auto_steering_wheel_heat")]
    public bool AutoSteeringWheelHeat { get; set; }

    [JsonProperty("battery_heater")]
    public bool BatteryHeater { get; set; }

    [JsonProperty("battery_heater_no_power")]
    public object BatteryHeaterNoPower { get; set; }

    [JsonProperty("bioweapon_mode")]
    public bool BioweaponMode { get; set; }

    [JsonProperty("cabin_overheat_protection")]
    public string CabinOverheatProtection { get; set; }

    [JsonProperty("cabin_overheat_protection_actively_cooling")]
    public bool CabinOverheatProtectionActivelyCooling { get; set; }

    [JsonProperty("climate_keeper_mode")]
    public string ClimateKeeperMode { get; set; }

    [JsonProperty("cop_activation_temperature")]
    public string CopActivationTemperature { get; set; }

    [JsonProperty("defrost_mode")]
    public int DefrostMode { get; set; }

    [JsonProperty("driver_temp_setting")]
    public float DriverTempSetting { get; set; }

    [JsonProperty("fan_status")]
    public int FanStatus { get; set; }

    [JsonProperty("hvac_auto_request")]
    public string HvacAutoRequest { get; set; }

    [JsonProperty("inside_temp")]
    public double InsideTemp { get; set; }

    [JsonProperty("is_auto_conditioning_on")]
    public bool IsAutoConditioningOn { get; set; }

    [JsonProperty("is_climate_on")]
    public bool IsClimateOn { get; set; }

    [JsonProperty("is_front_defroster_on")]
    public bool IsFrontDefrosterOn { get; set; }

    [JsonProperty("is_preconditioning")]
    public bool IsPreconditioning { get; set; }

    [JsonProperty("is_rear_defroster_on")]
    public bool IsRearDefrosterOn { get; set; }

    [JsonProperty("left_temp_direction")]
    public float LeftTempDirection { get; set; }

    [JsonProperty("max_avail_temp")]
    public float MaxAvailTemp { get; set; }

    [JsonProperty("min_avail_temp")]
    public float MinAvailTemp { get; set; }

    [JsonProperty("outside_temp")]
    public float OutsideTemp { get; set; }

    [JsonProperty("passenger_temp_setting")]
    public float PassengerTempSetting { get; set; }

    [JsonProperty("remote_heater_control_enabled")]
    public bool RemoteHeaterControlEnabled { get; set; }

    [JsonProperty("right_temp_direction")]
    public float RightTempDirection { get; set; }

    [JsonProperty("seat_heater_left")]
    public int SeatHeaterLeft { get; set; }

    [JsonProperty("seat_heater_rear_center")]
    public int SeatHeaterRearCenter { get; set; }

    [JsonProperty("seat_heater_rear_left")]
    public int SeatHeaterRearLeft { get; set; }

    [JsonProperty("seat_heater_rear_right")]
    public int SeatHeaterRearRight { get; set; }

    [JsonProperty("seat_heater_right")]
    public int SeatHeaterRight { get; set; }

    [JsonProperty("side_mirror_heaters")]
    public bool SideMirrorHeaters { get; set; }

    [JsonProperty("steering_wheel_heat_level")]
    public int SteeringWheelHeatLevel { get; set; }

    [JsonProperty("steering_wheel_heater")]
    public bool SteeringWheelHeater { get; set; }

    [JsonProperty("supports_fan_only_cabin_overheat_protection")]
    public bool SupportsFanOnlyCabinOverheatProtection { get; set; }

    [JsonProperty("timestamp")]
    public long Timestamp { get; set; }

    [JsonProperty("wiper_blade_heater")]
    public bool WiperBladeHeater { get; set; }
}

public class DriveState
{
    [JsonProperty("active_route_latitude")]
    public double ActiveRouteLatitude { get; set; }

    [JsonProperty("active_route_longitude")]
    public double ActiveRouteLongitude { get; set; }

    [JsonProperty("active_route_traffic_minutes_delay")]
    public float ActiveRouteTrafficMinutesDelay { get; set; }

    [JsonProperty("gps_as_of")]
    public int GpsAsOf { get; set; }

    [JsonProperty("heading")]
    public int Heading { get; set; }

    [JsonProperty("latitude")]
    public double Latitude { get; set; }

    [JsonProperty("longitude")]
    public double Longitude { get; set; }

    [JsonProperty("native_latitude")]
    public double NativeLatitude { get; set; }

    [JsonProperty("native_location_supported")]
    public int NativeLocationSupported { get; set; }

    [JsonProperty("native_longitude")]
    public double NativeLongitude { get; set; }

    [JsonProperty("native_type")]
    public string NativeType { get; set; }

    [JsonProperty("power")]
    public int Power { get; set; }

    [JsonProperty("shift_state")]
    public object ShiftState { get; set; }

    [JsonProperty("speed")]
    public object Speed { get; set; }

    [JsonProperty("timestamp")]
    public long Timestamp { get; set; }
}

public class GuiSettings
{
    [JsonProperty("gui_24_hour_time")]
    public bool Gui24HourTime { get; set; }

    [JsonProperty("gui_charge_rate_units")]
    public string GuiChargeRateUnits { get; set; }

    [JsonProperty("gui_distance_units")]
    public string GuiDistanceUnits { get; set; }

    [JsonProperty("gui_range_display")]
    public string GuiRangeDisplay { get; set; }

    [JsonProperty("gui_temperature_units")]
    public string GuiTemperatureUnits { get; set; }

    [JsonProperty("gui_tirepressure_units")]
    public string GuiTirepressureUnits { get; set; }

    [JsonProperty("show_range_units")]
    public bool ShowRangeUnits { get; set; }

    [JsonProperty("timestamp")]
    public long Timestamp { get; set; }
}

public class MediaInfo
{
    [JsonProperty("a2dp_source_name")]
    public string A2dpSourceName { get; set; }

    [JsonProperty("audio_volume")]
    public double AudioVolume { get; set; }

    [JsonProperty("audio_volume_increment")]
    public double AudioVolumeIncrement { get; set; }

    [JsonProperty("audio_volume_max")]
    public double AudioVolumeMax { get; set; }

    [JsonProperty("media_playback_status")]
    public string MediaPlaybackStatus { get; set; }

    [JsonProperty("now_playing_album")]
    public string NowPlayingAlbum { get; set; }

    [JsonProperty("now_playing_artist")]
    public string NowPlayingArtist { get; set; }

    [JsonProperty("now_playing_duration")]
    public int NowPlayingDuration { get; set; }

    [JsonProperty("now_playing_elapsed")]
    public int NowPlayingElapsed { get; set; }

    [JsonProperty("now_playing_source")]
    public string NowPlayingSource { get; set; }

    [JsonProperty("now_playing_station")]
    public string NowPlayingStation { get; set; }

    [JsonProperty("now_playing_title")]
    public string NowPlayingTitle { get; set; }
}

public class MediaState
{
    [JsonProperty("remote_control_enabled")]
    public bool RemoteControlEnabled { get; set; }
}

public class SoftwareUpdate
{
    [JsonProperty("download_perc")]
    public int DownloadPerc { get; set; }

    [JsonProperty("expected_duration_sec")]
    public int ExpectedDurationSec { get; set; }

    [JsonProperty("install_perc")]
    public int InstallPerc { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonProperty("version")]
    public string Version { get; set; }
}

public class SpeedLimitMode
{
    [JsonProperty("active")]
    public bool Active { get; set; }

    [JsonProperty("current_limit_mph")]
    public float CurrentLimitMph { get; set; }

    [JsonProperty("max_limit_mph")]
    public float MaxLimitMph { get; set; }

    [JsonProperty("min_limit_mph")]
    public float MinLimitMph { get; set; }

    [JsonProperty("pin_code_set")]
    public bool PinCodeSet { get; set; }
}

public class VehicleConfig
{
    [JsonProperty("aux_park_lamps")]
    public string AuxParkLamps { get; set; }

    [JsonProperty("badge_version")]
    public int BadgeVersion { get; set; }

    [JsonProperty("can_accept_navigation_requests")]
    public bool CanAcceptNavigationRequests { get; set; }

    [JsonProperty("can_actuate_trunks")]
    public bool CanActuateTrunks { get; set; }

    [JsonProperty("car_special_type")]
    public string CarSpecialType { get; set; }

    [JsonProperty("car_type")]
    public string CarType { get; set; }

    [JsonProperty("charge_port_type")]
    public string ChargePortType { get; set; }

    [JsonProperty("cop_user_set_temp_supported")]
    public bool CopUserSetTempSupported { get; set; }

    [JsonProperty("dashcam_clip_save_supported")]
    public bool DashcamClipSaveSupported { get; set; }

    [JsonProperty("default_charge_to_max")]
    public bool DefaultChargeToMax { get; set; }

    [JsonProperty("driver_assist")]
    public string DriverAssist { get; set; }

    [JsonProperty("ece_restrictions")]
    public bool EceRestrictions { get; set; }

    [JsonProperty("efficiency_package")]
    public string EfficiencyPackage { get; set; }

    [JsonProperty("eu_vehicle")]
    public bool EuVehicle { get; set; }

    [JsonProperty("exterior_color")]
    public string ExteriorColor { get; set; }

    [JsonProperty("exterior_trim")]
    public string ExteriorTrim { get; set; }

    [JsonProperty("exterior_trim_override")]
    public string ExteriorTrimOverride { get; set; }

    [JsonProperty("has_air_suspension")]
    public bool HasAirSuspension { get; set; }

    [JsonProperty("has_ludicrous_mode")]
    public bool HasLudicrousMode { get; set; }

    [JsonProperty("has_seat_cooling")]
    public bool HasSeatCooling { get; set; }

    [JsonProperty("headlamp_type")]
    public string HeadlampType { get; set; }

    [JsonProperty("interior_trim_type")]
    public string InteriorTrimType { get; set; }

    [JsonProperty("key_version")]
    public int KeyVersion { get; set; }

    [JsonProperty("motorized_charge_port")]
    public bool MotorizedChargePort { get; set; }

    [JsonProperty("paint_color_override")]
    public string PaintColorOverride { get; set; }

    [JsonProperty("performance_package")]
    public string PerformancePackage { get; set; }

    [JsonProperty("plg")]
    public bool Plg { get; set; }

    [JsonProperty("pws")]
    public bool Pws { get; set; }

    [JsonProperty("rear_drive_unit")]
    public string RearDriveUnit { get; set; }

    [JsonProperty("rear_seat_heaters")]
    public int RearSeatHeaters { get; set; }

    [JsonProperty("rear_seat_type")]
    public int RearSeatType { get; set; }

    [JsonProperty("rhd")]
    public bool Rhd { get; set; }

    [JsonProperty("roof_color")]
    public string RoofColor { get; set; }

    [JsonProperty("seat_type")]
    public object SeatType { get; set; }

    [JsonProperty("spoiler_type")]
    public string SpoilerType { get; set; }

    [JsonProperty("sun_roof_installed")]
    public object SunRoofInstalled { get; set; }

    [JsonProperty("supports_qr_pairing")]
    public bool SupportsQrPairing { get; set; }

    [JsonProperty("third_row_seats")]
    public string ThirdRowSeats { get; set; }

    [JsonProperty("timestamp")]
    public long Timestamp { get; set; }

    [JsonProperty("trim_badging")]
    public string TrimBadging { get; set; }

    [JsonProperty("use_range_badging")]
    public bool UseRangeBadging { get; set; }

    [JsonProperty("utc_offset")]
    public int UtcOffset { get; set; }

    [JsonProperty("webcam_selfie_supported")]
    public bool WebcamSelfieSupported { get; set; }

    [JsonProperty("webcam_supported")]
    public bool WebcamSupported { get; set; }

    [JsonProperty("wheel_type")]
    public string WheelType { get; set; }
}

public class VehicleState
{
    [JsonProperty("api_version")]
    public int ApiVersion { get; set; }

    [JsonProperty("autopark_state_v3")]
    public string AutoparkStateV3 { get; set; }

    [JsonProperty("autopark_style")]
    public string AutoparkStyle { get; set; }

    [JsonProperty("calendar_supported")]
    public bool CalendarSupported { get; set; }

    [JsonProperty("car_version")]
    public string CarVersion { get; set; }

    [JsonProperty("center_display_state")]
    public int CenterDisplayState { get; set; }

    [JsonProperty("dashcam_clip_save_available")]
    public bool DashcamClipSaveAvailable { get; set; }

    [JsonProperty("dashcam_state")]
    public string DashcamState { get; set; }

    [JsonProperty("df")]
    public int Df { get; set; }

    [JsonProperty("dr")]
    public int Dr { get; set; }

    [JsonProperty("fd_window")]
    public int FdWindow { get; set; }

    [JsonProperty("feature_bitmask")]
    public string FeatureBitmask { get; set; }

    [JsonProperty("fp_window")]
    public int FpWindow { get; set; }

    [JsonProperty("ft")]
    public int Ft { get; set; }

    [JsonProperty("homelink_device_count")]
    public int HomelinkDeviceCount { get; set; }

    [JsonProperty("homelink_nearby")]
    public bool HomelinkNearby { get; set; }

    [JsonProperty("is_user_present")]
    public bool IsUserPresent { get; set; }

    [JsonProperty("last_autopark_error")]
    public string LastAutoparkError { get; set; }

    [JsonProperty("locked")]
    public bool Locked { get; set; }

    [JsonProperty("media_info")]
    public MediaInfo MediaInfo { get; set; }

    [JsonProperty("media_state")]
    public MediaState MediaState { get; set; }

    [JsonProperty("notifications_supported")]
    public bool NotificationsSupported { get; set; }

    [JsonProperty("odometer")]
    public double Odometer { get; set; }

    [JsonProperty("parsed_calendar_supported")]
    public bool ParsedCalendarSupported { get; set; }

    [JsonProperty("pf")]
    public int Pf { get; set; }

    [JsonProperty("pr")]
    public int Pr { get; set; }

    [JsonProperty("rd_window")]
    public int RdWindow { get; set; }

    [JsonProperty("remote_start")]
    public bool RemoteStart { get; set; }

    [JsonProperty("remote_start_enabled")]
    public bool RemoteStartEnabled { get; set; }

    [JsonProperty("remote_start_supported")]
    public bool RemoteStartSupported { get; set; }

    [JsonProperty("rp_window")]
    public int RpWindow { get; set; }

    [JsonProperty("rt")]
    public int Rt { get; set; }

    [JsonProperty("santa_mode")]
    public int SantaMode { get; set; }

    [JsonProperty("sentry_mode")]
    public bool SentryMode { get; set; }

    [JsonProperty("sentry_mode_available")]
    public bool SentryModeAvailable { get; set; }

    [JsonProperty("service_mode")]
    public bool ServiceMode { get; set; }

    [JsonProperty("service_mode_plus")]
    public bool ServiceModePlus { get; set; }

    [JsonProperty("smart_summon_available")]
    public bool SmartSummonAvailable { get; set; }

    [JsonProperty("software_update")]
    public SoftwareUpdate SoftwareUpdate { get; set; }

    [JsonProperty("speed_limit_mode")]
    public SpeedLimitMode SpeedLimitMode { get; set; }

    [JsonProperty("summon_standby_mode_enabled")]
    public bool SummonStandbyModeEnabled { get; set; }

    [JsonProperty("timestamp")]
    public long Timestamp { get; set; }

    [JsonProperty("tpms_hard_warning_fl")]
    public bool TpmsHardWarningFl { get; set; }

    [JsonProperty("tpms_hard_warning_fr")]
    public bool TpmsHardWarningFr { get; set; }

    [JsonProperty("tpms_hard_warning_rl")]
    public bool TpmsHardWarningRl { get; set; }

    [JsonProperty("tpms_hard_warning_rr")]
    public bool TpmsHardWarningRr { get; set; }

    [JsonProperty("tpms_last_seen_pressure_time_fl")]
    public int TpmsLastSeenPressureTimeFl { get; set; }

    [JsonProperty("tpms_last_seen_pressure_time_fr")]
    public int TpmsLastSeenPressureTimeFr { get; set; }

    [JsonProperty("tpms_last_seen_pressure_time_rl")]
    public int TpmsLastSeenPressureTimeRl { get; set; }

    [JsonProperty("tpms_last_seen_pressure_time_rr")]
    public int TpmsLastSeenPressureTimeRr { get; set; }

    [JsonProperty("tpms_pressure_fl")]
    public float TpmsPressureFl { get; set; }

    [JsonProperty("tpms_pressure_fr")]
    public float TpmsPressureFr { get; set; }

    [JsonProperty("tpms_pressure_rl")]
    public float TpmsPressureRl { get; set; }

    [JsonProperty("tpms_pressure_rr")]
    public float TpmsPressureRr { get; set; }

    [JsonProperty("tpms_rcp_front_value")]
    public float TpmsRcpFrontValue { get; set; }

    [JsonProperty("tpms_rcp_rear_value")]
    public float TpmsRcpRearValue { get; set; }

    [JsonProperty("tpms_soft_warning_fl")]
    public bool TpmsSoftWarningFl { get; set; }

    [JsonProperty("tpms_soft_warning_fr")]
    public bool TpmsSoftWarningFr { get; set; }

    [JsonProperty("tpms_soft_warning_rl")]
    public bool TpmsSoftWarningRl { get; set; }

    [JsonProperty("tpms_soft_warning_rr")]
    public bool TpmsSoftWarningRr { get; set; }

    [JsonProperty("valet_mode")]
    public bool ValetMode { get; set; }

    [JsonProperty("valet_pin_needed")]
    public bool ValetPinNeeded { get; set; }

    [JsonProperty("vehicle_name")]
    public string VehicleName { get; set; }

    [JsonProperty("vehicle_self_test_progress")]
    public int VehicleSelfTestProgress { get; set; }

    [JsonProperty("vehicle_self_test_requested")]
    public bool VehicleSelfTestRequested { get; set; }

    [JsonProperty("webcam_available")]
    public bool WebcamAvailable { get; set; }
}
