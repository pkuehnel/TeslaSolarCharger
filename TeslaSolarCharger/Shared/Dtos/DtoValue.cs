namespace TeslaSolarCharger.Shared.Dtos;

public class DtoValue<T>
{
    public DtoValue()
    {
    }

    public DtoValue(T? value)
    {
        Value = value;
    }

    public T? Value;
}
