using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.SharedModel.Enums;
using Xunit;
using Xunit.Abstractions;
#pragma warning disable xUnit2013

namespace TeslaSolarCharger.Tests.Services.Services;

[SuppressMessage("ReSharper", "UseConfigureAwaitFalse")]
public class RestValueConfigurationService(ITestOutputHelper outputHelper) : TestBase(outputHelper)
{
    private string _httpLocalhostApiValues = "http://localhost:5000/api/values";
    private NodePatternType _nodePatternType = NodePatternType.Json;
    private HttpVerb _httpMethod = HttpVerb.Get;
    private string _headerKey = "Authorization";
    private string _headerValue = "Bearer asdf";
    private string? _nodePattern = "$.data";
    private float _correctionFactor = 1;
    private ValueUsage _valueUsage = ValueUsage.GridPower;
    private ValueOperator _valueOperator = ValueOperator.Plus;

    [Fact]
    public async Task Can_Get_Rest_Configurations()
    {
        await GenerateDemoData();
        var service = Mock.Create<TeslaSolarCharger.Services.Services.RestValueConfigurationService>();
        var restValueConfigurations = await service.GetAllRestValueConfigurations();
        Assert.NotEmpty(restValueConfigurations);
        Assert.Equal(1, restValueConfigurations.Count);
        var firstValue = restValueConfigurations.First();
        Assert.Equal(_httpLocalhostApiValues, firstValue.Url);
        Assert.Equal(_nodePatternType, firstValue.NodePatternType);
        Assert.Equal(_httpMethod, firstValue.HttpMethod);
    }

    [Fact]
    public async Task Can_Update_Rest_Configurations()
    {
        await GenerateDemoData();
        var service = Mock.Create<TeslaSolarCharger.Services.Services.RestValueConfigurationService>();
        var restValueConfigurations = await service.GetAllRestValueConfigurations();
        var firstValue = restValueConfigurations.First();
        var newUrl = "http://localhost:5000/api/values2";
        var newNodePatternType = NodePatternType.Xml;
        Assert.NotEqual(newUrl, firstValue.Url);
        Assert.NotEqual(newNodePatternType, firstValue.NodePatternType);
        firstValue.Url = newUrl;
        firstValue.NodePatternType = newNodePatternType;
        await service.SaveRestValueConfiguration(firstValue);
        var restValueConfigurationsAfterUpdate = await service.GetAllRestValueConfigurations();
        var firstValueAfterUpdate = restValueConfigurationsAfterUpdate.First();
        Assert.Equal(newUrl, firstValueAfterUpdate.Url);
        Assert.Equal(newNodePatternType, firstValueAfterUpdate.NodePatternType);
    }

    [Fact]
    public async Task Can_Get_Rest_Configuration_Headers()
    {
        await GenerateDemoData();
        var service = Mock.Create<TeslaSolarCharger.Services.Services.RestValueConfigurationService>();
        var restValueConfigurations = await service.GetAllRestValueConfigurations();
        var firstValue = restValueConfigurations.First();
        var headers = await service.GetHeadersByConfigurationId(firstValue.Id);
        Assert.NotEmpty(headers);
        Assert.Equal(1, headers.Count);
        var firstHeader = headers.First();
        Assert.Equal(_headerKey, firstHeader.Key);
        Assert.Equal(_headerValue, firstHeader.Value);
    }

    [Fact]
    public async Task Can_Update_Rest_Configuration_Headers()
    {
        await GenerateDemoData();
        var service = Mock.Create<TeslaSolarCharger.Services.Services.RestValueConfigurationService>();
        var restValueConfigurations = await service.GetAllRestValueConfigurations();
        var firstValue = restValueConfigurations.First();
        var headers = await service.GetHeadersByConfigurationId(firstValue.Id);
        var firstHeader = headers.First();
        var newKey = "test1";
        var newValue = "test2";
        Assert.NotEqual(newKey, firstHeader.Key);
        Assert.NotEqual(newValue, firstHeader.Value);
        firstHeader.Key = newKey;
        firstHeader.Value = newValue;
        var id = await service.SaveHeader(firstValue.Id, firstHeader);
        Assert.Equal(firstHeader.Id, id);
    }

    [Fact]
    public async Task Can_Get_Rest_Result_Configurations()
    {
        await GenerateDemoData();
        var service = Mock.Create<TeslaSolarCharger.Services.Services.RestValueConfigurationService>();
        var restValueConfigurations = await service.GetAllRestValueConfigurations();
        var firstValue = restValueConfigurations.First();
        var values = await service.GetResultConfigurationByConfigurationId(firstValue.Id);
        Assert.NotEmpty(values);
        Assert.Equal(1, values.Count);
        var firstHeader = values.First();
        Assert.Equal(_nodePattern, firstHeader.NodePattern);
        Assert.Equal(_correctionFactor, firstHeader.CorrectionFactor);
        Assert.Equal(_valueUsage, firstHeader.UsedFor);
        Assert.Equal(_valueOperator, firstHeader.Operator);
    }

    [Fact]
    public async Task Can_Update_Rest_Result_Configurations()
    {
        await GenerateDemoData();
        var service = Mock.Create<TeslaSolarCharger.Services.Services.RestValueConfigurationService>();
        var restValueConfigurations = await service.GetAllRestValueConfigurations();
        var firstValue = restValueConfigurations.First();
        var values = await service.GetResultConfigurationByConfigurationId(firstValue.Id);
        var firstHeader = values.First();
        var newNodePattern = "$.data2";
        var newCorrectionFactor = 2;
        var newValueUsage = ValueUsage.InverterPower;
        var newValueOperator = ValueOperator.Minus;
        Assert.NotEqual(newNodePattern, firstHeader.NodePattern);
        Assert.NotEqual(newCorrectionFactor, firstHeader.CorrectionFactor);
        Assert.NotEqual(newValueUsage, firstHeader.UsedFor);
        Assert.NotEqual(newValueOperator, firstHeader.Operator);
        firstHeader.NodePattern = newNodePattern;
        firstHeader.CorrectionFactor = newCorrectionFactor;
        firstHeader.UsedFor = newValueUsage;
        firstHeader.Operator = newValueOperator;
        var id = await service.SaveResultConfiguration(firstValue.Id, firstHeader);
        Assert.Equal(firstHeader.Id, id);
    }

    private async Task GenerateDemoData()
    {
        Context.RestValueConfigurations.Add(new RestValueConfiguration()
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
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Entries().Where(e => e.State != EntityState.Detached).ToList()
            .ForEach(entry => entry.State = EntityState.Detached);
    }
}
