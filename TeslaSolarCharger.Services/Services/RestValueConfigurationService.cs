using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
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

    public async Task<List<DtoFullRestValueConfiguration>> GetFullRestValueConfigurationsByPredicate(
        Expression<Func<RestValueConfiguration, bool>> predicate)
    {

        var mapper = mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<RestValueConfigurationHeader, DtoRestValueConfigurationHeader>();
            cfg.CreateMap<RestValueConfiguration, DtoFullRestValueConfiguration>()
                .ForMember(d => d.Headers, opt => opt.MapFrom(s => s.Headers))
                ;
        });
        var resultConfigurations = await context.RestValueConfigurations
            .Where(predicate)
            .ProjectTo<DtoFullRestValueConfiguration>(mapper)
            .ToListAsync().ConfigureAwait(false);
        return resultConfigurations;
    }

    public async Task<List<DtoRestValueResultConfiguration>> GetRestResultConfigurationByPredicate(
        Expression<Func<RestValueResultConfiguration, bool>> predicate)
    {

        var mapper = mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<RestValueResultConfiguration, DtoRestValueResultConfiguration>()
                ;
        });
        var resultConfigurations = await context.RestValueResultConfigurations
            .Where(predicate)
            .ProjectTo<DtoRestValueResultConfiguration>(mapper)
            .ToListAsync().ConfigureAwait(false);
        return resultConfigurations;
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

    public async Task DeleteHeader(int id)
    {
        logger.LogTrace("{method}({id})", nameof(DeleteHeader), id);
        context.RestValueConfigurationHeaders.Remove(new RestValueConfigurationHeader { Id = id });
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<List<DtoRestValueResultConfiguration>> GetResultConfigurationsByConfigurationId(int parentId)
    {
        logger.LogTrace("{method}({parentId})", nameof(GetResultConfigurationsByConfigurationId), parentId);
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

    public async Task DeleteResultConfiguration(int id)
    {
        logger.LogTrace("{method}({id})", nameof(DeleteResultConfiguration), id);
        context.RestValueResultConfigurations.Remove(new RestValueResultConfiguration { Id = id });
        await context.SaveChangesAsync().ConfigureAwait(false);
    }
}
