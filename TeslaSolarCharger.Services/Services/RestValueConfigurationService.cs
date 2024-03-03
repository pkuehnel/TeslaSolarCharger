using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Services.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;
using TeslaSolarCharger.SharedBackend.MappingExtensions;

namespace TeslaSolarCharger.Services.Services;

public class RestValueConfigurationService(
    ILogger<RestValueConfigurationService> logger,
    ITeslaSolarChargerContext context,
    IMapperConfigurationFactory mapperConfigurationFactory) : IRestValueConfigurationService
{
    public async Task<List<DtoRestValueConfiguration>> GetAllRestValueConfigurations()
    {
        logger.LogTrace("{method}()", nameof(GetAllRestValueConfigurations));
        var mapper = mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<RestValueConfiguration, DtoRestValueConfiguration>()
                ;
        });

        var result = await context.RestValueConfigurations
            .ProjectTo<DtoRestValueConfiguration>(mapper)
            .ToListAsync().ConfigureAwait(false);
        return result;
    }

    public async Task<int> SaveRestValueConfiguration(DtoRestValueConfiguration dtoData)
    {
        logger.LogTrace("{method}({@dtoData})", nameof(SaveRestValueConfiguration), dtoData);
        var mapperConfiguration = mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<DtoRestValueConfiguration, RestValueConfiguration>()
                ;
        });

        var mapper = mapperConfiguration.CreateMapper();
        var dbData = mapper.Map<RestValueConfiguration>(dtoData);
        if (dbData.Id == default)
        {
            context.RestValueConfigurations.Add(dbData);
        }
        else
        {
            context.RestValueConfigurations.Update(dbData);
        }
        await context.SaveChangesAsync().ConfigureAwait(false);
        return dbData.Id;
    }

    public async Task<List<DtoRestValueConfigurationHeader>> GetHeadersByConfigurationId(int parentId)
    {
        logger.LogTrace("{method}({parentId})", nameof(GetHeadersByConfigurationId), parentId);
        var mapper = mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<RestValueConfigurationHeader, DtoRestValueConfigurationHeader>()
                ;
        });
        return await context.RestValueConfigurationHeaders
            .Where(x => x.RestValueConfigurationId == parentId)
            .ProjectTo<DtoRestValueConfigurationHeader>(mapper)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<int> SaveHeader(int parentId, DtoRestValueConfigurationHeader dtoData)
    {
        logger.LogTrace("{method}({@dtoData})", nameof(SaveHeader), dtoData);
        var mapperConfiguration = mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<DtoRestValueConfigurationHeader, RestValueConfigurationHeader>()
                ;
        });

        var mapper = mapperConfiguration.CreateMapper();
        var dbData = mapper.Map<RestValueConfigurationHeader>(dtoData);
        dbData.RestValueConfigurationId = parentId;
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

    public async Task<List<DtoRestValueResultConfiguration>> GetResultConfigurationByConfigurationId(int parentId)
    {
        logger.LogTrace("{method}({parentId})", nameof(GetResultConfigurationByConfigurationId), parentId);
        var mapper = mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<RestValueResultConfiguration, DtoRestValueResultConfiguration>()
                ;
        });
        return await context.RestValueResultConfigurations
            .Where(x => x.RestValueConfigurationId == parentId)
            .ProjectTo<DtoRestValueResultConfiguration>(mapper)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<int> SaveResultConfiguration(int parentId, DtoRestValueResultConfiguration dtoData)
    {
        logger.LogTrace("{method}({@dtoData})", nameof(SaveResultConfiguration), dtoData);
        var mapperConfiguration = mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<DtoRestValueResultConfiguration, RestValueResultConfiguration>()
                ;
        });

        var mapper = mapperConfiguration.CreateMapper();
        var dbData = mapper.Map<RestValueResultConfiguration>(dtoData);
        dbData.RestValueConfigurationId = parentId;
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
}
