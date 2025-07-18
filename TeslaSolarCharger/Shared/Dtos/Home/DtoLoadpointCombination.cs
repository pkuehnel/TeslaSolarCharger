namespace TeslaSolarCharger.Shared.Dtos.Home;

public class DtoLoadpointCombination
{
    public int? CarId { get; init; }
    public int? ChargingConnectorId { get; init; }

    public DtoLoadpointCombination(int? carId, int? chargingConnectorId)
    {
        CarId = carId;
        ChargingConnectorId = chargingConnectorId;
    }

    // Equals method for object comparison
    public override bool Equals(object? obj)
    {
        return Equals(obj as DtoLoadpointCombination);
    }

    // Type-safe Equals method
    private bool Equals(DtoLoadpointCombination? other)
    {
        if (other == default)
        {
            return false;
        }
        if (ReferenceEquals(this, other))
            return true;

        return CarId == other.CarId &&
               ChargingConnectorId == other.ChargingConnectorId;
    }

    // Override GetHashCode to maintain consistency with Equals
    public override int GetHashCode()
    {
        return HashCode.Combine(CarId, ChargingConnectorId);
    }

    // Implement equality operators for convenience
    public static bool operator ==(DtoLoadpointCombination? left, DtoLoadpointCombination? right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left is null || right is null)
            return false;

        return left.Equals(right);
    }

    public static bool operator !=(DtoLoadpointCombination? left, DtoLoadpointCombination? right)
    {
        return !(left == right);
    }
}
