using System.Linq;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.Setup;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server.Setup;

public class SetupConfigurationOwnershipTests
{
    [Fact]
    public void OwnedPropertiesAllExist()
    {
        var propertyNames = typeof(DtoBaseConfiguration).GetProperties().Select(p => p.Name).ToHashSet();

        //A renamed setting would otherwise be silently dropped from what the assistant writes.
        Assert.All(SetupConfigurationOwnership.OwnedProperties, name => Assert.Contains(name, propertyNames));
    }

    [Fact]
    public void FinishingSetupIsNotSomethingTheAssistantCopiesAlongTheWay()
    {
        //Clearing the first run flag is the act of finishing, which happens on its own and only once everything
        //required has been saved.
        Assert.DoesNotContain(nameof(BaseConfigurationBase.IsFirstRun), SetupConfigurationOwnership.OwnedProperties);
    }

    [Fact]
    public void OnlyOwnedPropertiesAreCopied()
    {
        var from = new DtoBaseConfiguration
        {
            HomeGeofenceRadius = 300,
            HomeBatteryMinSoc = 22,
            PowerBuffer = 1,
            TelegramBotKey = "from",
        };
        var onto = new DtoBaseConfiguration
        {
            HomeGeofenceRadius = 50,
            HomeBatteryMinSoc = 10,
            PowerBuffer = 900,
            TelegramBotKey = "onto",
        };

        SetupConfigurationOwnership.CopyOwnedProperties(from, onto);

        Assert.Equal(300, onto.HomeGeofenceRadius);
        Assert.Equal(22, onto.HomeBatteryMinSoc);
        //Neither of these belongs to the assistant, so a change made elsewhere survives.
        Assert.Equal(900, onto.PowerBuffer);
        Assert.Equal("onto", onto.TelegramBotKey);
    }

    [Fact]
    public void CopyingAnUndecidedValueClearsTheTargetRatherThanKeepingAStaleOne()
    {
        var from = new DtoBaseConfiguration { DynamicHomeBatteryMinSoc = null, };
        var onto = new DtoBaseConfiguration { DynamicHomeBatteryMinSoc = true, };

        SetupConfigurationOwnership.CopyOwnedProperties(from, onto);

        Assert.Null(onto.DynamicHomeBatteryMinSoc);
    }
}
