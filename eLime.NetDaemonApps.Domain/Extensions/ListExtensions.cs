namespace eLime.NetDaemonApps.Domain.Extensions;

public static class ListExtensions
{
    public static T GetRandomItem<T>(this List<T> list) => list.GetRandomItems(1).Single();

    public static List<T> GetRandomItems<T>(this List<T> list, int count)
    {
        var random = new Random();
        var selectedItems = new List<T>();
        var selectedIndices = new HashSet<int>();

        while (selectedItems.Count < count && selectedIndices.Count < list.Count)
        {
            var randomIndex = random.Next(list.Count);

            if (selectedIndices.Add(randomIndex))
                selectedItems.Add(list[randomIndex]);
        }

        return selectedItems;
    }
}