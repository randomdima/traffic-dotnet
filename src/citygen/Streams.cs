namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>Each stage's own stream of a map's seed</b> (GEN-11): retuning what one stage does cannot move what another laid,
/// which is what makes a stage worth changing at all — and it is why a town is the same every time it is opened without
/// any stage having to know about the others' draws.
/// </summary>
internal static class Streams
{
    /// <summary>A brief's sea and rivers (<see cref="Gen.TerrainStage"/>).</summary>
    public const ulong Terrain = 0x7465_7272_6169_6E00;

    /// <summary>A brief's districts: where the land is cut, and what each district is like (<see cref="Gen.Partition"/>).</summary>
    public const ulong District = 0x6469_7374_7269_6374;

    /// <summary>A brief's core, railways and intercity roads (<see cref="Gen.Connectors"/>).</summary>
    public const ulong Connectors = 0x636F_6E6E_6563_7473;

    public const ulong CarPark = 0x6361_7270_6172_6B00;

    /// <summary>Which junctions of a wheel are lit (<see cref="Gen.LitJunctions"/>).</summary>
    public const ulong Signal = 0x7369_676E_616C_7300;

    /// <summary>The buildings, each ring of the walk keyed on its own number as well.</summary>
    public const ulong Building = 0x6275_696C_6469_6E67;

    /// <summary>The buildings behind the frontage, each zone keyed on its own number as well.</summary>
    public const ulong Behind = 0x6265_6869_6E64_0000;

    public const ulong Prop = 0x7072_6F70_7300_0000;

    public const ulong Spawn = 0x7370_6177_6E73_0000;

    /// <summary>
    /// <b>One thing's own stream under a stage's</b> — a ring's, a zone's, a district's — the key mixed before it is
    /// folded in.
    /// </summary>
    /// <remarks>
    /// <b>Never a key folded in bare</b>: PCG32's first draw is read off the high bits of the seed and the stream
    /// together (<see cref="Core.Simulation.Rng"/>), so streams a small number apart begin on the same draw, and a
    /// stage that keys every district on its number lays every district's first figure alike.
    /// </remarks>
    public static ulong Keyed(ulong stream, ulong key)
    {
        var mixed = key + 0x9E37_79B9_7F4A_7C15ul;
        mixed = (mixed ^ (mixed >> 30)) * 0xBF58_476D_1CE4_E5B9ul;
        mixed = (mixed ^ (mixed >> 27)) * 0x94D0_49BB_1331_11EBul;
        return stream ^ mixed ^ (mixed >> 31);
    }
}
