using Moq;
using System.Net;
using System.Threading.Tasks;
using TeslaSolarCharger.Server.Contracts;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class CoreServiceTests(ITestOutputHelper outputHelper) : TestBase(outputHelper)
{
    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData((HttpStatusCode)299)]
    public async Task SendTestTelegramMessage_SuccessStatusCode_ReturnsSuccess(HttpStatusCode statusCode)
    {
        Mock.Mock<ITelegramService>().Setup(t => t.SendMessage("TeslaSolarCharger test message")).ReturnsAsync(statusCode);
        var service = Mock.Create<TeslaSolarCharger.Server.Services.CoreService>();

        var result = await service.SendTestTelegramMessage();

        Assert.False(result.HasError);
        Assert.Equal("Sending message succeeded", result.Data?.Value);
        Assert.Null(result.ProblemDetails);
    }

    [Theory]
    [InlineData((HttpStatusCode)199)]
    [InlineData(HttpStatusCode.MultipleChoices)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task SendTestTelegramMessage_NonSuccessStatusCode_ReturnsError(HttpStatusCode statusCode)
    {
        Mock.Mock<ITelegramService>().Setup(t => t.SendMessage("TeslaSolarCharger test message")).ReturnsAsync(statusCode);
        var service = Mock.Create<TeslaSolarCharger.Server.Services.CoreService>();

        var result = await service.SendTestTelegramMessage();

        Assert.True(result.HasError);
        Assert.Equal($"Sending error message failed with status code {statusCode}", result.ErrorMessage);
        Assert.Null(result.Data);
        Assert.Null(result.ProblemDetails);
    }
}
