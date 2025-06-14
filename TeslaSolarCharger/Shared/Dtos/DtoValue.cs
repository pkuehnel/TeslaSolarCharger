namespace TeslaSolarCharger.Shared.Dtos;

public class DtoValue<T>
{
    private T? _value;
    public virtual T? Value
    {
        get => _value;
        set => _value = value;
    }

    // Base ctor no longer uses the virtual setter
    protected DtoValue() { }

    public DtoValue(T? value)
    {
        _value = value;   // direct field assignment
    }
}
