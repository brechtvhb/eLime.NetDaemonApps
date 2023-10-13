namespace eLime.NetDaemonApps.Domain.Storage;


public interface IFileStorage
{
    void Save<T>(string app, string id, T data) where T : class;
    T? Get<T>(string app, string id) where T : class;
}