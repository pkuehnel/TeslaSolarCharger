﻿namespace TeslaSolarCharger.Server.Contracts;

public interface ICarDbUpdateService
{
    Task UpdateMissingCarDataFromDatabase();
}