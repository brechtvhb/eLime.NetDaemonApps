using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Mqtt;
using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel;
using System.Globalization;
using System.Net.Http.Json;
using System.Reactive.Concurrency;
using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.CapacityCalculator;

public class CapacityCalculator
{
    private String SmartGatewayMeterUrl { get; set; }

    private IDisposable? GuardTask { get; set; }

    private readonly IHaContext _haContext;
    private readonly ILogger _logger;
    private readonly IScheduler _scheduler;
    private readonly IMqttEntityManager _mqttEntityManager;
    private readonly IFileStorage _fileStorage;
    private CapacityCalculatorStorage? _lastState;

    public Decimal AverageCapacityPastYear { get; private set; }

    public CapacityCalculator(IHaContext haContext, ILogger logger, IScheduler scheduler, IMqttEntityManager mqttEntityManager, IFileStorage fileStorage,
        String smartMeterUrl)
    {
        _haContext = haContext;
        _logger = logger;
        _scheduler = scheduler;
        _mqttEntityManager = mqttEntityManager;
        _fileStorage = fileStorage;

        SmartGatewayMeterUrl = smartMeterUrl;

        EnsureSensorsExist().RunSync();
        InitializeState();

        //Testing time zone shit
        var startTime = new TimeOnly(00, 01);
        var startFrom = startTime.GetUtcDateTimeFromLocalTimeOnly(scheduler.Now.DateTime).AddDays(1);

        _logger.LogInformation($"Will poll smart meter daily starting from: {startFrom:O}");
        GuardTask = _scheduler.RunEvery(TimeSpan.FromDays(1), startFrom, () =>
        {
            CalculateAveragePeak().RunSync();
        });
    }

    private async Task CalculateAveragePeak()
    {
        var client = new HttpClient();
        var model = await client.GetFromJsonAsync<SmartGateWayModel>(SmartGatewayMeterUrl);

        //Quick & dirty, Tahon would be proud
        var peakPerMonth = model.PeakConsumptionLast13Months.Split("*kW)(").Select(x => decimal.Parse(x.Replace("*kW)", "")[(x.LastIndexOf(")(") + 2)..], new NumberFormatInfo { NumberDecimalSeparator = "." })).Skip(1);
        AverageCapacityPastYear = Math.Round(peakPerMonth.Average(), 3);
        await UpdateStateInHomeAssistant();
    }
    public Device GetDevice() => new() { Identifiers = [$"capacity_calculator"], Name = "Capacity calculator", Manufacturer = "Me" };

    private async Task EnsureSensorsExist()
    {
        var sensorName = $"sensor.average_capacity_past_year";

        _logger.LogTrace("Creating entities in home assistant.");
        var sensor = new NumericSensorOptions { Icon = "fapro:bolt", Device = GetDevice(), UnitOfMeasurement = "kW" };
        await _mqttEntityManager.CreateAsync(sensorName, new EntityCreationOptions(UniqueId: sensorName, Name: $"Average capacity past 12 months", DeviceClass: "power", Persist: true), sensor);
    }

    private void InitializeState()
    {
        var storedState = _fileStorage.Get<CapacityCalculator>("CapacityCalculator", "CapacityCalculator");

        if (storedState == null)
            return;

        AverageCapacityPastYear = storedState.AverageCapacityPastYear;

        _logger.LogDebug($"Retrieved smart capacity calculator state. Average capacity past year was {AverageCapacityPastYear} kW.");
    }


    private async Task UpdateStateInHomeAssistant()
    {
        var fileStorage = ToFileStorage();
        if (fileStorage.Equals(_lastState))
            return;

        var sensorName = $"sensor.average_capacity_past_year";

        await _mqttEntityManager.SetStateAsync(sensorName, AverageCapacityPastYear.ToString("N", new NumberFormatInfo { NumberDecimalSeparator = "." }));
        _logger.LogInformation($"Updated average capacity to: {AverageCapacityPastYear.ToString("N", new NumberFormatInfo { NumberDecimalSeparator = "." })}");
        _fileStorage.Save("CapacityCalculator", "CapacityCalculator", fileStorage);
        _lastState = fileStorage;
    }


    internal CapacityCalculatorStorage ToFileStorage() => new()
    {
        AverageCapacityLastYear = AverageCapacityPastYear
    };


    public void Dispose()
    {
        _logger.LogInformation("Disposing");
        GuardTask?.Dispose();

        _logger.LogInformation("Disposed");
    }

}

public class SmartGateWayModel
{
    [JsonPropertyName("mac_address")]
    public String MacAddress { get; set; }

    [JsonPropertyName("PeakConsumptionLast13Months")]
    public String PeakConsumptionLast13Months { get; set; }
}