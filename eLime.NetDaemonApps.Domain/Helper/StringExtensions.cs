using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace eLime.NetDaemonApps.Domain.Helper;

internal static class StringExtensions
{
    public static String MakeHaFriendly(this String slug, Int32 length = 80)
    {
        if (String.IsNullOrEmpty(slug)) return "";

        slug = slug.ToLowerInvariant();
        // remove entities
        slug = Regex.Replace(slug, @"&\w+;", "");
        //remove accents
        slug = slug.RemoveAccents();
        // remove any leading or trailing spaces left over
        slug = slug.Trim();
        // remove anything that is not letters, numbers, underscore, or space
        slug = Regex.Replace(slug, @"[^A-Za-z0-9_\s]", "");
        // replace spaces with single dash
        slug = Regex.Replace(slug, @"\s+", "_");
        // if we end up with multiple underscores, collapse to single underscores
        slug = Regex.Replace(slug, @"_{2,}", "_");

        // if it's too long, clip it
        if (slug.Length > length)
            slug = slug.Substring(0, length - 1);
        // remove trailing dash, if there is one
        if (slug.EndsWith("-"))
            slug = slug.Substring(0, slug.Length - 1);

        return slug;
    }

    public static string RemoveAccents(this string txt)
    {
        var normalizedString = txt.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }
}