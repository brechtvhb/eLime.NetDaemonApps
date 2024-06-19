namespace eLime.NetDaemonApps.Domain.CapacityCalculator;

internal class CapacityCalculatorStorage
{
    public Decimal AverageCapacityLastYear { get; set; }

    public bool Equals(CapacityCalculatorStorage? r)
    {
        if (r == null)
            return false;

        return AverageCapacityLastYear == r.AverageCapacityLastYear;
    }
}