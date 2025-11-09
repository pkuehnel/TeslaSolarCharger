using System.Text.Json;
using TeslaSolarCharger.Model.Entities;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Services.Services.Template.Infrastructure.Contracts;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Sma;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Services.Services.Template.Infrastructure;

public class TemplateValueConfigurationFactory : ITemplateValueConfigurationFactory
{
    private readonly JsonSerializerOptions _jsonOptions;

    public TemplateValueConfigurationFactory()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }

    public object CreateDto(TemplateValueConfiguration entity)
    {
        var dtoType = GetDtoType(entity.GatherType, entity.ConfigurationVersion);
        var dto = Activator.CreateInstance(dtoType)!;

        // Map basic properties
        var idProp = dtoType.GetProperty(nameof(DtoTemplateValueConfiguration<object>.Id));
        var nameProp = dtoType.GetProperty(nameof(DtoTemplateValueConfiguration<object>.Name));
        var versionProp = dtoType.GetProperty(nameof(DtoTemplateValueConfiguration<object>.ConfigurationVersion));
        var intervalProp = dtoType.GetProperty(nameof(DtoTemplateValueConfiguration<object>.MinRefreshIntervalMilliseconds));
        var configProp = dtoType.GetProperty(nameof(DtoTemplateValueConfiguration<object>.Configuration));

        idProp?.SetValue(dto, entity.Id);
        nameProp?.SetValue(dto, entity.Name);
        versionProp?.SetValue(dto, entity.ConfigurationVersion);
        intervalProp?.SetValue(dto, entity.MinRefreshIntervalMilliseconds);

        // Deserialize configuration
        if (!string.IsNullOrEmpty(entity.ConfigurationJson) && configProp != null)
        {
            var configType = configProp.PropertyType;
            var config = JsonSerializer.Deserialize(entity.ConfigurationJson, configType, _jsonOptions);
            configProp.SetValue(dto, config);
        }

        return dto;
    }

    public TemplateValueConfiguration CreateEntity(object dto)
    {
        var dtoType = dto.GetType();
        var idProp = dtoType.GetProperty(nameof(DtoTemplateValueConfiguration<object>.Id))!;
        var nameProp = dtoType.GetProperty(nameof(DtoTemplateValueConfiguration<object>.Name))!;
        var versionProp = dtoType.GetProperty(nameof(DtoTemplateValueConfiguration<object>.ConfigurationVersion))!;
        var intervalProp = dtoType.GetProperty(nameof(DtoTemplateValueConfiguration<object>.MinRefreshIntervalMilliseconds))!;
        var gatherTypeProp = dtoType.GetProperty(nameof(DtoTemplateValueConfiguration<object>.GatherType))!;
        var configProp = dtoType.GetProperty(nameof(DtoTemplateValueConfiguration<object>.Configuration));

        var entity = new TemplateValueConfiguration((string)nameProp.GetValue(dto)!)
        {
            Id = (int)idProp.GetValue(dto)!,
            ConfigurationVersion = (int)versionProp.GetValue(dto)!,
            MinRefreshIntervalMilliseconds = (int?)intervalProp.GetValue(dto),
            GatherType = (TemplateValueGatherType)gatherTypeProp.GetValue(dto)!,
        };

        // Serialize configuration
        if (configProp != null)
        {
            var config = configProp.GetValue(dto);
            if (config != null)
            {
                entity.ConfigurationJson = JsonSerializer.Serialize(config, _jsonOptions);
            }
        }

        return entity;
    }

    private Type GetDtoType(TemplateValueGatherType gatherType, int version)
    {
        // This could be moved to a registry pattern for better extensibility
        return (gatherType, version) switch
        {
            (TemplateValueGatherType.SmaInverterModbus, _) => typeof(DtoSmaInverterTemplateValueConfiguration),
            (TemplateValueGatherType.SmaHybridInverterModbus, _) => typeof(DtoSmaHybridInverterTemplateValueConfiguration),
            _ => throw new NotSupportedException($"Gather type {gatherType} version {version} is not supported"),
        };
    }
}
