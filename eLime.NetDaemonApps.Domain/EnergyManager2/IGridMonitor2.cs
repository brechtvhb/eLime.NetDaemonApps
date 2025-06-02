namespace eLime.NetDaemonApps.Domain.EnergyManager2;

public interface IGridMonitor2 : IDisposable
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