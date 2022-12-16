namespace eLime.NetDaemonApps.Domain.Helper;

public class DebounceDispatcher : DebounceDispatcher<bool>
{
    public DebounceDispatcher(TimeSpan interval) : base(interval)
    {
    }

    public Task DebounceAsync(Func<Task> action)
    {
        return base.DebounceAsync(async () =>
        {
            await action.Invoke();
            return true;
        });
    }

    public void Debounce(Action action)
    {
        Func<Task<bool>> actionAsync = () => Task.Run(() =>
        {
            action.Invoke();
            return true;
        });

        DebounceAsync(actionAsync);
    }
}

public class DebounceDispatcher<T>
{
    private DateTime lastInvokeTime;
    private readonly TimeSpan interval;
    private Func<Task<T>> functToInvoke;
    private object locker = new object();
    private bool busy;
    private Task<T> waitingTask;

    public DebounceDispatcher(TimeSpan interval)
    {
        this.interval = interval;
    }

    public Task<T> DebounceAsync(Func<Task<T>> functToInvoke)
    {
        lock (locker)
        {
            this.functToInvoke = functToInvoke;
            this.lastInvokeTime = DateTime.UtcNow;
            if (busy)
            {
                return waitingTask;
            }

            busy = true;
            waitingTask = Task.Run(() =>
            {
                do
                {

                    var delay = interval - (DateTime.UtcNow - lastInvokeTime);
                    if (delay > TimeSpan.Zero)
                        Task.Delay(delay).Wait();

                } while ((DateTime.UtcNow - lastInvokeTime) < interval);

                T res;
                try
                {
                    res = this.functToInvoke.Invoke().Result;
                }
                finally
                {
                    lock (locker)
                    {
                        busy = false;
                    }
                }

                return res;
            });
            return waitingTask;
        }
    }
}