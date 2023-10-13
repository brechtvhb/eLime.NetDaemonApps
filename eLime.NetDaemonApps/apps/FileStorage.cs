using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.Options;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.apps;

public class FileStorage : IFileStorage
{
    private readonly string _dataStoragePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public FileStorage(IOptions<AppConfigurationLocationSetting> appConfigurationLocationSettings)
    {
        _dataStoragePath = Path.Combine(appConfigurationLocationSettings.Value.ApplicationConfigurationFolder, ".storage");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters ={
                new JsonStringEnumConverter()
            },
        };
    }

    public T? Get<T>(string app, string id) where T : class
    {
        try
        {
            var storageJsonFile = Path.Combine(_dataStoragePath, app, $"{id}.json");

            if (!File.Exists(storageJsonFile))
                return null;

            using var jsonStream = File.OpenRead(storageJsonFile);

            return JsonSerializer.Deserialize<T>(jsonStream, _jsonOptions);
        }
        catch
        {
            // We ignore errors, we will be adding logging later see issue #403
        }
        return default;
    }

    public void Save<T>(string app, string id, T data) where T : class
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        SaveInternal(app, id, data);
    }

    private void SaveInternal<T>(string app, string id, T data)
    {
        var folder = Path.Combine(_dataStoragePath, app);
        EnsureDirectoryExists(folder);

        var storageJsonFile = Path.Combine(folder, $"{id}.json");

        using var jsonStream = File.Open(storageJsonFile, FileMode.Create, FileAccess.Write);

        JsonSerializer.Serialize(jsonStream, data);
    }

    private void EnsureDirectoryExists(String directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath ?? throw new InvalidOperationException());
        }
    }
}