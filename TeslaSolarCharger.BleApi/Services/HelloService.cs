using TeslaSolarCharger.BleApi.Services.Contracts;

namespace TeslaSolarCharger.BleApi.Services;

public class HelloService (ILogger<HelloService> logger) : IHelloService
{
    public async Task<bool> IsAlive()
    {
        logger.LogTrace("{method}()", nameof(IsAlive));
        try
        {
            logger.LogTrace("Before return");
            return true;
        }
        finally
        {
            logger.LogTrace("Finally start");
            _ = Task.Run(async () =>
            {
                logger.LogTrace("Before delay");
                await Task.Delay(1000);
                logger.LogTrace("After delay");
            });
            logger.LogTrace("Finally end");
        }
    }
}