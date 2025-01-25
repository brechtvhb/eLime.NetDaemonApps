using eLime.NetDaemonApps.Domain.SolarBackup.Clients.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

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

        _httpClient = new HttpClient(new HttpClientHandler
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        });
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authToken);
    }

    public async Task<string?> StartVerifyTask()
    {
        try
        {
            var result = await _httpClient.PostAsync($"/api2/json/admin/verify/{_verifyId}/run", null);
            if (!result.IsSuccessStatusCode)
                return null;

            var response = await result.Content.ReadFromJsonAsync<TaskResponse>();

            return response?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start verify task.");
            return null;
        }
    }

    public async Task<string?> StartPruneTask()
    {
        try
        {
            var result = await _httpClient.PostAsync($"/api2/json/admin/prune/{_pruneId}/run", null);
            if (!result.IsSuccessStatusCode)
                return null;

            var response = await result.Content.ReadFromJsonAsync<TaskResponse>();

            return response?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start prune task.");
            return null;
        }
    }

    public async Task<string?> StartGarbageCollectTask()
    {
        try
        {
            var result = await _httpClient.PostAsync($"/api2/json/admin/datastore/{_datastore}/gc", null);
            if (!result.IsSuccessStatusCode)
                return null;

            var response = await result.Content.ReadFromJsonAsync<TaskResponse>();

            return response?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start garbage collect task.");
            return null;
        }
    }

    public async Task<string?> Shutdown()
    {
        try
        {
            var result = await _httpClient.PostAsJsonAsync($"/api2/json/nodes/localhost/status", new StatusCommand { Command = Command.Shutdown });

            if (!result.IsSuccessStatusCode)
                return null;

            var response = await result.Content.ReadFromJsonAsync<TaskResponse>();

            return response?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to shut down PBS.");
            return null;
        }
    }

    public async Task<bool> IsOnline()
    {
        try
        {
            var result = await _httpClient.GetAsync($"/api2/json/nodes/localhost/status");

            return result.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> CheckIfTaskCompleted(string taskId)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<TaskList>("/api2/json/nodes/localhost/tasks");
            var task = response?.Data.SingleOrDefault(x => x.Upid == taskId);

            return task?.Status == "OK";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if task was completed.");
            return false;
        }
    }
}