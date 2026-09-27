using System.Linq;
using FluentValidation.TestHelper;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Helper;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Shared;

public class BaseConfigurationValidatorHttpsTests
{
    private static TestValidationResult<DtoBaseConfiguration> Validate(string? httpsAdditionalHostNames) =>
        new BaseConfigurationValidator().TestValidate(new DtoBaseConfiguration { HttpsAdditionalHostNames = httpsAdditionalHostNames, });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("tsc.lan\n192.168.178.93\n::1")]
    public void AcceptsValidHostNames(string? httpsAdditionalHostNames)
    {
        Validate(httpsAdditionalHostNames).ShouldNotHaveValidationErrorFor(x => x.HttpsAdditionalHostNames);
    }

    [Fact]
    public void NamesTheInvalidEntries()
    {
        Validate("tsc.lan\nbad_name\nhttps://other.lan")
            .ShouldHaveValidationErrorFor(x => x.HttpsAdditionalHostNames)
            .WithErrorMessage("Not a host name or IP address: bad_name, https://other.lan");
    }

    [Fact]
    public void AcceptsTheMaximumNumberOfNames()
    {
        var hostNames = string.Join('\n', Enumerable.Range(1, HttpsHostNameHelper.MaxConfiguredHostNames).Select(i => $"host{i}.lan"));

        Validate(hostNames).ShouldNotHaveValidationErrorFor(x => x.HttpsAdditionalHostNames);
    }

    [Fact]
    public void RejectsMoreThanTheMaximumNumberOfNames()
    {
        var hostNames = string.Join('\n', Enumerable.Range(1, HttpsHostNameHelper.MaxConfiguredHostNames + 1).Select(i => $"host{i}.lan"));

        Validate(hostNames)
            .ShouldHaveValidationErrorFor(x => x.HttpsAdditionalHostNames)
            .WithErrorMessage($"Enter at most {HttpsHostNameHelper.MaxConfiguredHostNames} host names or IP addresses.");
    }

    [Fact]
    public void CountsDuplicatesOnce()
    {
        var hostNames = string.Join('\n', Enumerable.Repeat("tsc.lan", HttpsHostNameHelper.MaxConfiguredHostNames + 5));

        Validate(hostNames).ShouldNotHaveValidationErrorFor(x => x.HttpsAdditionalHostNames);
    }
}
