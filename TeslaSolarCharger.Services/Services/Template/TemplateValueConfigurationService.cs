using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Services.Services.Template.Contracts;
using TeslaSolarCharger.Services.Services.Template.Infrastructure.Contracts;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration;

namespace TeslaSolarCharger.Services.Services.Template;

public class TemplateValueConfigurationService : ITemplateValueConfigurationService
{
    private readonly ILogger<TemplateValueConfigurationService> _logger;
    private readonly ITeslaSolarChargerContext _context;
    private readonly ITemplateValueConfigurationFactory _factory;

    public TemplateValueConfigurationService(ILogger<TemplateValueConfigurationService> logger,
        ITeslaSolarChargerContext context,
        ITemplateValueConfigurationFactory factory)
    {
        _logger = logger;
        _context = context;
        _factory = factory;
    }

    public async Task<DtoTemplateValueConfigurationBase> GetAsync(int id)
    {
        _logger.LogTrace("{method}({id})", nameof(GetAsync), id);
        var entity = await _context.TemplateValueConfigurations
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null)
        {
            throw new KeyNotFoundException($"Could not find entity with id {id} in {nameof(_context.TemplateValueConfigurations)}");
        }

        var dto = _factory.CreateDto(entity);
        return dto;
    }

    public async Task<IEnumerable<DtoTemplateValueConfigurationBase>> GetConfigurationsByPredicateAsync(Expression<Func<TemplateValueConfiguration, bool>> predicate)
    {
        _logger.LogTrace("{method}({predicate})", nameof(GetConfigurationsByPredicateAsync), predicate);
        var entities = await _context.TemplateValueConfigurations
            .Where(predicate)
            .ToListAsync();

        return entities.Select(e => _factory.CreateDto(e));
    }

    public async Task<int> SaveAsync<TDto>(TDto dto) where TDto : DtoTemplateValueConfigurationBase
    {
        _logger.LogTrace("{method}({@dto})", nameof(SaveAsync), dto);
        var entity = _factory.CreateEntity(dto);

        if (entity.Id == default)
        {
            _context.TemplateValueConfigurations.Add(entity);
        }
        else
        {
            _context.TemplateValueConfigurations.Update(entity);
        }

        await _context.SaveChangesAsync();
        return entity.Id;
    }

    public async Task DeleteAsync(int id)
    {
        _logger.LogTrace("{method}({id})", nameof(DeleteAsync), id);
        var entity = await _context.TemplateValueConfigurations
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity != null)
        {
            _context.TemplateValueConfigurations.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }
}
