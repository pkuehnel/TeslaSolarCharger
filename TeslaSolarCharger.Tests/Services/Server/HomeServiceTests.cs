using System.Threading.Tasks;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Shared.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class HomeServiceTests(ITestOutputHelper outputHelper) : TestBase(outputHelper)
{
    /// <summary>
    /// The home page words the Tesla cloud key as a recommendation for a Bluetooth car and as an error for every other
    /// Tesla, so the overview has to say which of the two the car is.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetCarOverview_SaysWhetherTheCarIsControlledViaBluetooth(bool useBle)
    {
        Context.Cars.Add(new Car
        {
            Id = 41,
            Name = "Model Y",
            Vin = "VIN_OVERVIEW",
            CarType = CarType.Tesla,
            UseBle = useBle,
        });
        await Context.SaveChangesAsync();
        DetachAllEntities();
        var service = Mock.Create<TeslaSolarCharger.Server.Services.HomeService>();

        var overview = await service.GetCarOverview(41);

        Assert.Equal(useBle, overview.UseBle);
        Assert.Equal("Model Y", overview.Name);
        Assert.Equal(CarType.Tesla, overview.CarType);
    }
}
