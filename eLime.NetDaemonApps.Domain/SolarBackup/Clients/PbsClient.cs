using eLime.NetDaemonApps.Domain.SolarBackup.Clients.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using TaskStatus = eLime.NetDaemonApps.Domain.SolarBackup.Clients.Models.TaskStatus;

namespace eLime.NetDaemonApps.Domain.SolarBackup.Clients;

public class PbsClient
{
    private readonly ILogger _logger;
    private readonly string _datastore;
    private readonly string _verifyId;
    private readonly string _pruneId;
    private readonly HttpClient _httpClient;

    public PbsClient(ILogger logger, string baseUrl, string authToken, string datastore, string verifyId, string pruneId)
    {
        _logger = logger;
        _datastore = datastore;
        _verifyId = verifyId;
        _pruneId = pruneId;

        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Add("Authorization", authToken);

    }

    public async Task<string?> StartVerifyTask()
    {
        var result = await _httpClient.PostAsync($"/api2/json/admin/verify/{_verifyId}/run", new StringContent(""));
        if (!result.IsSuccessStatusCode)
            return null;

        var response = await result.Content.ReadFromJsonAsync<TaskResponse>();

        return response?.Data;
    }

    public async Task<string?> StartPruneTask()
    {
        var result = await _httpClient.PostAsync($"/api2/json/admin/prune/{_pruneId}/run", new StringContent(""));
        if (!result.IsSuccessStatusCode)
            return null;

        var response = await result.Content.ReadFromJsonAsync<TaskResponse>();

        return response?.Data;
    }

    public async Task<string?> StartGarbageCollectTask()
    {
        var result = await _httpClient.PostAsync($"/api2/json/admin/datastore/{_datastore}/gc", new StringContent(""));
        if (!result.IsSuccessStatusCode)
            return null;

        var response = await result.Content.ReadFromJsonAsync<TaskResponse>();

        return response?.Data;
    }

    public async Task<string?> Shutdown()
    {
        var result = await _httpClient.PostAsJsonAsync($"/api2/json/nodes/localhost/status", new StatusCommand { Command = Command.Shutdown });

        if (!result.IsSuccessStatusCode)
            return null;

        var response = await result.Content.ReadFromJsonAsync<TaskResponse>();

        return response?.Data;
    }

    public async Task<bool> CheckIfTaskCompleted(string taskId)
    {
        var response = await _httpClient.GetFromJsonAsync<TaskList>("/api2/json/nodes/localhost/tasks");
        var task = response?.Data.SingleOrDefault(x => x.Upid == taskId);

        return task?.Status is TaskStatus.Ok;
    }
}