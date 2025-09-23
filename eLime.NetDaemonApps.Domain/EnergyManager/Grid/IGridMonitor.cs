namespace eLime.NetDaemonApps.Domain.EnergyManager.Grid;

public interface IGridMonitor : IDisposable
{
    double CurrentLoad { get; }

    double CurrentLoadMinusBatteries { get; }

    double CurrentLoadMinusBatteriesSolarCorrected { get; }
    double CurrentLoadMinusBatteriesSolarCorrected50Percent { get; }
    double CurrentLoadMinusBatteriesSolarForecast30MinutesCorrected { get; }
    double CurrentLoadMinusBatteriesSolarForecast1HourCorrected { get; }


    double CurrentAverageDemand { get; }
    double PeakLoad { get; }

    double AverageLoad(TimeSpan timeSpan);
    double AverageLoadMinusBatteries(TimeSpan timeSpan);
}