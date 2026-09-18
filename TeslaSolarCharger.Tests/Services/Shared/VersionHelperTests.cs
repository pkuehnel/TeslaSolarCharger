using System;
using Microsoft.Extensions.Logging.Abstractions;
using TeslaSolarCharger.Shared.Helper;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Shared;

public class VersionHelperTests
{
    private static VersionHelper CreateHelper() => new(NullLogger<VersionHelper>.Instance);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("notAVersion")]
    [InlineData("alpha.3")]
    [InlineData("-alpha.3")]
    public void ParseComparableVersion_ReturnsNullForUnparsableVersions(string? versionString)
    {
        Assert.Null(CreateHelper().ParseComparableVersion(versionString));
    }

    [Theory]
    [InlineData("2.48.1", "2.48.1")]
    [InlineData("2.37.2", "2.37.2")]
    [InlineData("2.48", "2.48")]
    [InlineData("2.48.1.5", "2.48.1.5")]
    public void ParseComparableVersion_ReturnsPlainVersionsUnchanged(string versionString, string expectedVersion)
    {
        Assert.Equal(Version.Parse(expectedVersion), CreateHelper().ParseComparableVersion(versionString));
    }

    [Theory]
    //Build metadata does not influence precedence, so it is stripped without decrementing the build number
    [InlineData("2.48.1+abc123", "2.48.1")]
    [InlineData("2.48.1+abc123.456", "2.48.1")]
    public void ParseComparableVersion_IgnoresBuildMetadata(string versionString, string expectedVersion)
    {
        Assert.Equal(Version.Parse(expectedVersion), CreateHelper().ParseComparableVersion(versionString));
    }

    [Theory]
    //A pre release of x.y.z is older than x.y.z, so it is compared as the previous patch version
    [InlineData("2.48.1-alpha.3", "2.48.0")]
    [InlineData("2.48.1-beta1", "2.48.0")]
    [InlineData("2.48.5-alpha.1", "2.48.4")]
    [InlineData("2.48.1-alpha.3+abc123", "2.48.0")]
    //There is no lower build number than 0, so the build number stays at 0
    [InlineData("2.48.0-alpha.1", "2.48.0")]
    [InlineData("2.48-alpha.1", "2.48.0")]
    public void ParseComparableVersion_ComparesPreReleasesAsPreviousPatchVersion(string versionString, string expectedVersion)
    {
        Assert.Equal(Version.Parse(expectedVersion), CreateHelper().ParseComparableVersion(versionString));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("notAVersion")]
    public void IsVersionInRange_ReturnsTrueIfCurrentVersionIsUnknown(string? currentVersion)
    {
        Assert.True(CreateHelper().IsVersionInRange(currentVersion, "1.0.0", "1.0.1"));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public void IsVersionInRange_ReturnsTrueIfNoBoundsAreSet(string? validFromVersion, string? validToVersion)
    {
        Assert.True(CreateHelper().IsVersionInRange("2.48.1", validFromVersion, validToVersion));
    }

    [Theory]
    //An unparsable bound can not be evaluated and therefore must not filter anything out
    [InlineData("notAVersion", null)]
    [InlineData(null, "notAVersion")]
    public void IsVersionInRange_IgnoresUnparsableBounds(string? validFromVersion, string? validToVersion)
    {
        Assert.True(CreateHelper().IsVersionInRange("2.48.1", validFromVersion, validToVersion));
    }

    [Theory]
    [InlineData("2.37.2")]
    [InlineData("2.37.1")]
    [InlineData("2.0.0")]
    public void IsVersionInRange_UpperBoundIsInclusive(string currentVersion)
    {
        Assert.True(CreateHelper().IsVersionInRange(currentVersion, null, "2.37.2"));
    }

    [Theory]
    [InlineData("2.37.3")]
    [InlineData("2.38.0")]
    [InlineData("2.48.1")]
    //This is the version that wrongly displayed the charge mode notification
    [InlineData("2.48.1-alpha.3")]
    public void IsVersionInRange_ReturnsFalseIfCurrentVersionIsAboveUpperBound(string currentVersion)
    {
        Assert.False(CreateHelper().IsVersionInRange(currentVersion, null, "2.37.2"));
    }

    [Theory]
    [InlineData("2.37.2")]
    [InlineData("2.37.3")]
    [InlineData("2.48.1")]
    public void IsVersionInRange_LowerBoundIsInclusive(string currentVersion)
    {
        Assert.True(CreateHelper().IsVersionInRange(currentVersion, "2.37.2", null));
    }

    [Theory]
    [InlineData("2.37.1")]
    [InlineData("2.36.9")]
    [InlineData("1.0.0")]
    //2.37.2-alpha.1 is compared as 2.37.1 and therefore older than the lower bound
    [InlineData("2.37.2-alpha.1")]
    public void IsVersionInRange_ReturnsFalseIfCurrentVersionIsBelowLowerBound(string currentVersion)
    {
        Assert.False(CreateHelper().IsVersionInRange(currentVersion, "2.37.2", null));
    }

    [Theory]
    [InlineData("2.37.2", true)]
    [InlineData("2.38.0", true)]
    [InlineData("2.39.0", true)]
    [InlineData("2.37.1", false)]
    [InlineData("2.39.1", false)]
    public void IsVersionInRange_EvaluatesBothBounds(string currentVersion, bool expectedIsInRange)
    {
        Assert.Equal(expectedIsInRange, CreateHelper().IsVersionInRange(currentVersion, "2.37.2", "2.39.0"));
    }
}
