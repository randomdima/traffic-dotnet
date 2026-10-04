using System.Text;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>Names as two sources are compared by</b>: plain, so punctuation, case and the apostrophe's several spellings say
/// nothing; and a street's with its kind written out and its words in order, since an address and a road write
/// <c>вул. Садова</c> and <c>Садова вулиця</c> for one street.
/// </summary>
internal static class Names
{
    /// <summary>The words a street's kind is written in, and their short forms.</summary>
    static readonly Dictionary<string, string> Kinds = new(StringComparer.Ordinal)
    {
        ["вул"] = "вулиця", ["вулиця"] = "вулиця", ["пров"] = "провулок", ["провулок"] = "провулок", ["пл"] = "площа", ["площа"] = "площа",
        ["просп"] = "проспект", ["проспект"] = "проспект", ["бул"] = "бульвар", ["бульв"] = "бульвар", ["бульвар"] = "бульвар",
        ["узв"] = "узвіз", ["узвіз"] = "узвіз", ["туп"] = "тупик", ["тупик"] = "тупик", ["шосе"] = "шосе", ["дорога"] = "дорога",
        ["наб"] = "набережна", ["набережна"] = "набережна", ["алея"] = "алея", ["сквер"] = "сквер", ["майдан"] = "майдан", ["проїзд"] = "проїзд",
    };

    /// <summary>A name lower case, letters and digits only, words single-spaced.</summary>
    public static string Plain(string name)
    {
        var plain = new StringBuilder(name.Length);
        foreach (var c in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) plain.Append(c);
            else if (plain.Length > 0 && plain[^1] != ' ') plain.Append(' ');
        }

        return plain.ToString().Trim();
    }

    /// <summary>Whether two names are one place's: one holding the other once each is plain.</summary>
    public static bool Same(string? a, string? b)
    {
        if (a is null || b is null) return false;

        var (x, y) = (Plain(a), Plain(b));
        return x.Length > 0 && y.Length > 0 && (x.Contains(y, StringComparison.Ordinal) || y.Contains(x, StringComparison.Ordinal));
    }

    /// <summary>A street's name plain, its kind written out and its words in order.</summary>
    public static string Street(string name) =>
        string.Join(' ', Plain(name).Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(word => Kinds.GetValueOrDefault(word, word)).Order(StringComparer.Ordinal));

    /// <summary>
    /// Every street name a set of elements carries, as <see cref="Street"/> compares it, and every name one of them has
    /// since dropped (<c>old_name</c>, dated or not) with the name it has now; a name some element still has is no old one.
    /// </summary>
    public static (HashSet<string> Names, Dictionary<string, string> Old) Streets(IEnumerable<Dictionary<string, string>> tagged)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var old = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tags in tagged)
        {
            foreach (var (key, value) in tags)
            {
                if (key is "name" or "name:uk" or "alt_name" or "official_name" or "short_name") names.Add(Street(value));
                else if (key.StartsWith("old_name", StringComparison.Ordinal) && tags.GetValueOrDefault("name") is { } now)
                {
                    foreach (var each in value.Split(';')) old.TryAdd(Street(each), now);
                }
            }
        }

        foreach (var name in names) old.Remove(name);
        return (names, old);
    }
}
