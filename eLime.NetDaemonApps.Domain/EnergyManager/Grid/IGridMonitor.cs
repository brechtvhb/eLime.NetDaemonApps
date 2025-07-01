namespace eLime.NetDaemonApps.Domain.EnergyManager.Grid;

public interface IGridMonitor : IDisposable
{
    double CurrentLoad { get; }
    double CurrentLoadMinusBatteries { get; }
    double CurrentAverageDemand { get; }
    double PeakLoad { get; }

    double AverageImport(TimeSpan timeSpan);
    double AverageExport(TimeSpan timeSpan);
    double AverageLoad(TimeSpan timeSpan);
    double AverageLoadMinusBatteries(TimeSpan timeSpan);
}