using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.SharedModel.Enums;
using TeslaSolarCharger.Tests.Data;
using Xunit;
using Xunit.Abstractions;
#pragma warning disable xUnit2013

namespace TeslaSolarCharger.Tests.Services.Services;

[SuppressMessage("ReSharper", "UseConfigureAwaitFalse")]
public class RestValueConfigurationService(ITestOutputHelper outputHelper) : TestBase(outputHelper)
{
    

    [Fact]
    public async Task Can_Get_Rest_Configurations()
    {
        var service = Mock.Create<TeslaSolarCharger.Services.Services.RestValueConfigurationService>();
        var restValueConfigurations = await service.GetAllRestValueConfigurations();
        Assert.NotEmpty(restValueConfigurations);
        Assert.Equal(1, restValueConfigurations.Count);
        var firstValue = restValueConfigurations.First();
        Assert.Equal(DataGenerator._httpLocalhostApiValues, firstValue.Url);
        Assert.Equal(DataGenerator._nodePatternType, firstValue.NodePatternType);
        Assert.Equal(DataGenerator._httpMethod, firstValue.HttpMethod);
    }

    [Fact]
    public async Task Can_Get_PVValueRest_Configurations()
    {
        var service = Mock.Create<TeslaSolarCharger.Services.Services.RestValueConfigurationService>();
        var usedFors = new HashSet<ValueUsage>() { ValueUsage.InverterPower, ValueUsage.GridPower, };
        var restValueConfigurations = await service.GetFullRestValueConfigurationsByPredicate(c => c.RestValueResultConfigurations.Any(r => usedFors.Contains(r.UsedFor)));
        Assert.NotEmpty(restValueConfigurations);
        Assert.Equal(1, restValueConfigurations.Count);
        var firstValue = restValueConfigurations.First();
        Assert.Equal("http://localhost:5000/api/values", firstValue.Url);
        Assert.Equal(DataGenerator._nodePatternType, firstValue.NodePatternType);
        Assert.Equal(DataGenerator._httpMethod, firstValue.HttpMethod);
    }

    [Fact]
    public async Task Can_Update_Rest_Configurations()
    {
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
        var service = Mock.Create<TeslaSolarCharger.Services.Services.RestValueConfigurationService>();
        var restValueConfigurations = await service.GetAllRestValueConfigurations();
        var firstValue = restValueConfigurations.First();
        var headers = await service.GetHeadersByConfigurationId(firstValue.Id);
        Assert.NotEmpty(headers);
        Assert.Equal(1, headers.Count);
        var firstHeader = headers.First();
        Assert.Equal(DataGenerator._headerKey, firstHeader.Key);
        Assert.Equal(DataGenerator._headerValue, firstHeader.Value);
    }

    [Fact]
    public async Task Can_Update_Rest_Configuration_Headers()
    {
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
        var service = Mock.Create<TeslaSolarCharger.Services.Services.RestValueConfigurationService>();
        var restValueConfigurations = await service.GetAllRestValueConfigurations();
        var firstValue = restValueConfigurations.First();
        var values = await service.GetResultConfigurationsByConfigurationId(firstValue.Id);
        Assert.NotEmpty(values);
        Assert.Equal(4, values.Count);
        var firstHeader = values.First(v => v.UsedFor == ValueUsage.GridPower);
        Assert.Equal(DataGenerator._nodePattern, firstHeader.NodePattern);
        Assert.Equal(DataGenerator._correctionFactor, firstHeader.CorrectionFactor);
        Assert.Equal(DataGenerator._valueUsage, firstHeader.UsedFor);
        Assert.Equal(DataGenerator._valueOperator, firstHeader.Operator);
    }

    [Fact]
    public async Task Can_Update_Rest_Result_Configurations()
    {
        var service = Mock.Create<TeslaSolarCharger.Services.Services.RestValueConfigurationService>();
        var restValueConfigurations = await service.GetAllRestValueConfigurations();
        var firstValue = restValueConfigurations.First();
        var values = await service.GetResultConfigurationsByConfigurationId(firstValue.Id);
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
}
