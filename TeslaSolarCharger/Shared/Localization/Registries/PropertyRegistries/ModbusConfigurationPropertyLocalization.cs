using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;

namespace TeslaSolarCharger.Shared.Localization.Registries.PropertyRegistries;

public class ModbusConfigurationPropertyLocalization : PropertyLocalizationRegistry<DtoModbusConfiguration>
{
    protected override void Configure()
    {
        Register(x => x.UnitIdentifier,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Unit Identifier", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Unit Identifier", null));

        Register(x => x.Host,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Host", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Host", null));

        Register(x => x.Port,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Port", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Port", null));

        Register(x => x.Endianess,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Endianess", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Endianness", null));

        Register(x => x.ConnectDelayMilliseconds,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Connect Delay Milliseconds", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Verbindungsverzögerung (ms)", null));

        Register(x => x.ReadTimeoutMilliseconds,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Read Timeout Milliseconds", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Lese-Timeout (ms)", null));
    }
}
