namespace TeslaSolarCharger.Shared.Localization.Registries.Components;

/// <summary>
/// Explanations shown behind the inputs of the REST, MQTT and Modbus value configuration dialogs. The content
/// follows the "Setting up solar power values" chapter of the README, so users no longer have to leave the app to
/// find out what a field expects.
/// </summary>
public class ValueConfigurationInfoLocalizationRegistry : TextLocalizationRegistry<ValueConfigurationInfoLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.ValueConfigInfoUsedFor,
            new TextLocalizationTranslation(LanguageCodes.English,
                """
                Which of the four values TSC needs is read here. TSC expects:

                Inverter power: solar power generated, a positive number in watts.
                Grid power: watts, export to the grid positive, import from the grid negative.
                Home battery power: watts, charging positive, discharging negative.
                Home battery SoC: percent from 0 to 100.

                If your device reports something else, correct it with the operator and the correction factor.
                """),
            new TextLocalizationTranslation(LanguageCodes.German,
                """
                Welchen der vier Werte, die TSC benötigt, dieser Eintrag liefert. TSC erwartet:

                Wechselrichterleistung: erzeugte Solarleistung als positive Zahl in Watt.
                Netzleistung: Watt, Einspeisung positiv, Bezug negativ.
                Heimspeicherleistung: Watt, Laden positiv, Entladen negativ.
                Heimspeicher-Ladestand: Prozent von 0 bis 100.

                Liefert Ihr Gerät etwas anderes, korrigieren Sie das über Operator und Korrekturfaktor.
                """));

        Register(TranslationKeys.ValueConfigInfoOperator,
            new TextLocalizationTranslation(LanguageCodes.English,
                """
                Whether the value is used as it is (Plus) or with its sign flipped (Minus).

                Example: your device reports the power drawn from the grid as a positive number, but TSC expects an
                import to be negative. Select Minus.
                """),
            new TextLocalizationTranslation(LanguageCodes.German,
                """
                Ob der Wert unverändert übernommen wird (Plus) oder mit umgekehrtem Vorzeichen (Minus).

                Beispiel: Ihr Gerät meldet den Netzbezug als positive Zahl, TSC erwartet den Bezug aber negativ.
                Wählen Sie Minus.
                """));

        Register(TranslationKeys.ValueConfigInfoCorrectionFactor,
            new TextLocalizationTranslation(LanguageCodes.English,
                """
                The value is multiplied by this factor so the result is what TSC expects: watts, or percent for the
                home battery SoC.

                Examples:
                Value already in watts: 1
                Value in kilowatts: 1000
                Home battery SoC reported in kWh and the battery holds 100 kWh: 0.01
                """),
            new TextLocalizationTranslation(LanguageCodes.German,
                """
                Der Wert wird mit diesem Faktor multipliziert, damit das Ergebnis dem entspricht, was TSC erwartet:
                Watt, beim Heimspeicher-Ladestand Prozent.

                Beispiele:
                Wert bereits in Watt: 1
                Wert in Kilowatt: 1000
                Heimspeicher-Ladestand wird in kWh gemeldet und der Speicher fasst 100 kWh: 0,01
                """));

        Register(TranslationKeys.ValueConfigInfoDataFormat,
            new TextLocalizationTranslation(LanguageCodes.English,
                """
                How the answer of your device has to be read.

                Single value: the answer is the value itself, e.g. 1234.
                JSON: the value is picked out of a JSON answer.
                XML: the value is picked out of an XML answer.
                """),
            new TextLocalizationTranslation(LanguageCodes.German,
                """
                Wie die Antwort Ihres Geräts gelesen werden muss.

                Einzelner Wert: die Antwort ist der Wert selbst, z. B. 1234.
                JSON: der Wert wird aus einer JSON-Antwort herausgesucht.
                XML: der Wert wird aus einer XML-Antwort herausgesucht.
                """));

        Register(TranslationKeys.ValueConfigInfoPathToValue,
            new TextLocalizationTranslation(LanguageCodes.English,
                """
                Where the value sits inside the answer.

                JSON: for {"data": {"value": 14}} use $.data.value
                XML: for <Device><Measurements><Measurement Value="18.7" Type="GridPower"/></Measurements></Device>
                use Device/Measurements/Measurement and pick the right node with the three XML attribute fields.
                """),
            new TextLocalizationTranslation(LanguageCodes.German,
                """
                Wo der Wert innerhalb der Antwort steht.

                JSON: für {"data": {"value": 14}} verwenden Sie $.data.value
                XML: für <Device><Measurements><Measurement Value="18.7" Type="GridPower"/></Measurements></Device>
                verwenden Sie Device/Measurements/Measurement und wählen den richtigen Knoten über die drei
                XML-Attribut-Felder aus.
                """));

        Register(TranslationKeys.ValueConfigInfoXmlAttributeHeaderName,
            new TextLocalizationTranslation(LanguageCodes.English,
                """
                Name of the attribute that tells the nodes apart when the path matches more than one node.

                For <Measurement Value="18.7" Unit="W" Type="GridPower"/> this is Type.
                """),
            new TextLocalizationTranslation(LanguageCodes.German,
                """
                Name des Attributs, an dem die Knoten unterschieden werden, wenn der Pfad mehrere Knoten trifft.

                Bei <Measurement Value="18.7" Unit="W" Type="GridPower"/> ist das Type.
                """));

        Register(TranslationKeys.ValueConfigInfoXmlAttributeHeaderValue,
            new TextLocalizationTranslation(LanguageCodes.English,
                """
                Value that attribute has to have for the node you want.

                For <Measurement Value="18.7" Unit="W" Type="GridPower"/> this is GridPower.
                """),
            new TextLocalizationTranslation(LanguageCodes.German,
                """
                Wert, den dieses Attribut beim gesuchten Knoten haben muss.

                Bei <Measurement Value="18.7" Unit="W" Type="GridPower"/> ist das GridPower.
                """));

        Register(TranslationKeys.ValueConfigInfoXmlAttributeValueName,
            new TextLocalizationTranslation(LanguageCodes.English,
                """
                Name of the attribute that carries the value itself.

                For <Measurement Value="18.7" Unit="W" Type="GridPower"/> this is Value.
                """),
            new TextLocalizationTranslation(LanguageCodes.German,
                """
                Name des Attributs, das den Wert selbst enthält.

                Bei <Measurement Value="18.7" Unit="W" Type="GridPower"/> ist das Value.
                """));

        Register(TranslationKeys.ValueConfigInfoRestUrl,
            new TextLocalizationTranslation(LanguageCodes.English,
                """
                The complete URL TSC calls to read the values, including protocol and port.

                Example for the SolarEdge plugin:
                http://<IP of your Docker host>:7193/api/CurrentValues/GetCurrentPvValues
                """),
            new TextLocalizationTranslation(LanguageCodes.German,
                """
                Die vollständige URL, die TSC zum Auslesen der Werte aufruft, inklusive Protokoll und Port.

                Beispiel für das SolarEdge-Plugin:
                http://<IP Ihres Docker-Hosts>:7193/api/CurrentValues/GetCurrentPvValues
                """));

        Register(TranslationKeys.ValueConfigInfoHttpMethod,
            new TextLocalizationTranslation(LanguageCodes.English,
                "HTTP method used for the request. Only GET is supported at the moment."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "HTTP-Methode für die Anfrage. Aktuell wird nur GET unterstützt."));

        Register(TranslationKeys.ValueConfigInfoHeaders,
            new TextLocalizationTranslation(LanguageCodes.English,
                "Optional HTTP headers sent with every request, e.g. an authorization token your device requires."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Optionale HTTP-Header, die bei jeder Anfrage mitgesendet werden, z. B. ein Token, das Ihr Gerät zur Authentifizierung verlangt."));

        Register(TranslationKeys.ValueConfigInfoModbusHost,
            new TextLocalizationTranslation(LanguageCodes.English,
                "IP address or hostname of your inverter."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "IP-Adresse oder Hostname Ihres Wechselrichters."));

        Register(TranslationKeys.ValueConfigInfoModbusPort,
            new TextLocalizationTranslation(LanguageCodes.English,
                "Modbus TCP port of your inverter. 502 in most cases."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Modbus-TCP-Port Ihres Wechselrichters. In den meisten Fällen 502."));

        Register(TranslationKeys.ValueConfigInfoUnitIdentifier,
            new TextLocalizationTranslation(LanguageCodes.English,
                "Internal Modbus ID of your inverter, in most cases 1 or 3. Your inverter's documentation states which one it uses."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Interne Modbus-ID Ihres Wechselrichters, in den meisten Fällen 1 oder 3. Die Dokumentation Ihres Wechselrichters nennt den richtigen Wert."));

        Register(TranslationKeys.ValueConfigInfoConnectDelay,
            new TextLocalizationTranslation(LanguageCodes.English,
                "How long TSC waits after connecting before it sends the first request. 1000 ms works with most devices."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Wie lange TSC nach dem Verbinden wartet, bevor die erste Anfrage gesendet wird. 1000 ms funktionieren bei den meisten Geräten."));

        Register(TranslationKeys.ValueConfigInfoReadTimeout,
            new TextLocalizationTranslation(LanguageCodes.English,
                "How long TSC waits for an answer before it reports an error. 1000 ms works with most devices."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Wie lange TSC auf eine Antwort wartet, bevor ein Fehler gemeldet wird. 1000 ms funktionieren bei den meisten Geräten."));

        Register(TranslationKeys.ValueConfigInfoEndianess,
            new TextLocalizationTranslation(LanguageCodes.English,
                "Byte order your device uses for values that span several registers. Big endian is the Modbus default; if you get implausible values, try little endian."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Byte-Reihenfolge, die Ihr Gerät für Werte über mehrere Register verwendet. Big Endian ist der Modbus-Standard; bei unplausiblen Werten probieren Sie Little Endian."));

        Register(TranslationKeys.ValueConfigInfoRegisterType,
            new TextLocalizationTranslation(LanguageCodes.English,
                "Which register table the value is read from. Your inverter's documentation states whether the address is a holding register or an input register."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Aus welcher Registertabelle der Wert gelesen wird. Die Dokumentation Ihres Wechselrichters gibt an, ob die Adresse ein Holding- oder ein Input-Register ist."));

        Register(TranslationKeys.ValueConfigInfoValueType,
            new TextLocalizationTranslation(LanguageCodes.English,
                "Data type of the register content as documented by your inverter, e.g. Int 32 for a 32 bit value spanning two registers. A wrong type results in implausible values."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Datentyp des Registerinhalts laut Dokumentation Ihres Wechselrichters, z. B. Int 32 für einen 32-Bit-Wert über zwei Register. Ein falscher Typ führt zu unplausiblen Werten."));

        Register(TranslationKeys.ValueConfigInfoAddress,
            new TextLocalizationTranslation(LanguageCodes.English,
                "Register address of the value, taken from your inverter's documentation."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Registeradresse des Werts laut Dokumentation Ihres Wechselrichters."));

        Register(TranslationKeys.ValueConfigInfoLength,
            new TextLocalizationTranslation(LanguageCodes.English,
                "Number of registers to read: two for 32 bit values, one for 16 bit values, four for 64 bit values."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Anzahl der zu lesenden Register: zwei bei 32-Bit-Werten, eins bei 16-Bit-Werten, vier bei 64-Bit-Werten."));

        Register(TranslationKeys.ValueConfigInfoBitStartIndex,
            new TextLocalizationTranslation(LanguageCodes.English,
                "Only needed for the Bool type: position of the bit inside the register that carries the value, counted from 0."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Nur beim Typ Bool nötig: Position des Bits innerhalb des Registers, das den Wert enthält, gezählt ab 0."));

        Register(TranslationKeys.ValueConfigInfoMqttTopic,
            new TextLocalizationTranslation(LanguageCodes.English,
                "Topic that carries the value, e.g. inverter/grid/power. TSC subscribes to it and uses the last message it received."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Topic, über das der Wert veröffentlicht wird, z. B. inverter/grid/power. TSC abonniert es und verwendet die zuletzt empfangene Nachricht."));
    }
}
