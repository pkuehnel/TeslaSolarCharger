using System.Collections.Generic;
using System.Text.Json;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Services.Services.Template.Infrastructure.Contracts;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Sma;
using TeslaSolarCharger.Shared.Enums;

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
                Key(new DtoBaseSmaInverterTemplateValueConfiguration().GatherType, AnyVersion),
                new TemplateValueConfigurationConverter<DtoBaseSmaInverterTemplateValueConfiguration, DtoSmaInverterTemplateValueConfiguration>()
            },
            {
                Key(new DtoBaseSmaHybridInverterTemplateValueConfiguration().GatherType, AnyVersion),
                new TemplateValueConfigurationConverter<DtoBaseSmaHybridInverterTemplateValueConfiguration, DtoSmaInverterTemplateValueConfiguration>()
            },
        };
    }

    public DtoTemplateValueConfigurationBase CreateDto(TemplateValueConfiguration entity)
    {
        var converter = GetConverter(entity.GatherType, entity.ConfigurationVersion);
        return converter.CreateDto(entity, _jsonOptions);
    }

    public TemplateValueConfiguration CreateEntity(DtoTemplateValueConfigurationBase dtoBase)
    {
        if (dtoBase.GatherType == default)
        {
            throw new InvalidOperationException("Gather type can not be null");
        }
        var converter = GetConverter(dtoBase.GatherType.Value, dtoBase.ConfigurationVersion);
        return converter.CreateEntity(dtoBase, _jsonOptions);
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
        DtoTemplateValueConfigurationBase CreateDto(TemplateValueConfiguration entity, JsonSerializerOptions options);
        TemplateValueConfiguration CreateEntity(DtoTemplateValueConfigurationBase dtoBase, JsonSerializerOptions options);
    }

    private sealed class TemplateValueConfigurationConverter<TDto, TConfig> : ITemplateValueConfigurationConverter
        where TDto : DtoGenericTemplateValueConfiguration<TConfig>, new()
        where TConfig : class
    {
        public DtoTemplateValueConfigurationBase CreateDto(TemplateValueConfiguration entity, JsonSerializerOptions options)
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

        public TemplateValueConfiguration CreateEntity(DtoTemplateValueConfigurationBase dtoBase, JsonSerializerOptions options)
        {
            if (dtoBase is not TDto typedDto)
            {
                throw new ArgumentException($"DTO must be of type {typeof(TDto).Name}", nameof(dtoBase));
            }

            if (dtoBase.Name == default)
            {
                throw new InvalidOperationException("Name can not be null");
            }

            if (dtoBase.GatherType == default)
            {
                throw new InvalidOperationException("Gather Type can not be null");
            }
            var entity = new TemplateValueConfiguration(dtoBase.Name)
            {
                Id = dtoBase.Id,
                ConfigurationVersion = dtoBase.ConfigurationVersion,
                MinRefreshIntervalMilliseconds = dtoBase.MinRefreshIntervalMilliseconds,
                GatherType = dtoBase.GatherType.Value,
            };

            if (typedDto.Configuration is not null)
            {
                entity.ConfigurationJson = JsonSerializer.Serialize(typedDto.Configuration, options);
            }

            return entity;
        }
    }
}
