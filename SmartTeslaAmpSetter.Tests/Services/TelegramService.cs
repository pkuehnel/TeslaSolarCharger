using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SmartTeslaAmpSetter.Tests.Services;

public class TelegramService : TestBase
{
    public TelegramService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public async Task Can_Send_Telegram_Messages()
    {
        var telegramService = Mock.Create<Server.Services.TelegramService>();
        var statusCode = await telegramService.SendMessage("test");

        Assert.True((int)statusCode >= 200 && (int)statusCode <= 299);
    }
}