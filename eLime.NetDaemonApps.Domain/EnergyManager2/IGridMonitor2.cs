namespace eLime.NetDaemonApps.Domain.EnergyManager2;

public interface IGridMonitor2 : IDisposable
{
    double CurrentLoad { get; }
    double CurrentLoadMinusBatteries { get; }
    double CurrentAverageDemand { get; }
    double PeakLoad { get; }

    double AverageImportSince(DateTimeOffset now, TimeSpan timeSpan);
    double AverageExportSince(DateTimeOffset now, TimeSpan timeSpan);
    double AverageLoadSince(DateTimeOffset now, TimeSpan timeSpan);
    double AverageLoadMinusBatteriesSince(DateTimeOffset now, TimeSpan timeSpan);
}