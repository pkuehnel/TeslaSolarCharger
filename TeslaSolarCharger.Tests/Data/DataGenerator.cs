using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.EntityFramework;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Tests.Data;

public static class DataGenerator
{
    public static string _httpLocalhostApiValues = "http://localhost:5000/api/values";
    public static NodePatternType _nodePatternType = NodePatternType.Json;
    public static HttpVerb _httpMethod = HttpVerb.Get;
    public static string _headerKey = "Authorization";
    public static string _headerValue = "Bearer asdf";
    public static string? _nodePattern = "$.data";
    public static decimal _correctionFactor = 1;
    public static ValueUsage _valueUsage = ValueUsage.GridPower;
    public static ValueOperator _valueOperator = ValueOperator.Plus;


    public static TeslaSolarChargerContext InitSpotPrices(this TeslaSolarChargerContext context)
    {
        context.SpotPrices.Add(new SpotPrice()
        {
            StartDate = new DateTime(2023, 1, 22, 17, 0, 0),
            EndDate = new DateTime(2023, 1, 22, 18, 0, 0), Price = new decimal(0.11)
        });
        return context;
    }

    public static TeslaSolarChargerContext InitRestValueConfigurations(this TeslaSolarChargerContext context)
    {
        context.RestValueConfigurations.Add(new RestValueConfiguration()
        {
            Url = _httpLocalhostApiValues,
            NodePatternType = _nodePatternType,
            HttpMethod = _httpMethod,
            Headers = new List<RestValueConfigurationHeader>()
            {
                new RestValueConfigurationHeader()
                {
                    Key = _headerKey,
                    Value = _headerValue,
                },
            },
            RestValueResultConfigurations = new List<RestValueResultConfiguration>()
            {
                new RestValueResultConfiguration()
                {
                    NodePattern = _nodePattern,
                    CorrectionFactor = _correctionFactor,
                    UsedFor = _valueUsage,
                    Operator = _valueOperator,
                },
                new RestValueResultConfiguration()
                {
                    NodePattern = "$.invPower",
                    CorrectionFactor = _correctionFactor,
                    UsedFor = ValueUsage.InverterPower,
                    Operator = _valueOperator,
                },
                new RestValueResultConfiguration()
                {
                    NodePattern = "$.batSoc",
                    CorrectionFactor = _correctionFactor,
                    UsedFor = ValueUsage.HomeBatterySoc,
                    Operator = _valueOperator,
                },
                new RestValueResultConfiguration()
                {
                    NodePattern = "$.batPower",
                    CorrectionFactor = _correctionFactor,
                    UsedFor = ValueUsage.HomeBatteryPower,
                    Operator = _valueOperator,
                },
            },
        });
        return context;
    }

    public static TeslaSolarChargerContext InitLoggedErrors(this TeslaSolarChargerContext context)
    {
        context.LoggedErrors.AddRange(new List<LoggedError>()
        {
            new()
            {
                Id = -1,
                Headline = "Not Hidden Test",
                IssueKey = "CarStateUnknown",
                StartTimeStamp = new(2023, 1, 22, 17, 0, 0, DateTimeKind.Utc),
                FurtherOccurrences = [new(2023, 1, 22, 17, 1, 0, DateTimeKind.Utc)],
                Message = "Test Error Message",
                Vin = "1234567890",
                Source = nameof(LoggedError.Source),
                MethodName = nameof(LoggedError.MethodName),
                StackTrace = "Test Stack Trace",
            },
            new()
            {
                Id = -2,
                Headline = "Not Hidden Test due to dismissed in before last occurrence",
                IssueKey = "CarStateUnknown",
                StartTimeStamp = new(2023, 1, 22, 17, 0, 0, DateTimeKind.Utc),
                FurtherOccurrences = [new(2023, 1, 22, 17, 1, 0, DateTimeKind.Utc)],
                Message = "Test Error Message",
                Vin = "1234567890",
                Source = nameof(LoggedError.Source),
                MethodName = nameof(LoggedError.MethodName),
                StackTrace = "Test Stack Trace",
                DismissedAt = new DateTime(2023, 1, 22, 17, 0, 30, DateTimeKind.Utc),
            },
            new()
            {
                Id = -3,
                Headline = "Not Enough Occurrences Test",
                IssueKey = "CarStateUnknown",
                StartTimeStamp = new(2023, 1, 22, 17, 0, 0, DateTimeKind.Utc),
                FurtherOccurrences = [],
                Message = "Test Error Message",
                Vin = "1234567890",
                Source = nameof(LoggedError.Source),
                MethodName = nameof(LoggedError.MethodName),
                StackTrace = "Test Stack Trace",
            },
            new()
            {
                Id = -4,
                Headline = "Dismissed Test",
                IssueKey = "CarStateUnknown",
                StartTimeStamp = new(2023, 1, 22, 17, 0, 0, DateTimeKind.Utc),
                FurtherOccurrences = [new(2023, 1, 22, 17, 1, 0, DateTimeKind.Utc)],
                Message = "Test Error Message",
                Vin = "1234567890",
                Source = nameof(LoggedError.Source),
                MethodName = nameof(LoggedError.MethodName),
                StackTrace = "Test Stack Trace",
                DismissedAt = new DateTime(2023, 1, 22, 17, 1, 30, DateTimeKind.Utc),
            },
        });
        return context;
    }
}
