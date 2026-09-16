namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

/// <summary>The words of the solar, battery and electricity price screens.</summary>
public partial class SetupPageLocalizationRegistry
{
    private void RegisterSolarAndBatteryTexts()
    {
        Register(TranslationKeys.SetupSourcePickerTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "What equipment do you have?"),
            new TextLocalizationTranslation(LanguageCodes.German, "Welche Geräte haben Sie?"));

        Register(TranslationKeys.SetupSourcePickerIntro,
            new TextLocalizationTranslation(LanguageCodes.English, "Add your inverter, meter or battery by choosing its brand and then the device. We work out on our own how to read it."),
            new TextLocalizationTranslation(LanguageCodes.German, "Fügen Sie Ihren Wechselrichter, Zähler oder Speicher hinzu, indem Sie erst die Marke und dann das Gerät auswählen. Wie wir es auslesen, finden wir selbst heraus."));

        Register(TranslationKeys.SetupSourcePickerAddButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Add device"),
            new TextLocalizationTranslation(LanguageCodes.German, "Gerät hinzufügen"));

        Register(TranslationKeys.SetupSourcePickerConnectedTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Connected"),
            new TextLocalizationTranslation(LanguageCodes.German, "Verbunden"));

        Register(TranslationKeys.SetupSourcePickerEditButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Change"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ändern"));

        Register(TranslationKeys.SetupSourcePickerRemoveButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Remove"),
            new TextLocalizationTranslation(LanguageCodes.German, "Entfernen"));

        Register(TranslationKeys.SetupSourcePickerRemoveError,
            new TextLocalizationTranslation(LanguageCodes.English, "This device could not be removed."),
            new TextLocalizationTranslation(LanguageCodes.German, "Dieses Gerät konnte nicht entfernt werden."));

        Register(TranslationKeys.SetupSourcePickerSaved,
            new TextLocalizationTranslation(LanguageCodes.English, "Saved. Readings can take a minute to appear."),
            new TextLocalizationTranslation(LanguageCodes.German, "Gespeichert. Es kann eine Minute dauern, bis Werte erscheinen."));

        Register(TranslationKeys.SetupSourcePickerNotListedHint,
            new TextLocalizationTranslation(LanguageCodes.English, "Cannot find your device? Open the advanced section below to connect it by hand."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihr Gerät ist nicht dabei? Öffnen Sie unten den erweiterten Bereich, um es manuell zu verbinden."));

        Register(TranslationKeys.SetupSourceAdvancedTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "My device is not in the list"),
            new TextLocalizationTranslation(LanguageCodes.German, "Mein Gerät ist nicht in der Liste"));

        Register(TranslationKeys.SetupSourceAdvancedIntro,
            new TextLocalizationTranslation(LanguageCodes.English, "You can read any device that publishes its values over a web address, Modbus or MQTT. You will need its address and which value means what, which is usually in its manual."),
            new TextLocalizationTranslation(LanguageCodes.German, "Sie können jedes Gerät auslesen, das seine Werte über eine Webadresse, Modbus oder MQTT bereitstellt. Sie benötigen dessen Adresse und die Bedeutung der Werte, was meist im Handbuch steht."));

        Register(TranslationKeys.SetupMeasurementsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "What we can read right now"),
            new TextLocalizationTranslation(LanguageCodes.German, "Was wir gerade auslesen können"));

        Register(TranslationKeys.SetupMeasurementGridName,
            new TextLocalizationTranslation(LanguageCodes.English, "Electricity to and from the grid"),
            new TextLocalizationTranslation(LanguageCodes.German, "Strom ins und aus dem Netz"));

        Register(TranslationKeys.SetupMeasurementSolarName,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar generation"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solarerzeugung"));

        Register(TranslationKeys.SetupMeasurementBatterySocName,
            new TextLocalizationTranslation(LanguageCodes.English, "Home battery level"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestand des Hausspeichers"));

        Register(TranslationKeys.SetupMeasurementBatteryPowerName,
            new TextLocalizationTranslation(LanguageCodes.English, "Home battery power"),
            new TextLocalizationTranslation(LanguageCodes.German, "Leistung des Hausspeichers"));

        Register(TranslationKeys.SetupMeasurementNotConfigured,
            new TextLocalizationTranslation(LanguageCodes.English, "No device supplies this yet"),
            new TextLocalizationTranslation(LanguageCodes.German, "Noch kein Gerät liefert diesen Wert"));

        Register(TranslationKeys.SetupMeasurementWaitingForData,
            new TextLocalizationTranslation(LanguageCodes.English, "Connected, waiting for the first reading"),
            new TextLocalizationTranslation(LanguageCodes.German, "Verbunden, wartet auf den ersten Wert"));

        Register(TranslationKeys.SetupMeasurementMissingHint,
            new TextLocalizationTranslation(LanguageCodes.English, "Without every reading above we can still charge, but we have to be more careful and you will get less of your own solar electricity into the car."),
            new TextLocalizationTranslation(LanguageCodes.German, "Auch ohne alle Werte oben können wir laden, müssen aber vorsichtiger sein, und es landet weniger Ihres eigenen Solarstroms im Auto."));

        Register(TranslationKeys.SetupMeasurementGridExportFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "sending {0} W to the grid"),
            new TextLocalizationTranslation(LanguageCodes.German, "speist {0} W ins Netz ein"));

        Register(TranslationKeys.SetupMeasurementGridImportFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "taking {0} W from the grid"),
            new TextLocalizationTranslation(LanguageCodes.German, "bezieht {0} W aus dem Netz"));

        Register(TranslationKeys.SetupMeasurementSolarFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} W"),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} W"));

        Register(TranslationKeys.SetupMeasurementBatterySocFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} % full"),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} % voll"));

        Register(TranslationKeys.SetupMeasurementBatteryChargingFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "charging at {0} W"),
            new TextLocalizationTranslation(LanguageCodes.German, "lädt mit {0} W"));

        Register(TranslationKeys.SetupMeasurementBatteryDischargingFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "supplying {0} W to the house"),
            new TextLocalizationTranslation(LanguageCodes.German, "gibt {0} W an das Haus ab"));

        Register(TranslationKeys.SetupMeasurementBatteryIdle,
            new TextLocalizationTranslation(LanguageCodes.English, "neither charging nor discharging"),
            new TextLocalizationTranslation(LanguageCodes.German, "lädt und entlädt gerade nicht"));

        Register(TranslationKeys.SetupMeasurementGridBalanced,
            new TextLocalizationTranslation(LanguageCodes.English, "neither sending electricity to the grid nor taking any from it"),
            new TextLocalizationTranslation(LanguageCodes.German, "weder Einspeisung ins Netz noch Bezug aus dem Netz"));

        Register(TranslationKeys.SetupMeasurementJustNow,
            new TextLocalizationTranslation(LanguageCodes.English, "just now"),
            new TextLocalizationTranslation(LanguageCodes.German, "gerade eben"));

        Register(TranslationKeys.SetupMeasurementMinutesAgoFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} minutes ago"),
            new TextLocalizationTranslation(LanguageCodes.German, "vor {0} Minuten"));

        Register(TranslationKeys.SetupMeasurementHoursAgoFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} hours ago - this reading has stopped updating"),
            new TextLocalizationTranslation(LanguageCodes.German, "vor {0} Stunden – dieser Wert wird nicht mehr aktualisiert"));

        Register(TranslationKeys.SetupHomeBatteryFactsIntro,
            new TextLocalizationTranslation(LanguageCodes.English, "A few facts about the battery itself. You will find them in its data sheet or app."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ein paar Angaben zum Speicher selbst. Sie finden sie im Datenblatt oder in seiner App."));

        Register(TranslationKeys.SetupHomeBatteryReserveTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "How much to keep back for the house"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wie viel für das Haus zurückbleibt"));

        Register(TranslationKeys.SetupHomeBatteryReserveExplanation,
            new TextLocalizationTranslation(LanguageCodes.English, "Left to us, we keep just enough charge to get the house through the evening and let your car have the rest. Set it yourself if you would rather hold a fixed amount back."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wenn Sie es uns überlassen, halten wir genau so viel zurück, wie das Haus über den Abend braucht, und überlassen den Rest Ihrem Auto. Legen Sie es selbst fest, wenn Sie lieber einen festen Wert zurückhalten."));

        Register(TranslationKeys.SetupHomeBatteryReservePending,
            new TextLocalizationTranslation(LanguageCodes.English, "We will work the reserve out automatically as soon as we know:"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wir ermitteln die Reserve automatisch, sobald wir Folgendes wissen:"));

        Register(TranslationKeys.SetupHomeBatteryControlTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Telling the battery when to charge"),
            new TextLocalizationTranslation(LanguageCodes.German, "Dem Speicher sagen, wann er laden soll"));

        Register(TranslationKeys.SetupHomeBatteryControlExplanation,
            new TextLocalizationTranslation(LanguageCodes.English, "Deciding how much to keep back is one thing; actively telling the battery to charge or to hold is another, and not every battery lets us do it. Setup leaves that switched off. You can look at it later in the detailed settings."),
            new TextLocalizationTranslation(LanguageCodes.German, "Zu entscheiden, wie viel zurückbleibt, ist das eine; dem Speicher aktiv zu sagen, dass er laden oder halten soll, das andere – und nicht jeder Speicher lässt das zu. Die Einrichtung lässt das ausgeschaltet. Sie können es später in den detaillierten Einstellungen ansehen."));

        Register(TranslationKeys.SetupHomeBatteryControlLink,
            new TextLocalizationTranslation(LanguageCodes.English, "Open the detailed settings"),
            new TextLocalizationTranslation(LanguageCodes.German, "Detaillierte Einstellungen öffnen"));
    }

    private void RegisterPriceTexts()
    {
        Register(TranslationKeys.SetupPricesIntro,
            new TextLocalizationTranslation(LanguageCodes.English, "We need to know what electricity costs you, so we can tell a cheap hour from an expensive one."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wir müssen wissen, was Sie für Strom bezahlen, um günstige von teuren Stunden unterscheiden zu können."));

        Register(TranslationKeys.SetupPricesKindQuestion,
            new TextLocalizationTranslation(LanguageCodes.English, "Does your electricity price stay the same, change at set times, or follow market prices?"),
            new TextLocalizationTranslation(LanguageCodes.German, "Bleibt Ihr Strompreis gleich, ändert er sich zu festen Zeiten, oder folgt er dem Marktpreis?"));

        Register(TranslationKeys.SetupPricesKindFixed,
            new TextLocalizationTranslation(LanguageCodes.English, "It stays the same"),
            new TextLocalizationTranslation(LanguageCodes.German, "Er bleibt gleich"));

        Register(TranslationKeys.SetupPricesKindFixedHint,
            new TextLocalizationTranslation(LanguageCodes.English, "One price, all day, every day. This is the most common."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ein Preis, den ganzen Tag, jeden Tag. Das ist der häufigste Fall."));

        Register(TranslationKeys.SetupPricesKindTimeOfUse,
            new TextLocalizationTranslation(LanguageCodes.English, "It changes at set times"),
            new TextLocalizationTranslation(LanguageCodes.German, "Er ändert sich zu festen Zeiten"));

        Register(TranslationKeys.SetupPricesKindTimeOfUseHint,
            new TextLocalizationTranslation(LanguageCodes.English, "For example a cheaper rate at night or at the weekend, at hours your contract names."),
            new TextLocalizationTranslation(LanguageCodes.German, "Zum Beispiel ein günstigerer Nacht- oder Wochenendtarif zu den in Ihrem Vertrag genannten Zeiten."));

        Register(TranslationKeys.SetupPricesKindMarket,
            new TextLocalizationTranslation(LanguageCodes.English, "It follows market prices"),
            new TextLocalizationTranslation(LanguageCodes.German, "Er folgt dem Marktpreis"));

        Register(TranslationKeys.SetupPricesKindMarketHint,
            new TextLocalizationTranslation(LanguageCodes.English, "The price changes every hour. Your contract adds its own charges on top of the market price."),
            new TextLocalizationTranslation(LanguageCodes.German, "Der Preis ändert sich stündlich. Ihr Vertrag schlägt eigene Kosten auf den Marktpreis auf."));

        Register(TranslationKeys.SetupPricesGridTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "What you pay for electricity"),
            new TextLocalizationTranslation(LanguageCodes.German, "Was Sie für Strom bezahlen"));

        Register(TranslationKeys.SetupPricesGridExplanationFixed,
            new TextLocalizationTranslation(LanguageCodes.English, "The price for one kilowatt hour, including tax and all charges."),
            new TextLocalizationTranslation(LanguageCodes.German, "Der Preis für eine Kilowattstunde, inklusive Steuern und aller Abgaben."));

        Register(TranslationKeys.SetupPricesGridExplanationTimeOfUse,
            new TextLocalizationTranslation(LanguageCodes.English, "Your normal price, for every hour you do not describe below."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihr normaler Preis für alle Stunden, die Sie unten nicht beschreiben."));

        Register(TranslationKeys.SetupPricesGridExplanationMarket,
            new TextLocalizationTranslation(LanguageCodes.English, "Everything your contract charges on top of the market price: grid fees, tax and the supplier's own margin."),
            new TextLocalizationTranslation(LanguageCodes.German, "Alles, was Ihr Vertrag zusätzlich zum Marktpreis berechnet: Netzentgelte, Steuern und die Marge des Anbieters."));

        Register(TranslationKeys.SetupPricesWhereToFind,
            new TextLocalizationTranslation(LanguageCodes.English, "You will find this on your electricity bill or in your supplier's app."),
            new TextLocalizationTranslation(LanguageCodes.German, "Sie finden das auf Ihrer Stromrechnung oder in der App Ihres Anbieters."));

        Register(TranslationKeys.SetupPricesTimesTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "When the price is different"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wann der Preis abweicht"));

        Register(TranslationKeys.SetupPricesTimesExplanation,
            new TextLocalizationTranslation(LanguageCodes.English, "Add one entry per period your contract prices differently. Every other hour uses the price above."),
            new TextLocalizationTranslation(LanguageCodes.German, "Fügen Sie je einen Eintrag pro Zeitraum hinzu, den Ihr Vertrag anders bepreist. Alle übrigen Stunden nutzen den Preis oben."));

        Register(TranslationKeys.SetupPricesTimeSlotPriceLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Price during these hours"),
            new TextLocalizationTranslation(LanguageCodes.German, "Preis in diesen Stunden"));

        Register(TranslationKeys.SetupPricesAddTimeSlotButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Add a period"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zeitraum hinzufügen"));

        Register(TranslationKeys.SetupPricesNoTimeSlotsYet,
            new TextLocalizationTranslation(LanguageCodes.English, "Without a period your price is the same all day, which is the first option above."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ohne Zeitraum gilt Ihr Preis den ganzen Tag, was der ersten Option oben entspricht."));

        Register(TranslationKeys.SetupPricesMarketTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Your market price contract"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihr Marktpreis-Vertrag"));

        Register(TranslationKeys.SetupPricesMarketExplanation,
            new TextLocalizationTranslation(LanguageCodes.English, "We fetch the hourly market price for your region and add what your contract charges on top."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wir holen den stündlichen Marktpreis für Ihre Region und rechnen die Kosten Ihres Vertrags hinzu."));

        Register(TranslationKeys.SetupPricesMarketRegionLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Which market price applies to you?"),
            new TextLocalizationTranslation(LanguageCodes.German, "Welcher Marktpreis gilt für Sie?"));

        Register(TranslationKeys.SetupPricesMarketRegionMissing,
            new TextLocalizationTranslation(LanguageCodes.English, "Please choose your region. Market prices are published per region, so without one there is nothing to fetch."),
            new TextLocalizationTranslation(LanguageCodes.German, "Bitte wählen Sie Ihre Region. Marktpreise werden je Region veröffentlicht, ohne Region gibt es nichts abzurufen."));

        Register(TranslationKeys.SetupIssueMarketRegionMissing,
            new TextLocalizationTranslation(LanguageCodes.English, "Your electricity tariff follows the market price, so we need to know which region's prices apply."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihr Stromtarif folgt dem Marktpreis, daher müssen wir wissen, welche Regionspreise gelten."));

        Register(TranslationKeys.SetupPricesMarketSurchargeNote,
            new TextLocalizationTranslation(LanguageCodes.English, "The number this starts with is only an example. Use the figure from your own contract, or charging will be planned around the wrong price."),
            new TextLocalizationTranslation(LanguageCodes.German, "Der voreingestellte Wert ist nur ein Beispiel. Verwenden Sie den Wert aus Ihrem eigenen Vertrag, sonst wird das Laden um den falschen Preis herum geplant."));

        Register(TranslationKeys.SetupPricesSolarTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "What your own solar electricity is worth"),
            new TextLocalizationTranslation(LanguageCodes.German, "Was Ihr eigener Solarstrom wert ist"));

        Register(TranslationKeys.SetupPricesSolarExplanation,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar electricity you put into the car is electricity you do not sell. Enter what you would have been paid for it, so charging from the sun is compared fairly against buying from the grid."),
            new TextLocalizationTranslation(LanguageCodes.German, "Solarstrom, der ins Auto geht, wird nicht verkauft. Geben Sie an, was Sie dafür bekommen hätten, damit das Laden mit Sonne fair gegen den Netzbezug abgewogen wird."));
    }
}
