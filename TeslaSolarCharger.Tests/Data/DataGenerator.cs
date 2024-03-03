using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.EntityFramework;
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
            },
        });
        return context;
    }
}
