using eLime.NetDaemonApps.Domain.SolarBackup.Clients.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace eLime.NetDaemonApps.Domain.SolarBackup.Clients;

public class PveClient
{
    private readonly ILogger _logger;
    private readonly string _cluster;
    private readonly string _pbsStorageId;
    private readonly string _pbsStorageName;
    private readonly HttpClient _httpClient;

    public PveClient(ILogger logger, string baseUrl, string authToken, string cluster, string pbsStorageId, string pbsStorageName)
    {
        _logger = logger;
        _cluster = cluster;

        _httpClient = new HttpClient(new HttpClientHandler
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        });
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authToken);

        _pbsStorageId = pbsStorageId;
        _pbsStorageName = pbsStorageName;
    }

    public async Task<bool> CheckPbsStorageStatus()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<DataStoreList>("/api2/json/cluster/resources");
            var pbsDataStore = response?.Data.SingleOrDefault(x => x.Id == _pbsStorageId);

            if (pbsDataStore == null)
                return false;

            if (pbsDataStore.Content != "backup")
                return false;

            return pbsDataStore.Status is DataStoreStatus.Available or DataStoreStatus.Online;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check PBS storage status.");
            return false;
        }
    }

    public async Task<string?> StartBackup()
    {
        try
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
            var content = JsonContent.Create(backupTask);
            await content.LoadIntoBufferAsync();
            var result = await _httpClient.PostAsync($"/api2/json/nodes/{_cluster}/vzdump", content);
            if (!result.IsSuccessStatusCode)
                return null;

            var response = await result.Content.ReadFromJsonAsync<TaskResponse>();

            return response?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start backup task.");
            return null;
        }
    }

    public async Task<bool> CheckIfTaskCompleted(string taskId)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<TaskList>("/api2/json/cluster/tasks");
            var backupTask = response?.Data.SingleOrDefault(x => x.Upid == taskId);

            return backupTask?.Status == "OK";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if backup task was completed.");
            return false;
        }
    }
}