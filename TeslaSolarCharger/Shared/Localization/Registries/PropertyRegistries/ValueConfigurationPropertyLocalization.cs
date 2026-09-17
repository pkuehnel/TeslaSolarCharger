using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;

namespace TeslaSolarCharger.Shared.Localization.Registries.PropertyRegistries;

/// <summary>
/// Registered for the base type, so the values every result configuration shares are named the same way in the REST,
/// MQTT and Modbus dialogs (the lookup walks up the base types).
/// </summary>
public class ValueConfigurationBasePropertyLocalization : PropertyLocalizationRegistry<ValueConfigurationBase>
{
    protected override void Configure()
    {
        Register(x => x.CorrectionFactor,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Correction Factor", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Korrekturfaktor", null));
    }
}

public class JsonXmlResultConfigurationPropertyLocalization : PropertyLocalizationRegistry<DtoJsonXmlResultConfiguration>
{
    protected override void Configure()
    {
        Register(x => x.NodePattern,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Path to value", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Pfad zum Wert", null));

        Register(x => x.XmlAttributeHeaderName,
            new PropertyLocalizationTranslation(LanguageCodes.English, "XML Attribute Header Name", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "XML-Attribut Kopfzeilenname", null));

        Register(x => x.XmlAttributeHeaderValue,
            new PropertyLocalizationTranslation(LanguageCodes.English, "XML Attribute Header Value", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "XML-Attribut Kopfzeilenwert", null));

        Register(x => x.XmlAttributeValueName,
            new PropertyLocalizationTranslation(LanguageCodes.English, "XML Attribute Value Name", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "XML-Attribut Wertname", null));
    }
}
