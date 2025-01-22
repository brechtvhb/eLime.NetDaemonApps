using eLime.NetDaemonApps.Domain.SolarBackup.Clients.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using TaskStatus = eLime.NetDaemonApps.Domain.SolarBackup.Clients.Models.TaskStatus;

namespace eLime.NetDaemonApps.Domain.SolarBackup.Clients;

public class PveClient
{
    private readonly ILogger _logger;
    private readonly string _cluster;
    private readonly string _pbsStorageName;
    private readonly HttpClient _httpClient;

    public PveClient(ILogger logger, string baseUrl, string authToken, string cluster, string pbsStorageName)
    {
        _logger = logger;
        _cluster = cluster;

        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Add("Authorization", authToken);

        _pbsStorageName = pbsStorageName;
    }

    public async Task<bool> CheckPbsStorageStatus()
    {
        var response = await _httpClient.GetFromJsonAsync<DataStoreList>("/api2/json/cluster/resources");
        var pbsDataStore = response?.Data.SingleOrDefault(x => x.Storage == _pbsStorageName);

        if (pbsDataStore == null)
            return false;

        if (pbsDataStore.Content != "backup")
            return false;

        return pbsDataStore.Status is DataStoreStatus.Available or DataStoreStatus.Online;
    }

    public async Task<string?> StartBackup()
    {
        var backupTask = new BackupTask
        {
            Mode = BackupMode.Snapshot,
            Storage = _pbsStorageName,
            All = 1,
            PruneBackups = "keep-daily=1,keep-hourly=1,keep-last=6,keep-monthly=3,keep-weekly=5,keep-yearly=2",
            NotificationMode = "notification-system",
            Fleecing = "enabled=0",
            NotesTemplate = "{{guestname}} (solarbackup)"
        };
        var result = await _httpClient.PostAsJsonAsync($"/api2/extjs/nodes/{_cluster}/vzdump", backupTask);
        if (!result.IsSuccessStatusCode)
            return null;

        var response = await result.Content.ReadFromJsonAsync<TaskResponse>();

        return response?.Success == 1 ? response.Data : null;
    }

    public async Task<bool> CheckIfTaskCompleted(string taskId)
    {
        var response = await _httpClient.GetFromJsonAsync<TaskList>("/api2/json/cluster/tasks");
        var backupTask = response?.Data.SingleOrDefault(x => x.Upid == taskId);

        return backupTask?.Status is TaskStatus.Ok;
    }
}