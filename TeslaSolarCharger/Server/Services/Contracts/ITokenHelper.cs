﻿using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ITokenHelper
{
    Task<TokenState> GetFleetApiTokenState(bool useCache);
    Task<TokenState> GetBackendTokenState(bool useCache);
    Task<DateTimeOffset?> GetFleetApiTokenExpirationDate(bool useCache);
    Task<DateTimeOffset?> GetBackendTokenExpirationDate();
    Task<string?> GetTokenUserName();
}
