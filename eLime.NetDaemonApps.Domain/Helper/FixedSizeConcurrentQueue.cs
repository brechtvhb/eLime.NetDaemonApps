using System.Collections.Concurrent;

namespace eLime.NetDaemonApps.Domain.Helper;

public class FixedSizeConcurrentQueue<T> : ConcurrentQueue<T>
{
    public int MaxSize { get; }

    public FixedSizeConcurrentQueue(int maxSize)
    {
        MaxSize = maxSize;
    }

    public new void Enqueue(T obj)
    {
        base.Enqueue(obj);

        while (Count > MaxSize)
        {
            TryDequeue(out var outObj);
        }
    }
}