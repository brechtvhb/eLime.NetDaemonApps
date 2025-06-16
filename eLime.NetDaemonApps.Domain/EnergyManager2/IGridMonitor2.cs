namespace eLime.NetDaemonApps.Domain.EnergyManager2;

public interface IGridMonitor2 : IDisposable
{
    double CurrentLoad { get; }
    double CurrentLoadMinusBatteries { get; }
    double CurrentAverageDemand { get; }
    double PeakLoad { get; }

    double AverageImportSince(TimeSpan timeSpan);
    double AverageExportSince(TimeSpan timeSpan);
    double AverageLoadSince(TimeSpan timeSpan);
    double AverageLoadMinusBatteriesSince(TimeSpan timeSpan);
}