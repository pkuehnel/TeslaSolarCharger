using TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;

namespace TeslaSolarCharger.Shared.Localization.Registries.PropertyRegistries;

public class RestValueConfigurationPropertyLocalization : PropertyLocalizationRegistry<DtoFullRestValueConfiguration>
{
    protected override void Configure()
    {
        Register(x => x.Url,
            new PropertyLocalizationTranslation(LanguageCodes.English, "URL", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "URL", null));

        Register(x => x.HttpMethod,
            new PropertyLocalizationTranslation(LanguageCodes.English, "HTTP Method", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "HTTP-Methode", null));

        Register(x => x.NodePatternType,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Node Pattern Type", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Knotenmustertyp", null));
    }
}

public class RestValueResultConfigurationPropertyLocalization : PropertyLocalizationRegistry<DtoJsonXmlResultConfiguration>
{
    protected override void Configure()
    {
        Register(x => x.NodePattern,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Node Pattern", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Knotenmuster", null));

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
