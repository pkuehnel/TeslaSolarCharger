using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using TeslaSolarCharger.Shared.Localization;
using TeslaSolarCharger.Shared.Localization.Registries.Components;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Shared;

/// <summary>
/// A missing registration is invisible in the UI: the dialog simply shows the key name. As the explanations are
/// keys and content in two places, guard that every info key actually resolves in both languages.
/// </summary>
public class ValueConfigurationInfoLocalizationTests : TestBase
{
    public ValueConfigurationInfoLocalizationTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    public static TheoryData<string> InfoKeys()
    {
        var data = new TheoryData<string>();
        foreach (var key in GetInfoKeys())
        {
            data.Add(key);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(InfoKeys))]
    public void EveryInfoTextIsRegisteredInEveryLanguage(string key)
    {
        var registry = new ValueConfigurationInfoLocalizationRegistry();

        foreach (var language in new[] { LanguageCodes.English, LanguageCodes.German, })
        {
            var text = registry.Get(key, new CultureInfo(language));
            Assert.False(string.IsNullOrWhiteSpace(text), $"{key} is not registered for {language}");
        }
    }

    [Fact]
    public void InfoKeysExist()
    {
        //Guards the test above: if the keys were renamed to another prefix it would silently test nothing.
        Assert.NotEmpty(GetInfoKeys());
    }

    private static IEnumerable<string> GetInfoKeys() => typeof(TranslationKeys)
        .GetProperties(BindingFlags.Public | BindingFlags.Static)
        .Where(p => p.Name.StartsWith("ValueConfigInfo") && p.PropertyType == typeof(string))
        .Select(p => (string)p.GetValue(null)!)
        .ToList();
}
