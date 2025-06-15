namespace eLime.NetDaemonApps.Domain.CapacityCalculator;

internal class CapacityCalculatorStorage
{
    public decimal AverageCapacityLastYear { get; set; }

    public bool Equals(CapacityCalculatorStorage? r)
    {
        if (r == null)
            return false;

        return AverageCapacityLastYear == r.AverageCapacityLastYear;
    }
}