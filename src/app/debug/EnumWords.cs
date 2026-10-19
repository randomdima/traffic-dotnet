namespace TrafficSimulation.App.Debug;

/// <summary>
/// <b>What a member of an enum is called on a card</b>: its own name, lower-cased, read off the enum once — so a member
/// added to the engine is named the day it lands, where a table of words kept beside the enum ran out at the last member
/// somebody remembered to write down.
/// </summary>
internal static class EnumWords<T> where T : struct, Enum
{
    static readonly string[] Words = Laid();

    /// <summary>The member numbered <paramref name="number"/> — the caller's cast, which boxes nothing.</summary>
    public static string Of(int number) => (uint)number < (uint)Words.Length ? Words[number] : "?";

    static string[] Laid()
    {
        var words = new string[0];
        foreach (var member in Enum.GetValues<T>())
        {
            var at = Convert.ToInt32(member, System.Globalization.CultureInfo.InvariantCulture);
            if (at < 0) continue;

            if (at >= words.Length) Array.Resize(ref words, at + 1);
            words[at] = member.ToString().ToLowerInvariant();
        }

        for (var at = 0; at < words.Length; at++) words[at] ??= "?";
        return words;
    }
}
