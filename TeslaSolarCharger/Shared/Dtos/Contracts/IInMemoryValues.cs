﻿namespace TeslaSolarCharger.Shared.Dtos.Contracts;

public interface IInMemoryValues
{
    List<int> OverageValues { get; set; }
}