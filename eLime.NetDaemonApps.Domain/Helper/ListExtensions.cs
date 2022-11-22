namespace eLime.NetDaemonApps.Domain.Helper;

internal static class ListExtensions
{
    public static int IndexOf<T>(this IReadOnlyList<T> self, Func<T, bool> predicate)
    {
        for (var i = 0; i < self.Count; i++)
        {
            if (predicate(self[i]))
                return i;
        }

        return -1;
    }
}