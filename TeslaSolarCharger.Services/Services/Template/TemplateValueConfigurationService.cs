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

    public async Task<TDto?> GetAsync<TDto>(int id) where TDto : class, ITemplateValueConfigurationDto
    {
        _logger.LogTrace("{method}({id})", nameof(GetAsync), id);
        var entity = await _context.TemplateValueConfigurations
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null)
            return null;

        var dto = _factory.CreateDto(entity);
        return dto as TDto;
    }

    public async Task<IEnumerable<ITemplateValueConfigurationDto>> GetAllAsync(Expression<Func<TemplateValueConfiguration, bool>> predicate)
    {
        _logger.LogTrace("{method}({predicate})", nameof(GetAllAsync), predicate);
        var entities = await _context.TemplateValueConfigurations
            .Where(predicate)
            .ToListAsync();

        return entities.Select(e => _factory.CreateDto(e));
    }

    public async Task<int> SaveAsync<TDto>(TDto dto) where TDto : class, ITemplateValueConfigurationDto
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
