namespace eLime.NetDaemonApps.Domain.Storage;


public interface IFileStorage
{
    void Save<T>(string app, string id, T data);
    T? Get<T>(string app, string id) where T : class;
}