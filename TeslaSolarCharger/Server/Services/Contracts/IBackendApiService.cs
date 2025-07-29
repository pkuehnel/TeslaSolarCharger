﻿using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.Solar4CarBackend;
using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IBackendApiService
{
    Task<DtoValue<string>> StartTeslaOAuth(string locale, string baseUrl);
    Task<DtoVersionRecommendation> PostInstallationInformation(string reason);
    Task<string?> GetCurrentVersion();
    Task GetNewBackendNotifications();
    Task GetToken(DtoBackendLogin login);
    Task RefreshBackendTokenIfNeeded();
    Task<Dtos.Result<T>> SendRequestToBackend<T>(HttpMethod httpMethod, string? accessToken, string requestUrlPart, object? content);
    Task<Result<bool?>> IsBaseAppLicensed(bool useCache);
    Task<bool> IsFleetApiLicensed(string vin, bool useCache);
}
