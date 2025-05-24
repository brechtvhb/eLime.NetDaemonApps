namespace eLime.NetDaemonApps.Domain.EnergyManager;

public interface IGridMonitor : IDisposable
{
    Double CurrentLoad { get; }
    Double CurrentLoadMinusBatteries { get; }
    Double CurrentAverageDemand { get; }
    Double PeakLoad { get; }

    double AverageImportSince(DateTimeOffset now, TimeSpan timeSpan);
    double AverageExportSince(DateTimeOffset now, TimeSpan timeSpan);
    double AverageLoadSince(DateTimeOffset now, TimeSpan timeSpan);
    double AverageLoadMinusBatteriesSince(DateTimeOffset now, TimeSpan timeSpan);
}