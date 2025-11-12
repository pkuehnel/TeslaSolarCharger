namespace TeslaSolarCharger.Shared.Dtos.TemplateConfiguration;

public abstract class DtoGenericTemplateValueConfiguration<TConfig> : DtoTemplateValueConfigurationBase where TConfig : class
{
    public new TConfig? Configuration { get; set; }
}
