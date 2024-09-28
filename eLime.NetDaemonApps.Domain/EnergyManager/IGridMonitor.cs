namespace eLime.NetDaemonApps.Domain.EnergyManager;

public interface IGridMonitor : IDisposable
{
    double CurrentLoad { get; }
    Double PeakLoad { get; }
    double AverageImportSince(DateTimeOffset now, TimeSpan timeSpan);
    double AverageExportSince(DateTimeOffset now, TimeSpan timeSpan);
    double AverageLoadSince(DateTimeOffset now, TimeSpan timeSpan);
}