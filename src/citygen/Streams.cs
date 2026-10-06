namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>Each stage's own stream of a map's seed</b> (GEN-11): retuning what one stage does cannot move what another laid,
/// which is what makes a stage worth changing at all — and it is why a town is the same every time it is opened without
/// any stage having to know about the others' draws.
/// </summary>
internal static class Streams
{
    /// <summary>A brief's water, drawn when the map is authored (<see cref="Gen.TownAuthor"/>).</summary>
    public const ulong Terrain = 0x7465_7272_6169_6E00;

    /// <summary>A brief's wheel and districts, drawn when the map is authored.</summary>
    public const ulong District = 0x6469_7374_7269_6374;

    public const ulong CarPark = 0x6361_7270_6172_6B00;

    /// <summary>Which junctions of a wheel are lit (<see cref="Gen.LitJunctions"/>).</summary>
    public const ulong Signal = 0x7369_676E_616C_7300;

    /// <summary>The buildings, each ring of the walk keyed on its own number as well.</summary>
    public const ulong Building = 0x6275_696C_6469_6E67;

    /// <summary>The buildings behind the frontage, each zone keyed on its own number as well.</summary>
    public const ulong Behind = 0x6265_6869_6E64_0000;

    public const ulong Prop = 0x7072_6F70_7300_0000;

    public const ulong Spawn = 0x7370_6177_6E73_0000;
}
