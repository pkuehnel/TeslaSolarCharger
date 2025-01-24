using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Services.Services.Rest.Contracts;
using TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;

namespace TeslaSolarCharger.Services.Services.Rest;

public class RestValueConfigurationService(
    ILogger<RestValueConfigurationService> logger,
    ITeslaSolarChargerContext context) : IRestValueConfigurationService
{
    public async Task<List<DtoRestValueConfiguration>> GetAllRestValueConfigurations()
    {
        logger.LogTrace("{method}()", nameof(GetAllRestValueConfigurations));
        var result = await context.RestValueConfigurations
            .Select(e => new DtoRestValueConfiguration()
            {
                Id = e.Id,
                Url = e.Url,
                NodePatternType = e.NodePatternType,
                HttpMethod = e.HttpMethod,
            })
            .ToListAsync().ConfigureAwait(false);
        return result;
    }

    public async Task<List<DtoFullRestValueConfiguration>> GetFullRestValueConfigurationsByPredicate(
        Expression<Func<RestValueConfiguration, bool>> predicate)
    {
        logger.LogTrace("{method}({predicate})", nameof(GetFullRestValueConfigurationsByPredicate), predicate);
        var restValueConfigurations = await context.RestValueConfigurations
            .Where(predicate)
            .Select(c => new DtoFullRestValueConfiguration()
            {
                Id = c.Id,
                Url = c.Url,
                NodePatternType = c.NodePatternType,
                HttpMethod = c.HttpMethod,
                Headers = c.Headers.Select(h => new DtoRestValueConfigurationHeader()
                {
                    Id = h.Id,
                    Key = h.Key,
                    Value = h.Value,
                }).ToList(),
            })
            .ToListAsync().ConfigureAwait(false);
        return restValueConfigurations;
    }

    public async Task<List<DtoJsonXmlResultConfiguration>> GetRestResultConfigurationByPredicate(
        Expression<Func<RestValueResultConfiguration, bool>> predicate)
    {
        var resultConfigurations = await context.RestValueResultConfigurations
            .Where(predicate)
            .Select(e => new DtoJsonXmlResultConfiguration()
            {
                Id = e.Id,
                CorrectionFactor = e.CorrectionFactor,
                UsedFor = e.UsedFor,
                Operator = e.Operator,
                NodePattern = e.NodePattern,
                XmlAttributeHeaderName = e.XmlAttributeHeaderName,
                XmlAttributeHeaderValue = e.XmlAttributeHeaderValue,
                XmlAttributeValueName = e.XmlAttributeValueName,
            })
            .ToListAsync().ConfigureAwait(false);
        return resultConfigurations;
    }

    public async Task<int> SaveRestValueConfiguration(DtoFullRestValueConfiguration dtoData)
    {
        logger.LogTrace("{method}({@dtoData})", nameof(SaveRestValueConfiguration), dtoData);
        var dbData = new RestValueConfiguration()
        {
            Id = dtoData.Id, Url = dtoData.Url, NodePatternType = dtoData.NodePatternType, HttpMethod = dtoData.HttpMethod,
        };
        if (dbData.Id == default)
        {
            context.RestValueConfigurations.Add(dbData);
        }
        else
        {
            var dtoHeaderIds = dtoData.Headers.Select(h => h.Id).ToList();
            var headersToRemove = await context.RestValueConfigurationHeaders
                .Where(x => x.RestValueConfigurationId == dbData.Id &&
                            !dtoHeaderIds.Contains(x.Id))
                .ToListAsync().ConfigureAwait(false);
            context.RestValueConfigurationHeaders.RemoveRange(headersToRemove);
            context.RestValueConfigurations.Update(dbData);
        }
        foreach (var dtoHeader in dtoData.Headers)
        {
            var dbHeader = new RestValueConfigurationHeader() { Id = dtoHeader.Id, Key = dtoHeader.Key, Value = dtoHeader.Value, };
            if (dbData.Id == default)
            {
                dbData.Headers.Add(dbHeader);
            }
            else
            {
                dbHeader.RestValueConfigurationId = dbData.Id;
                if (dbHeader.Id == default)
                {
                    context.RestValueConfigurationHeaders.Add(dbHeader);
                }
                else
                {
                    context.RestValueConfigurationHeaders.Update(dbHeader);
                }
            }
        }
        await context.SaveChangesAsync().ConfigureAwait(false);
        return dbData.Id;
    }

    public async Task<List<DtoRestValueConfigurationHeader>> GetHeadersByConfigurationId(int parentId)
    {
        logger.LogTrace("{method}({parentId})", nameof(GetHeadersByConfigurationId), parentId);
        return await context.RestValueConfigurationHeaders
            .Where(x => x.RestValueConfigurationId == parentId)
            .Select(e => new DtoRestValueConfigurationHeader()
            {
                Id = e.Id,
                Key = e.Key,
                Value = e.Value,
            })
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<int> SaveHeader(int parentId, DtoRestValueConfigurationHeader dtoData)
    {
        logger.LogTrace("{method}({@dtoData})", nameof(SaveHeader), dtoData);
        var dbData = new RestValueConfigurationHeader
        {
            Id = dtoData.Id,
            Key = dtoData.Key,
            Value = dtoData.Value,
            RestValueConfigurationId = parentId,
        };
        if (dbData.Id == default)
        {
            context.RestValueConfigurationHeaders.Add(dbData);
        }
        else
        {
            context.RestValueConfigurationHeaders.Update(dbData);
        }
        await context.SaveChangesAsync().ConfigureAwait(false);
        return dbData.Id;
    }

    public async Task DeleteHeader(int id)
    {
        logger.LogTrace("{method}({id})", nameof(DeleteHeader), id);
        context.RestValueConfigurationHeaders.Remove(new RestValueConfigurationHeader { Id = id });
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<List<DtoJsonXmlResultConfiguration>> GetResultConfigurationsByConfigurationId(int parentId)
    {
        logger.LogTrace("{method}({parentId})", nameof(GetResultConfigurationsByConfigurationId), parentId);
        return await context.RestValueResultConfigurations
            .Where(x => x.RestValueConfigurationId == parentId)
            .Select(e => new DtoJsonXmlResultConfiguration()
            {
                Id = e.Id,
                CorrectionFactor = e.CorrectionFactor,
                UsedFor = e.UsedFor,
                Operator = e.Operator,
                NodePattern = e.NodePattern,
                XmlAttributeHeaderName = e.XmlAttributeHeaderName,
                XmlAttributeHeaderValue = e.XmlAttributeHeaderValue,
                XmlAttributeValueName = e.XmlAttributeValueName,
            })
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<int> SaveResultConfiguration(int parentId, DtoJsonXmlResultConfiguration dtoData)
    {
        logger.LogTrace("{method}({@dtoData})", nameof(SaveResultConfiguration), dtoData);
        var dbData = new RestValueResultConfiguration
        {
            Id = dtoData.Id,
            CorrectionFactor = dtoData.CorrectionFactor,
            UsedFor = dtoData.UsedFor,
            Operator = dtoData.Operator,
            NodePattern = dtoData.NodePattern,
            XmlAttributeHeaderName = dtoData.XmlAttributeHeaderName,
            XmlAttributeHeaderValue = dtoData.XmlAttributeHeaderValue,
            XmlAttributeValueName = dtoData.XmlAttributeValueName,
            RestValueConfigurationId = parentId,
        };
        if (dbData.Id == default)
        {
            context.RestValueResultConfigurations.Add(dbData);
        }
        else
        {
            context.RestValueResultConfigurations.Update(dbData);
        }
        await context.SaveChangesAsync().ConfigureAwait(false);
        return dbData.Id;
    }

    public async Task DeleteResultConfiguration(int id)
    {
        logger.LogTrace("{method}({id})", nameof(DeleteResultConfiguration), id);
        context.RestValueResultConfigurations.Remove(new RestValueResultConfiguration { Id = id });
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task DeleteRestValueConfiguration(int id)
    {
        logger.LogTrace("{method}({id})", nameof(DeleteRestValueConfiguration), id);
        var restValueConfiguration = await context.RestValueConfigurations
            .Include(x => x.Headers)
            .Include(x => x.RestValueResultConfigurations)
            .FirstAsync(x => x.Id == id).ConfigureAwait(false);
        context.RestValueConfigurations.Remove(restValueConfiguration);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }
}
