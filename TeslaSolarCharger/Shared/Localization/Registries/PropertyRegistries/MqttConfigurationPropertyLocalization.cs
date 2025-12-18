using TeslaSolarCharger.Shared.Dtos.MqttConfiguration;

namespace TeslaSolarCharger.Shared.Localization.Registries.PropertyRegistries;

public class MqttConfigurationPropertyLocalization : PropertyLocalizationRegistry<DtoMqttConfiguration>
{
    protected override void Configure()
    {
        Register(x => x.Host,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Host", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Host", null));

        Register(x => x.Port,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Port", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Port", null));

        Register(x => x.Username,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Username", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Benutzername", null));

        Register(x => x.Password,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Password", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Passwort", null));
    }
}

public class MqttResultConfigurationPropertyLocalization : PropertyLocalizationRegistry<DtoMqttResultConfiguration>
{
    protected override void Configure()
    {
        Register(x => x.Topic,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Topic", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Topic", null));

        Register(x => x.NodePattern,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Node Pattern", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Node Pattern", null));

        Register(x => x.XmlAttributeHeaderName,
            new PropertyLocalizationTranslation(LanguageCodes.English, "XML Attribute Header Name", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "XML Attribut Header Name", null));

        Register(x => x.XmlAttributeHeaderValue,
            new PropertyLocalizationTranslation(LanguageCodes.English, "XML Attribute Header Value", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "XML Attribut Header Wert", null));

        Register(x => x.XmlAttributeValueName,
            new PropertyLocalizationTranslation(LanguageCodes.English, "XML Attribute Value Name", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "XML Attribut Wert Name", null));

        Register(x => x.CorrectionFactor,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Correction Factor", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Korrekturfaktor", null));
    }
}
