using Xunit;
using Xunit.Abstractions;

namespace SolarTeslaCharger.Tests.Services.Server;

public class TelegramService : TestBase
{
    public TelegramService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public void BuildsCorrectRequestUri()
    {
        var botKey = "0815:2asdf";
        var channelId = "5236466";
        var message = "Test";
        var telegramService = Mock.Create<SolarTeslaCharger.Server.Services.TelegramService>();

        var uri = telegramService.CreateRequestUri(message, botKey, channelId);

        Assert.Equal("https://api.telegram.org/bot0815:2asdf/sendMessage?chat_id=5236466&text=Test", uri);
    }
}