namespace TeslaSolarCharger.Shared.Localization;

public static class LocalizationKeys
{
    public static class BaseConfiguration
    {
        public const string HomeBatteryPowerInversionUrl_DisplayName = "BaseConfiguration.HomeBatteryPowerInversionUrl.DisplayName";
        public const string HomeBatteryPowerInversionUrl_HelperText = "BaseConfiguration.HomeBatteryPowerInversionUrl.HelperText";
        public const string UpdateIntervalSeconds_DisplayName = "BaseConfiguration.UpdateIntervalSeconds.DisplayName";
        public const string UpdateIntervalSeconds_HelperText = "BaseConfiguration.UpdateIntervalSeconds.HelperText";
        public const string SkipPowerChangesOnLastAdjustmentNewerThanSeconds_HelperText = "BaseConfiguration.SkipPowerChangesOnLastAdjustmentNewerThanSeconds.HelperText";
        public const string PvValueUpdateIntervalSeconds_DisplayName = "BaseConfiguration.PvValueUpdateIntervalSeconds.DisplayName";
        public const string MinutesUntilSwitchOn_DisplayName = "BaseConfiguration.MinutesUntilSwitchOn.DisplayName";
        public const string MinutesUntilSwitchOff_DisplayName = "BaseConfiguration.MinutesUntilSwitchOff.DisplayName";
        public const string PowerBuffer_DisplayName = "BaseConfiguration.PowerBuffer.DisplayName";
        public const string PowerBuffer_HelperText = "BaseConfiguration.PowerBuffer.HelperText";
        public const string AllowPowerBufferChangeOnHome_HelperText = "BaseConfiguration.AllowPowerBufferChangeOnHome.HelperText";
        public const string PredictSolarPowerGeneration_HelperText = "BaseConfiguration.PredictSolarPowerGeneration.HelperText";
        public const string UsePredictedSolarPowerGenerationForChargingSchedules_HelperText = "BaseConfiguration.UsePredictedSolarPowerGenerationForChargingSchedules.HelperText";
        public const string ShowEnergyDataOnHome_HelperText = "BaseConfiguration.ShowEnergyDataOnHome.HelperText";
        public const string TelegramBotKey_DisplayName = "BaseConfiguration.TelegramBotKey.DisplayName";
        public const string TelegramChannelId_DisplayName = "BaseConfiguration.TelegramChannelId.DisplayName";
        public const string SendStackTraceToTelegram_HelperText = "BaseConfiguration.SendStackTraceToTelegram.HelperText";
        public const string TeslaMateDbServer_DisplayName = "BaseConfiguration.TeslaMateDbServer.DisplayName";
        public const string TeslaMateDbPort_DisplayName = "BaseConfiguration.TeslaMateDbPort.DisplayName";
        public const string TeslaMateDbPort_HelperText = "BaseConfiguration.TeslaMateDbPort.HelperText";
        public const string TeslaMateDbDatabaseName_DisplayName = "BaseConfiguration.TeslaMateDbDatabaseName.DisplayName";
        public const string TeslaMateDbUser_DisplayName = "BaseConfiguration.TeslaMateDbUser.DisplayName";
        public const string TeslaMateDbPassword_DisplayName = "BaseConfiguration.TeslaMateDbPassword.DisplayName";
        public const string MosquitoServer_DisplayName = "BaseConfiguration.MosquitoServer.DisplayName";
        public const string MqqtClientId_DisplayName = "BaseConfiguration.MqqtClientId.DisplayName";
        public const string DynamicHomeBatteryMinSoc_DisplayName = "BaseConfiguration.DynamicHomeBatteryMinSoc.DisplayName";
        public const string DynamicHomeBatteryMinSoc_HelperText = "BaseConfiguration.DynamicHomeBatteryMinSoc.HelperText";
        public const string HomeBatteryMinSoc_DisplayName = "BaseConfiguration.HomeBatteryMinSoc.DisplayName";
        public const string HomeBatteryMinSoc_HelperText = "BaseConfiguration.HomeBatteryMinSoc.HelperText";
        public const string HomeBatteryMinDynamicMinSoc_HelperText = "BaseConfiguration.HomeBatteryMinDynamicMinSoc.HelperText";
        public const string HomeBatteryMaxDynamicMinSoc_HelperText = "BaseConfiguration.HomeBatteryMaxDynamicMinSoc.HelperText";
        public const string DynamicMinSocCalculationBuffer_HelperText = "BaseConfiguration.DynamicMinSocCalculationBuffer.HelperText";
        public const string ForceFullHomeBatteryBySunset_HelperText = "BaseConfiguration.ForceFullHomeBatteryBySunset.HelperText";
        public const string HomeBatteryChargingPower_DisplayName = "BaseConfiguration.HomeBatteryChargingPower.DisplayName";
        public const string HomeBatteryChargingPower_HelperText = "BaseConfiguration.HomeBatteryChargingPower.HelperText";
        public const string HomeBatteryDischargingPower_DisplayName = "BaseConfiguration.HomeBatteryDischargingPower.DisplayName";
        public const string HomeBatteryDischargingPower_HelperText = "BaseConfiguration.HomeBatteryDischargingPower.HelperText";
        public const string HomeBatteryUsableEnergy_DisplayName = "BaseConfiguration.HomeBatteryUsableEnergy.DisplayName";
        public const string HomeBatteryUsableEnergy_HelperText = "BaseConfiguration.HomeBatteryUsableEnergy.HelperText";
        public const string DischargeHomeBatteryToMinSocDuringDay_HelperText = "BaseConfiguration.DischargeHomeBatteryToMinSocDuringDay.HelperText";
        public const string CarChargeLoss_HelperText = "BaseConfiguration.CarChargeLoss.HelperText";
        public const string MaxCombinedCurrent_DisplayName = "BaseConfiguration.MaxCombinedCurrent.DisplayName";
        public const string MaxCombinedCurrent_HelperText = "BaseConfiguration.MaxCombinedCurrent.HelperText";
        public const string MaxInverterAcPower_DisplayName = "BaseConfiguration.MaxInverterAcPower.DisplayName";
        public const string MaxInverterAcPower_HelperText = "BaseConfiguration.MaxInverterAcPower.HelperText";
        public const string UseTeslaMateIntegration_DisplayName = "BaseConfiguration.UseTeslaMateIntegration.DisplayName";
        public const string UseTeslaMateIntegration_HelperText = "BaseConfiguration.UseTeslaMateIntegration.HelperText";
        public const string UseTeslaMateAsDataSource_DisplayName = "BaseConfiguration.UseTeslaMateAsDataSource.DisplayName";
        public const string UseTeslaMateAsDataSource_HelperText = "BaseConfiguration.UseTeslaMateAsDataSource.HelperText";
        public const string HomeGeofenceRadius_DisplayName = "BaseConfiguration.HomeGeofenceRadius.DisplayName";
        public const string HomeGeofenceRadius_HelperText = "BaseConfiguration.HomeGeofenceRadius.HelperText";

        public static readonly LocalizationKey HomeBatteryPowerInversionUrl_DisplayNameKey = new(HomeBatteryPowerInversionUrl_DisplayName);
        public static readonly LocalizationKey HomeBatteryPowerInversionUrl_HelperTextKey = new(HomeBatteryPowerInversionUrl_HelperText);
        public static readonly LocalizationKey UpdateIntervalSeconds_DisplayNameKey = new(UpdateIntervalSeconds_DisplayName);
        public static readonly LocalizationKey UpdateIntervalSeconds_HelperTextKey = new(UpdateIntervalSeconds_HelperText);
        public static readonly LocalizationKey SkipPowerChangesOnLastAdjustmentNewerThanSeconds_HelperTextKey = new(SkipPowerChangesOnLastAdjustmentNewerThanSeconds_HelperText);
        public static readonly LocalizationKey PvValueUpdateIntervalSeconds_DisplayNameKey = new(PvValueUpdateIntervalSeconds_DisplayName);
        public static readonly LocalizationKey MinutesUntilSwitchOn_DisplayNameKey = new(MinutesUntilSwitchOn_DisplayName);
        public static readonly LocalizationKey MinutesUntilSwitchOff_DisplayNameKey = new(MinutesUntilSwitchOff_DisplayName);
        public static readonly LocalizationKey PowerBuffer_DisplayNameKey = new(PowerBuffer_DisplayName);
        public static readonly LocalizationKey PowerBuffer_HelperTextKey = new(PowerBuffer_HelperText);
        public static readonly LocalizationKey AllowPowerBufferChangeOnHome_HelperTextKey = new(AllowPowerBufferChangeOnHome_HelperText);
        public static readonly LocalizationKey PredictSolarPowerGeneration_HelperTextKey = new(PredictSolarPowerGeneration_HelperText);
        public static readonly LocalizationKey UsePredictedSolarPowerGenerationForChargingSchedules_HelperTextKey = new(UsePredictedSolarPowerGenerationForChargingSchedules_HelperText);
        public static readonly LocalizationKey ShowEnergyDataOnHome_HelperTextKey = new(ShowEnergyDataOnHome_HelperText);
        public static readonly LocalizationKey TelegramBotKey_DisplayNameKey = new(TelegramBotKey_DisplayName);
        public static readonly LocalizationKey TelegramChannelId_DisplayNameKey = new(TelegramChannelId_DisplayName);
        public static readonly LocalizationKey SendStackTraceToTelegram_HelperTextKey = new(SendStackTraceToTelegram_HelperText);
        public static readonly LocalizationKey TeslaMateDbServer_DisplayNameKey = new(TeslaMateDbServer_DisplayName);
        public static readonly LocalizationKey TeslaMateDbPort_DisplayNameKey = new(TeslaMateDbPort_DisplayName);
        public static readonly LocalizationKey TeslaMateDbPort_HelperTextKey = new(TeslaMateDbPort_HelperText);
        public static readonly LocalizationKey TeslaMateDbDatabaseName_DisplayNameKey = new(TeslaMateDbDatabaseName_DisplayName);
        public static readonly LocalizationKey TeslaMateDbUser_DisplayNameKey = new(TeslaMateDbUser_DisplayName);
        public static readonly LocalizationKey TeslaMateDbPassword_DisplayNameKey = new(TeslaMateDbPassword_DisplayName);
        public static readonly LocalizationKey MosquitoServer_DisplayNameKey = new(MosquitoServer_DisplayName);
        public static readonly LocalizationKey MqqtClientId_DisplayNameKey = new(MqqtClientId_DisplayName);
        public static readonly LocalizationKey DynamicHomeBatteryMinSoc_DisplayNameKey = new(DynamicHomeBatteryMinSoc_DisplayName);
        public static readonly LocalizationKey DynamicHomeBatteryMinSoc_HelperTextKey = new(DynamicHomeBatteryMinSoc_HelperText);
        public static readonly LocalizationKey HomeBatteryMinSoc_DisplayNameKey = new(HomeBatteryMinSoc_DisplayName);
        public static readonly LocalizationKey HomeBatteryMinSoc_HelperTextKey = new(HomeBatteryMinSoc_HelperText);
        public static readonly LocalizationKey HomeBatteryMinDynamicMinSoc_HelperTextKey = new(HomeBatteryMinDynamicMinSoc_HelperText);
        public static readonly LocalizationKey HomeBatteryMaxDynamicMinSoc_HelperTextKey = new(HomeBatteryMaxDynamicMinSoc_HelperText);
        public static readonly LocalizationKey DynamicMinSocCalculationBuffer_HelperTextKey = new(DynamicMinSocCalculationBuffer_HelperText);
        public static readonly LocalizationKey ForceFullHomeBatteryBySunset_HelperTextKey = new(ForceFullHomeBatteryBySunset_HelperText);
        public static readonly LocalizationKey HomeBatteryChargingPower_DisplayNameKey = new(HomeBatteryChargingPower_DisplayName);
        public static readonly LocalizationKey HomeBatteryChargingPower_HelperTextKey = new(HomeBatteryChargingPower_HelperText);
        public static readonly LocalizationKey HomeBatteryDischargingPower_DisplayNameKey = new(HomeBatteryDischargingPower_DisplayName);
        public static readonly LocalizationKey HomeBatteryDischargingPower_HelperTextKey = new(HomeBatteryDischargingPower_HelperText);
        public static readonly LocalizationKey HomeBatteryUsableEnergy_DisplayNameKey = new(HomeBatteryUsableEnergy_DisplayName);
        public static readonly LocalizationKey HomeBatteryUsableEnergy_HelperTextKey = new(HomeBatteryUsableEnergy_HelperText);
        public static readonly LocalizationKey DischargeHomeBatteryToMinSocDuringDay_HelperTextKey = new(DischargeHomeBatteryToMinSocDuringDay_HelperText);
        public static readonly LocalizationKey CarChargeLoss_HelperTextKey = new(CarChargeLoss_HelperText);
        public static readonly LocalizationKey MaxCombinedCurrent_DisplayNameKey = new(MaxCombinedCurrent_DisplayName);
        public static readonly LocalizationKey MaxCombinedCurrent_HelperTextKey = new(MaxCombinedCurrent_HelperText);
        public static readonly LocalizationKey MaxInverterAcPower_DisplayNameKey = new(MaxInverterAcPower_DisplayName);
        public static readonly LocalizationKey MaxInverterAcPower_HelperTextKey = new(MaxInverterAcPower_HelperText);
        public static readonly LocalizationKey UseTeslaMateIntegration_DisplayNameKey = new(UseTeslaMateIntegration_DisplayName);
        public static readonly LocalizationKey UseTeslaMateIntegration_HelperTextKey = new(UseTeslaMateIntegration_HelperText);
        public static readonly LocalizationKey UseTeslaMateAsDataSource_DisplayNameKey = new(UseTeslaMateAsDataSource_DisplayName);
        public static readonly LocalizationKey UseTeslaMateAsDataSource_HelperTextKey = new(UseTeslaMateAsDataSource_HelperText);
        public static readonly LocalizationKey HomeGeofenceRadius_DisplayNameKey = new(HomeGeofenceRadius_DisplayName);
        public static readonly LocalizationKey HomeGeofenceRadius_HelperTextKey = new(HomeGeofenceRadius_HelperText);
    }

    public static class NotChargingReasons
    {
        public const string CarFullyCharged = "NotChargingReasons.CarFullyCharged";
        public static readonly LocalizationKey CarFullyChargedKey = new(CarFullyCharged);
    }

    public static class Components
    {
        public static class NotChargingReasons
        {
            public const string Heading = "Components.NotChargingReasons.Heading";
            public const string Remaining = "Components.NotChargingReasons.Remaining";
            public const string ReasonWithCountdown = "Components.NotChargingReasons.ReasonWithCountdown";
            public const string NextEnding = "Components.NotChargingReasons.NextEnding";

            public static readonly LocalizationKey HeadingKey = new(Heading);
            public static readonly LocalizationKey RemainingKey = new(Remaining);
            public static readonly LocalizationKey ReasonWithCountdownKey = new(ReasonWithCountdown);
            public static readonly LocalizationKey NextEndingKey = new(NextEnding);
        }
    }
}
