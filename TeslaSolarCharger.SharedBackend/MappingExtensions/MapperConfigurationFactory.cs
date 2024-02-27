using AutoMapper;
using AutoMapper.Configuration.Annotations;
using System.Reflection;
using IConfigurationProvider = AutoMapper.IConfigurationProvider;

namespace TeslaSolarCharger.SharedBackend.MappingExtensions;

public class MapperConfigurationFactory : IMapperConfigurationFactory
{
    public IConfigurationProvider Create(Action<IMapperConfigurationExpression> configure)
    {
        return new MapperConfiguration(config =>
        {
            config.ShouldMapProperty = p => p.GetCustomAttribute<IgnoreAttribute>() == null;
            configure(config);
        });
    }

    public IConfigurationProvider CreateMap<TSource, TDestination>()
    {
        return new MapperConfiguration(config => config.CreateMap<TSource, TDestination>());
    }
}

public interface IMapperConfigurationFactory
{
    IConfigurationProvider Create(Action<IMapperConfigurationExpression> configure);
    IConfigurationProvider CreateMap<TSource, TDestination>();
}
