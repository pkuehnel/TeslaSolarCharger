namespace TeslaSolarCharger.Server.Services.ApiServices.Contracts;

public interface ITeslamateApiService
{
    Task ResumeLogging(int teslaMateCarId);
}
