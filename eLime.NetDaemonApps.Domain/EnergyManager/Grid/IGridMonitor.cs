namespace eLime.NetDaemonApps.Domain.EnergyManager.Grid;

public interface IGridMonitor : IDisposable
{
    double CurrentLoad { get; }
    double CurrentLoadMinusBatteries { get; }
    double CurrentAverageDemand { get; }
    double PeakLoad { get; }

    double AverageLoad(TimeSpan timeSpan);
    double AverageLoadMinusBatteries(TimeSpan timeSpan);
}