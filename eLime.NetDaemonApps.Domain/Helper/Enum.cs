namespace eLime.NetDaemonApps.Domain.Helper;

public static class Enum<T> where T : Enum
{
    public static T Cast(string value)
    {
        return (T)Enum.Parse(typeof(T), value, true);
    }

    public static T Cast<U>(U value) where U : System.Enum
    {
        return (T)Enum.Parse(typeof(T), value.ToString(), true);
    }

    public static T Cast(String value, T defaultValue)
    {
        try
        {
            return (T)Enum.Parse(typeof(T), value, true);
        }
        catch (Exception)
        {
            return defaultValue;
        }
    }

    public static T Cast<U>(U value, T defaultValue) where U : System.Enum
    {
        try
        {
            return (T)Enum.Parse(typeof(T), value.ToString(), true);
        }
        catch (Exception)
        {
            return defaultValue;
        }
    }

    public static T Cast(int value)
    {
        return (T)Enum.ToObject(typeof(T), value);
    }

    public static int GetInt32(string value)
    {
        var enumke = (T)Enum.Parse(typeof(T), value, true);
        return Convert.ToInt32(enumke);
    }

    public static int GetInt32(T value)
    {
        return Convert.ToInt32(value);
    }

    public static T ListToFlag(IEnumerable<T> list)
    {
        return ListToFlag(list, Cast(0));
    }

    public static T ListToFlag(IEnumerable<T> list, T startValue)
    {
        //HOLY SHIT!!!
        return list.Aggregate(startValue, (current, pointer) => Cast(((IConvertible)current).ToInt32(null) | ((IConvertible)pointer).ToInt32(null)));
    }

    public static int ListToFlagInt(IEnumerable<T> list)
    {
        if (list == null || !list.Any())
        {
            return 0;
        }

        T flag = ListToFlag(list);
        return GetInt32(flag);
    }

    public static bool IsInflag(T flag, T itemToCheck, Boolean addDefaultValue = true)
    {
        var state = (((IConvertible)flag).ToInt32(null) & ((IConvertible)itemToCheck).ToInt32(null)) == ((IConvertible)itemToCheck).ToInt32(null);

        if (((IConvertible)itemToCheck).ToInt32(null) != 0)
            return state;

        return addDefaultValue && state;
    }

    public static List<T> AllValues()
    {
        //all values
        return Enum.GetValues(typeof(T))
            .Cast<T>()
            .ToList();
    }
    public static List<String> AllValuesAsStringList()
    {
        //all values
        return Enum.GetValues(typeof(T))
            .Cast<T>()
            .Select(x => x.ToString())
            .ToList();
    }

    public static List<T> FlagToList(T flag, Boolean addDefaultValue = true)
    {
        return AllValues()
            .Where(val => IsInflag(flag, val, addDefaultValue))
            .ToList();
    }

    public static List<T> FlagToList(int flag)
    {
        T flagAsEnum = Cast(flag);
        return FlagToList(flagAsEnum);
    }

    public static T StringListToFlag(List<string> list)
    {
        return ListToFlag(list.Select(Cast));
    }

    public static T StringToFlag(string value)
    {
        var splittedArray = value.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        return StringListToFlag(splittedArray);
    }

    public static List<T> StringToList(String value)
    {
        var splittedArray = (value ?? "").Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        return splittedArray
            .Select(Cast)
            .ToList();
    }

    public static List<T> CastEnumList<U>(IEnumerable<U> value) where U : System.Enum
    {
        return value
            .Select(Cast)
            .ToList();
    }

    public static List<T> StringListToList(IEnumerable<String> value)
    {
        return value
            .Select(Cast)
            .ToList();
    }


    public static String ListToString(IEnumerable<T> list)
    {
        var str = String.Join(", ", list);
        return str;
    }

    public static List<String> ListToStringList(IEnumerable<T> list)
    {
        return list.Select(x => x.ToString()).ToList();
    }
}