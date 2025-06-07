using System;
using System.Collections.Generic;
using System.Linq;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Server.Services.GridPrice.Dtos;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server;

public class ValidFromToSplitter : TestBase
{
    public ValidFromToSplitter(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }


    [Fact]
    public void LetsDataWithoutSplitRequirementsAlone()
    {
        var chargingSchedules = new List<DtoChargingSchedule>()
        {
            new DtoChargingSchedule(1, null)
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 12, 0, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 13, 0, 0, TimeSpan.Zero),
                ChargingPower = 5000,
                OnlyChargeOnAtLeastSolarPower = 4200,
            },
            new DtoChargingSchedule(1, null)
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 13, 0, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 13, 15, 0, TimeSpan.Zero),
                ChargingPower = 5000,
                OnlyChargeOnAtLeastSolarPower = 4200,
            },
        };
        var prices = new List<Price>()
        {
            new Price()
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 12, 0, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 13, 0, 0, TimeSpan.Zero),
                SolarPrice = 0.05m,
                GridPrice = 0.24m,
            },
            new Price()
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 13, 0, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 13, 15, 0, TimeSpan.Zero),
                SolarPrice = 0.06m,
                GridPrice = 0.28m,
            },
        };
        var validFromToSplitter = Mock.Create<TeslaSolarCharger.Server.Services.ValidFromToSplitter>();
        var result = validFromToSplitter.SplitByBoundaries(chargingSchedules, prices,
            new DateTimeOffset(2025, 5, 26, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2025, 5, 26, 13, 15, 0, TimeSpan.Zero));
        Assert.Equal(2, result.SplitLeft.Count);
        Assert.Equal(2, result.SplitRight.Count);
    }

    [Fact]
    public void SplitsRightAsRequired()
    {
        var chargingSchedules = new List<DtoChargingSchedule>()
        {
            new DtoChargingSchedule(1, null)
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 12, 0, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 12, 45, 0, TimeSpan.Zero),
                ChargingPower = 5000,
                OnlyChargeOnAtLeastSolarPower = 4200,
            },
            new DtoChargingSchedule(1, null)
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 12, 45, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 13, 00, 0, TimeSpan.Zero),
                ChargingPower = 8000,
                OnlyChargeOnAtLeastSolarPower = 6200,
            },
            new DtoChargingSchedule(1, null)
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 13, 0, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 13, 15, 0, TimeSpan.Zero),
                ChargingPower = 5000,
                OnlyChargeOnAtLeastSolarPower = 4200,
            },
        };
        var prices = new List<Price>()
        {
            new Price()
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 12, 0, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 13, 0, 0, TimeSpan.Zero),
                SolarPrice = 0.05m,
                GridPrice = 0.24m,
            },
            new Price()
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 13, 0, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 13, 15, 0, TimeSpan.Zero),
                SolarPrice = 0.06m,
                GridPrice = 0.28m,
            },
        };
        var validFromToSplitter = Mock.Create<TeslaSolarCharger.Server.Services.ValidFromToSplitter>();
        var result = validFromToSplitter.SplitByBoundaries(chargingSchedules, prices,
            new DateTimeOffset(2025, 5, 26, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2025, 5, 26, 13, 15, 0, TimeSpan.Zero));
        Assert.Equal(3, result.SplitLeft.Count);
        Assert.Equal(3, result.SplitRight.Count);
    }

    [Fact]
    public void SplitsBothAsRequired()
    {
        var chargingSchedules = new List<DtoChargingSchedule>()
        {
            new DtoChargingSchedule(1, null)
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 12, 0, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 12, 45, 0, TimeSpan.Zero),
                ChargingPower = 5000,
                OnlyChargeOnAtLeastSolarPower = 4200,
            },
            new DtoChargingSchedule(1, null)
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 12, 45, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 13, 00, 0, TimeSpan.Zero),
                ChargingPower = 8000,
                OnlyChargeOnAtLeastSolarPower = 6200,
            },
            new DtoChargingSchedule(1, null)
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 13, 0, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 13, 15, 0, TimeSpan.Zero),
                ChargingPower = 5000,
                OnlyChargeOnAtLeastSolarPower = 4200,
            },
        };
        var prices = new List<Price>()
        {
            new Price()
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 12, 0, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 12, 11, 0, TimeSpan.Zero),
                SolarPrice = 0.05m,
                GridPrice = 0.24m,
            },
            new Price()
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 12, 11, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 12, 32, 0, TimeSpan.Zero),
                SolarPrice = 0.05m,
                GridPrice = 0.24m,
            },
            new Price()
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 12, 32, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 12, 47, 0, TimeSpan.Zero),
                SolarPrice = 0.05m,
                GridPrice = 0.24m,
            },
            new Price()
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 12, 47, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 13, 15, 0, TimeSpan.Zero),
                SolarPrice = 0.06m,
                GridPrice = 0.28m,
            },
        };
        var validFromToSplitter = Mock.Create<TeslaSolarCharger.Server.Services.ValidFromToSplitter>();
        var result = validFromToSplitter.SplitByBoundaries(chargingSchedules, prices,
            new DateTimeOffset(2025, 5, 26, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2025, 5, 26, 13, 15, 0, TimeSpan.Zero));
        Assert.Equal(6, result.SplitLeft.Count);
        Assert.Equal(6, result.SplitRight.Count);
    }

    [Fact]
    public void SplitsBothAsRequiredAndCutsStartsAndEnds()
    {
        var chargingSchedules = new List<DtoChargingSchedule>()
        {
            new DtoChargingSchedule(1, null)
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 12, 0, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 12, 45, 0, TimeSpan.Zero),
                ChargingPower = 5000,
                OnlyChargeOnAtLeastSolarPower = 4200,
            },
            new DtoChargingSchedule(1, null)
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 12, 45, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 13, 00, 0, TimeSpan.Zero),
                ChargingPower = 8000,
                OnlyChargeOnAtLeastSolarPower = 6200,
            },
            new DtoChargingSchedule(1, null)
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 13, 0, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 13, 15, 0, TimeSpan.Zero),
                ChargingPower = 5000,
                OnlyChargeOnAtLeastSolarPower = 4200,
            },
        };
        var prices = new List<Price>()
        {
            new Price()
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 12, 0, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 12, 11, 0, TimeSpan.Zero),
                SolarPrice = 0.05m,
                GridPrice = 0.24m,
            },
            new Price()
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 12, 11, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 12, 32, 0, TimeSpan.Zero),
                SolarPrice = 0.05m,
                GridPrice = 0.24m,
            },
            new Price()
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 12, 32, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 12, 47, 0, TimeSpan.Zero),
                SolarPrice = 0.05m,
                GridPrice = 0.24m,
            },
            new Price()
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 12, 47, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 13, 15, 0, TimeSpan.Zero),
                SolarPrice = 0.06m,
                GridPrice = 0.28m,
            },
        };
        var validFromToSplitter = Mock.Create<TeslaSolarCharger.Server.Services.ValidFromToSplitter>();
        var startDate = new DateTimeOffset(2025, 5, 26, 12, 12, 0, TimeSpan.Zero);
        var endDate = new DateTimeOffset(2025, 5, 26, 13, 8, 0, TimeSpan.Zero);
        var result = validFromToSplitter.SplitByBoundaries(chargingSchedules, prices,
            startDate, endDate);
        Assert.Equal(5, result.SplitLeft.Count);
        Assert.Equal(5, result.SplitRight.Count);
        Assert.Equal(startDate, result.SplitLeft.First().ValidFrom);
        Assert.Equal(endDate, result.SplitRight.Last().ValidTo);
    }

    [Fact]
    public void LeavesOutMissingElements()
    {
        var chargingSchedules = new List<DtoChargingSchedule>()
        {
            new DtoChargingSchedule(1, null)
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 12, 0, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 12, 45, 0, TimeSpan.Zero),
                ChargingPower = 5000,
                OnlyChargeOnAtLeastSolarPower = 4200,
            },
            new DtoChargingSchedule(1, null)
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 13, 0, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 13, 15, 0, TimeSpan.Zero),
                ChargingPower = 5000,
                OnlyChargeOnAtLeastSolarPower = 4200,
            },
        };
        var prices = new List<Price>()
        {
            new Price()
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 12, 0, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 12, 11, 0, TimeSpan.Zero),
                SolarPrice = 0.05m,
                GridPrice = 0.24m,
            },
            new Price()
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 12, 11, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 12, 32, 0, TimeSpan.Zero),
                SolarPrice = 0.05m,
                GridPrice = 0.24m,
            },
            new Price()
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 12, 32, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 12, 47, 0, TimeSpan.Zero),
                SolarPrice = 0.05m,
                GridPrice = 0.24m,
            },
            new Price()
            {
                ValidFrom = new DateTimeOffset(2025, 5, 26, 12, 47, 0, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(2025, 5, 26, 13, 15, 0, TimeSpan.Zero),
                SolarPrice = 0.06m,
                GridPrice = 0.28m,
            },
        };
        var validFromToSplitter = Mock.Create<TeslaSolarCharger.Server.Services.ValidFromToSplitter>();
        var startDate = new DateTimeOffset(2025, 5, 26, 12, 12, 0, TimeSpan.Zero);
        var endDate = new DateTimeOffset(2025, 5, 26, 13, 8, 0, TimeSpan.Zero);
        var result = validFromToSplitter.SplitByBoundaries(chargingSchedules, prices,
            startDate, endDate);
        Assert.Equal(3, result.SplitLeft.Count);
        Assert.Equal(5, result.SplitRight.Count);
        Assert.Equal(startDate, result.SplitLeft.First().ValidFrom);
        Assert.Equal(endDate, result.SplitRight.Last().ValidTo);
    }
}
