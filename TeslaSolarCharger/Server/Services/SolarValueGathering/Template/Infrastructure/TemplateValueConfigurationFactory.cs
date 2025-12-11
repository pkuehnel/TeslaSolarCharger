using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.Infrastructure.Contracts;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration;
using TeslaSolarCharger.Shared.Helper;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Template.Infrastructure;

public class TemplateValueConfigurationFactory : ITemplateValueConfigurationFactory
{
    public DtoTemplateValueConfigurationBase CreateDto(TemplateValueConfiguration entity)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));

        var dto = new DtoTemplateValueConfigurationBase
        {
            Id = entity.Id,
            Name = entity.Name,
            MinRefreshIntervalMilliseconds = entity.MinRefreshIntervalMilliseconds,
            GatherType = entity.GatherType,
        };

        if (!string.IsNullOrWhiteSpace(entity.ConfigurationJson))
        {
            var expectedType = TemplateValueConfigurationTypeHelper
                .GetConfigurationType(entity.GatherType);

            if (expectedType == default)
            {
                throw new KeyNotFoundException($"Could not find correct object type for GatherType {entity.GatherType}");
            }
            var typedConfig = JsonConvert.DeserializeObject(
                entity.ConfigurationJson,
                expectedType);

            if (typedConfig == null)
            {
                throw new JsonException(
                    $"ConfigurationJson could not be deserialized as {expectedType.Name}.");
            }

            // Expose as JObject on the DTO
            dto.Configuration = JObject.FromObject(typedConfig);
        }

        return dto;
    }

    public TemplateValueConfiguration CreateEntity(DtoTemplateValueConfigurationBase dtoBase)
    {
        if (dtoBase == null) throw new ArgumentNullException(nameof(dtoBase));
        if (string.IsNullOrWhiteSpace(dtoBase.Name))
            throw new ArgumentException("Name must be provided.", nameof(dtoBase));

        if (!dtoBase.GatherType.HasValue)
            throw new ArgumentException("GatherType must be provided.", nameof(dtoBase));

        var entity = new TemplateValueConfiguration(dtoBase.Name)
        {
            Id = dtoBase.Id,
            MinRefreshIntervalMilliseconds = dtoBase.MinRefreshIntervalMilliseconds,
            GatherType = dtoBase.GatherType.Value,
        };

        if (dtoBase.Configuration != null)
        {
            var expectedType = TemplateValueConfigurationTypeHelper
                .GetConfigurationType(entity.GatherType);

            if (expectedType != null)
            {
                // Validate JObject against expected type
                var typedConfig = dtoBase.Configuration.ToObject(expectedType);
                if (typedConfig == null)
                {
                    throw new JsonException(
                        $"Configuration does not match expected type {expectedType.Name}.");
                }

                // Serialize back as JSON string
                entity.ConfigurationJson = JsonConvert.SerializeObject(typedConfig);
            }
            else
            {
                // No specific type known: just store JSON as-is
                entity.ConfigurationJson = dtoBase.Configuration.ToString(Formatting.None);
            }
        }
        else
        {
            entity.ConfigurationJson = null;
        }

        return entity;
    }
}
