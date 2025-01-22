using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.SolarBackup.Clients.Models;

public class BackupTask
{
    [JsonPropertyName("notes-template")]
    public string NotesTemplate { get; set; }

    [JsonPropertyName("mode")]
    public BackupMode Mode { get; set; }

    [JsonPropertyName("prune-backups")]
    public string PruneBackups { get; set; }

    [JsonPropertyName("notification-mode")]
    public string NotificationMode { get; set; }

    [JsonPropertyName("storage")]
    public string Storage { get; set; }

    [JsonPropertyName("fleecing")]
    public string Fleecing { get; set; }

    [JsonPropertyName("all")]
    public int All { get; set; }


}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BackupMode
{
    [JsonStringEnumMemberName("snapshot")]
    Snapshot
}