using System.Collections.Generic;
using System.Text.Json;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Services.Services.Template.Infrastructure.Contracts;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Sma;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Services.Services.Template.Infrastructure;

public class TemplateValueConfigurationFactory : ITemplateValueConfigurationFactory
{
    private const int AnyVersion = -1;

    private static readonly IReadOnlyDictionary<(TemplateValueGatherType GatherType, int Version), ITemplateValueConfigurationConverter> Converters;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    static TemplateValueConfigurationFactory()
    {
        Converters = new Dictionary<(TemplateValueGatherType GatherType, int Version), ITemplateValueConfigurationConverter>
        {
            {
                Key(new DtoSmaInverterTemplateValueConfiguration().GatherType, AnyVersion),
                new TemplateValueConfigurationConverter<DtoSmaInverterTemplateValueConfiguration, DtoSmaTemplateValueConfguration>()
            },
            {
                Key(new DtoSmaHybridInverterTemplateValueConfiguration().GatherType, AnyVersion),
                new TemplateValueConfigurationConverter<DtoSmaHybridInverterTemplateValueConfiguration, DtoSmaTemplateValueConfguration>()
            },
        };
    }

    public ITemplateValueConfigurationDto CreateDto(TemplateValueConfiguration entity)
    {
        var converter = GetConverter(entity.GatherType, entity.ConfigurationVersion);
        return converter.CreateDto(entity, _jsonOptions);
    }

    public TemplateValueConfiguration CreateEntity(ITemplateValueConfigurationDto dto)
    {
        var converter = GetConverter(dto.GatherType, dto.ConfigurationVersion);
        return converter.CreateEntity(dto, _jsonOptions);
    }

    private static ITemplateValueConfigurationConverter GetConverter(TemplateValueGatherType gatherType, int version)
    {
        if (Converters.TryGetValue(Key(gatherType, version), out var converter))
        {
            return converter;
        }

        if (Converters.TryGetValue(Key(gatherType, AnyVersion), out converter))
        {
            return converter;
        }

        throw new NotSupportedException($"Gather type {gatherType} version {version} is not supported");
    }

    private static (TemplateValueGatherType GatherType, int Version) Key(TemplateValueGatherType gatherType, int version) => (gatherType, version);

    private interface ITemplateValueConfigurationConverter
    {
        ITemplateValueConfigurationDto CreateDto(TemplateValueConfiguration entity, JsonSerializerOptions options);
        TemplateValueConfiguration CreateEntity(ITemplateValueConfigurationDto dto, JsonSerializerOptions options);
    }

    private sealed class TemplateValueConfigurationConverter<TDto, TConfig> : ITemplateValueConfigurationConverter
        where TDto : DtoTemplateValueConfiguration<TConfig>, new()
        where TConfig : class
    {
        public ITemplateValueConfigurationDto CreateDto(TemplateValueConfiguration entity, JsonSerializerOptions options)
        {
            var dto = new TDto
            {
                Id = entity.Id,
                Name = entity.Name,
                ConfigurationVersion = entity.ConfigurationVersion,
                MinRefreshIntervalMilliseconds = entity.MinRefreshIntervalMilliseconds,
            };

            if (!string.IsNullOrWhiteSpace(entity.ConfigurationJson))
            {
                dto.Configuration = JsonSerializer.Deserialize<TConfig>(entity.ConfigurationJson, options);
            }

            return dto;
        }

        public TemplateValueConfiguration CreateEntity(ITemplateValueConfigurationDto dto, JsonSerializerOptions options)
        {
            if (dto is not TDto typedDto)
            {
                throw new ArgumentException($"DTO must be of type {typeof(TDto).Name}", nameof(dto));
            }

            var entity = new TemplateValueConfiguration(dto.Name)
            {
                Id = dto.Id,
                ConfigurationVersion = dto.ConfigurationVersion,
                MinRefreshIntervalMilliseconds = dto.MinRefreshIntervalMilliseconds,
                GatherType = dto.GatherType,
            };

            if (typedDto.Configuration is not null)
            {
                entity.ConfigurationJson = JsonSerializer.Serialize(typedDto.Configuration, options);
            }

            return entity;
        }
    }
}
