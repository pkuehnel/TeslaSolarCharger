using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using TeslaSolarCharger.Shared.Localization;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Shared;

/// <summary>
/// The app is called Solar4Car, the paid per car plan is the Car License, and "Fleet API" is jargon users do not
/// understand. Guards every text of every registry, so an old wording can not slip back in with a new text.
/// </summary>
public class AppWordingTests
{
    //The BLE container really is called TeslaSolarChargerBleApi, so naming it must stay possible
    private static readonly Regex OutdatedWording = new(@"\bTSC\b|TeslaSolarCharger(?!BleApi)|Fleet[ -]API|Car-Lizenz");

    //The subtitle under the logo names the old product on purpose, so existing users recognize the app
    private static readonly string[] IntentionalMentions = ["formerly TeslaSolarCharger", "ehemals TeslaSolarCharger",];

    private static readonly string[] Languages = [LanguageCodes.English, LanguageCodes.German,];

    private static IReadOnlyList<TRegistry> CreateAll<TRegistry>() =>
        typeof(ITextLocalizationRegistry).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false, } && typeof(TRegistry).IsAssignableFrom(type))
            .Select(type => (TRegistry)Activator.CreateInstance(type)!)
            .ToList();

    private static bool IsOutdated(string? text)
    {
        if (text == null)
        {
            return false;
        }

        foreach (var intentionalMention in IntentionalMentions)
        {
            text = text.Replace(intentionalMention, string.Empty, StringComparison.Ordinal);
        }

        return OutdatedWording.IsMatch(text);
    }

    [Fact]
    public void NoTextUsesTheOldProductNameOrTheFleetApiJargon()
    {
        var registries = CreateAll<ITextLocalizationRegistry>();

        var outdated = registries
            .SelectMany(registry => registry.Keys.SelectMany(key => Languages.Select(language =>
                (Name: $"{registry.GetType().Name}.{key} ({language})", Text: registry.Get(key, new CultureInfo(language))))))
            .Where(text => IsOutdated(text.Text))
            .Select(text => $"{text.Name}: {text.Text}")
            .ToList();

        Assert.NotEmpty(registries);
        Assert.Empty(outdated);
    }

    [Fact]
    public void NoFieldLabelOrHelpTextUsesTheOldProductNameOrTheFleetApiJargon()
    {
        var registries = CreateAll<IPropertyLocalizationRegistry>();

        var outdated = registries
            .SelectMany(registry => registry.Keys.SelectMany(key => Languages.Select(language =>
                (Name: $"{registry.GetType().Name}.{key} ({language})", Localization: registry.Get(key, new CultureInfo(language))))))
            .Where(text => IsOutdated(text.Localization?.DisplayName) || IsOutdated(text.Localization?.HelperText))
            .Select(text => $"{text.Name}: {text.Localization}")
            .ToList();

        Assert.NotEmpty(registries);
        Assert.Empty(outdated);
    }

    [Theory]
    [InlineData("Restart the TSC container.")]
    [InlineData("Starten Sie den TSC-Container neu.")]
    [InlineData("Welcome to TeslaSolarCharger!")]
    [InlineData("Wake up (Fleet API)")]
    [InlineData("Das Fleet-API-Token ist abgelaufen.")]
    [InlineData("Dafür ist eine Car-Lizenz erforderlich.")]
    public void OldWordingIsDetected(string text)
    {
        Assert.True(IsOutdated(text));
    }

    [Theory]
    [InlineData("Welcome to Solar4Car!")]
    [InlineData("formerly TeslaSolarCharger")]
    [InlineData("ehemals TeslaSolarCharger")]
    [InlineData("Install the Solar4Car BLE container (TeslaSolarChargerBleApi).")]
    [InlineData("Restart the Solar4Car container (teslasolarcharger).")]
    [InlineData("Live charging data (Fleet Telemetry)")]
    public void CurrentWordingIsAccepted(string text)
    {
        Assert.False(IsOutdated(text));
    }

    [Fact]
    public void TheLogoSubtitleStillNamesTheFormerProduct()
    {
        var texts = CreateAll<ITextLocalizationRegistry>()
            .SelectMany(registry => registry.Keys.SelectMany(key => Languages.Select(language => registry.Get(key, new CultureInfo(language)))))
            .ToList();

        Assert.All(IntentionalMentions, mention => Assert.Contains(mention, texts));
    }
}
