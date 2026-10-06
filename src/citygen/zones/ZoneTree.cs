using System.Numerics;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.CityGen.Zones;

/// <summary>
/// <b>Which zone a place stands in, and what that zone is laid by</b> (GEN-58): the deepest zone holding it — a park
/// inside an estate is the park's — two of one depth going to the smaller, and the whole map where no other holds it;
/// and each zone's settings resolved once, its own over its kind's.
/// </summary>
/// <remarks>
/// <para>
/// <b>Filed by the squares each zone's outline box covers</b>, and asked of a square's zones alone, deepest first. A
/// zone's outline is thinned and in whole metres, so a place a metre from its edge may be read as either side of it.
/// </para>
/// <para>Read-only once built, so any number of threads ask it at once.</para>
/// </remarks>
internal sealed class ZoneTree
{
    /// <summary>
    /// How big a square of the map the zones are filed under: a few blocks, so a place asks a handful of zones, and a
    /// zone the size of a city quarter is filed under a few thousand squares and not a million.
    /// </summary>
    const float CellM = 250f;

    readonly float[] _outlineM2;
    readonly float[] _ownM2;
    readonly int[] _depth;
    readonly (Vector2 LeastM, Vector2 MostM)[] _box;
    readonly Dictionary<(int X, int Y), int[]> _cells;
    readonly ZoneSettings[] _settings;

    public ZoneTree(TownMap.ZoneArrays zones, ZoneFigures figures)
    {
        Zones = zones;
        _outlineM2 = new float[zones.Count];
        _ownM2 = new float[zones.Count];
        _depth = new int[zones.Count];
        _box = new (Vector2, Vector2)[zones.Count];
        _settings = new ZoneSettings[zones.Count];
        var filing = new Dictionary<(int X, int Y), List<int>>();
        for (var zone = 0; zone < zones.Count; zone++)
        {
            var (first, past) = (zones.RingOffsets[zone], zones.RingOffsets[zone + 1]);
            var (leastM, mostM) = (new Vector2(float.MaxValue), new Vector2(float.MinValue));
            foreach (var pointM in zones.RingOf(first)) (leastM, mostM) = (Vector2.Min(leastM, pointM), Vector2.Max(mostM, pointM));

            _box[zone] = (leastM, mostM);
            for (var ring = first; ring < past; ring++) _outlineM2[zone] += (ring == first ? 1f : -1f) * MathF.Abs(Area(zones.RingOf(ring)));

            _ownM2[zone] += _outlineM2[zone];
            _depth[zone] = zone == TownMap.ZoneArrays.Root ? 0 : _depth[zones.Parent[zone]] + 1;
            _settings[zone] = Resolved(zones, zone, figures);
            if (zone == TownMap.ZoneArrays.Root) continue;

            _ownM2[zones.Parent[zone]] -= _outlineM2[zone];

            var (least, most) = (Cell(leastM), Cell(mostM));
            for (var x = least.X; x <= most.X; x++)
            {
                for (var y = least.Y; y <= most.Y; y++)
                {
                    if (!filing.TryGetValue((x, y), out var here)) filing[(x, y)] = here = [];
                    here.Add(zone);
                }
            }
        }

        // Deepest first and then smallest, so the first holding a place is the one it stands in.
        _cells = filing.ToDictionary(
            cell => cell.Key,
            cell => cell.Value.OrderByDescending(zone => _depth[zone]).ThenBy(zone => _outlineM2[zone]).ToArray());
    }

    public TownMap.ZoneArrays Zones { get; }

    public int Count => _settings.Length;

    /// <summary>What a zone is laid by.</summary>
    public ZoneSettings this[int zone] => _settings[zone];

    /// <summary>How many zones a zone stands inside.</summary>
    public int DepthOf(int zone) => _depth[zone];

    /// <summary>
    /// The ground a zone holds, in square metres: its outline's less its holes and less the zones inside it, which hold
    /// their own.
    /// </summary>
    public float AreaM2Of(int zone) => MathF.Max(0f, _ownM2[zone]);

    /// <summary>The box a zone's outline stands in.</summary>
    public (Vector2 LeastM, Vector2 MostM) BoxOf(int zone) => _box[zone];

    /// <summary>The zone a place stands in: the deepest holding it, else the whole map.</summary>
    public int At(Vector2 atM)
    {
        if (!_cells.TryGetValue(Cell(atM), out var here)) return TownMap.ZoneArrays.Root;

        foreach (var zone in here)
        {
            if (Holds(zone, atM)) return zone;
        }

        return TownMap.ZoneArrays.Root;
    }

    /// <summary>What the zone a place stands in is laid by.</summary>
    public ZoneSettings SettingsAt(Vector2 atM) => _settings[At(atM)];

    /// <summary>Whether a zone holds a place: inside its outline and inside none of its holes.</summary>
    public bool Holds(int zone, Vector2 atM)
    {
        var (leastM, mostM) = _box[zone];
        if (atM.X < leastM.X || atM.Y < leastM.Y || atM.X > mostM.X || atM.Y > mostM.Y) return false;

        var inside = false;
        for (var ring = Zones.RingOffsets[zone]; ring < Zones.RingOffsets[zone + 1]; ring++) inside ^= Encloses(Zones.RingOf(ring), atM);

        return inside;
    }

    /// <summary><b>A zone's settings resolved</b>: its own, over its kind's. A zone that says any look says its whole mix.</summary>
    static ZoneSettings Resolved(TownMap.ZoneArrays zones, int zone, ZoneFigures figures)
    {
        var kind = zones.Kind[zone];
        var defaults = DefaultsOf(kind, figures);
        var (frontage, frontM, spreadM, skewDeg, variety, interior, growth) =
            (defaults.Frontage, defaults.FrontM, defaults.FrontSpreadM, defaults.SkewDeg, defaults.Variety, defaults.Interior, defaults.Growth);
        float? bearingDeg = null;
        var (footprintM2, behindM2) = (defaults.FootprintM2, defaults.BehindM2);
        var looks = defaults.Looks.InOrder();

        var saidLook = false;
        for (var at = zones.ParamOffsets[zone]; at < zones.ParamOffsets[zone + 1]; at++)
        {
            var value = zones.ParamValue[at];
            switch (zones.ParamKey[at])
            {
                case ZoneParam.Frontage: frontage = value; break;
                case ZoneParam.FrontM: frontM = value; break;
                case ZoneParam.FrontSpreadM: spreadM = value; break;
                case ZoneParam.SkewDeg: skewDeg = value; break;
                case ZoneParam.Variety: variety = value; break;
                case ZoneParam.Interior: interior = value; break;
                case ZoneParam.BearingDeg: bearingDeg = value; break;
                case ZoneParam.Growth: growth = value; break;
                case ZoneParam.FootprintM2: footprintM2 = value; break;
                case ZoneParam.BehindM2: behindM2 = value; break;
                case var param when ZoneParams.LookOf(param) is { } look:
                    if (!saidLook) Array.Clear(looks);
                    saidLook = true;
                    looks[(int)look] = value;
                    break;
            }
        }

        var total = 0f;
        foreach (var share in looks) total += MathF.Max(0f, share);
        for (var look = 0; look < looks.Length; look++) looks[look] = total > 0f ? MathF.Max(0f, looks[look]) / total : 0f;

        return new ZoneSettings(
            kind, Math.Clamp(frontage, 0f, 1f), frontM, MathF.Max(0f, spreadM), MathF.Max(0f, skewDeg), Math.Clamp(variety, 0f, 1f),
            Math.Clamp(interior, 0f, 1f), bearingDeg, Math.Clamp(growth, 0f, 1f), MathF.Max(0f, footprintM2), MathF.Max(0f, behindM2), looks);
    }

    static ZoneDefaults DefaultsOf(ZoneKind kind, ZoneFigures figures) => kind switch
    {
        ZoneKind.Town => figures.Town,
        ZoneKind.Wheel => figures.Wheel,
        ZoneKind.OldTown => figures.OldTown,
        ZoneKind.Residential => figures.Residential,
        ZoneKind.HighRise => figures.HighRise,
        ZoneKind.Suburb => figures.Suburb,
        ZoneKind.Commercial => figures.Commercial,
        ZoneKind.Industrial => figures.Industrial,
        ZoneKind.Civic => figures.Civic,
        ZoneKind.Garages => figures.Garages,
        ZoneKind.Parking => figures.Parking,
        ZoneKind.Park => figures.Park,
        ZoneKind.Wild => figures.Wild,
        ZoneKind.Farmland => figures.Farmland,
        ZoneKind.Open => figures.Open,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "a kind of zone with no figures"),
    };

    static (int X, int Y) Cell(Vector2 atM) => ((int)MathF.Floor(atM.X / CellM), (int)MathF.Floor(atM.Y / CellM));

    static bool Encloses(ReadOnlySpan<Vector2> ring, Vector2 atM)
    {
        var inside = false;
        for (int at = 0, before = ring.Length - 1; at < ring.Length; before = at++)
        {
            var (a, b) = (ring[at], ring[before]);
            if ((a.Y > atM.Y) != (b.Y > atM.Y) && atM.X < a.X + ((atM.Y - a.Y) / (b.Y - a.Y) * (b.X - a.X))) inside = !inside;
        }

        return inside;
    }

    static float Area(ReadOnlySpan<Vector2> ring)
    {
        var twiceM2 = 0f;
        for (int at = 0, before = ring.Length - 1; at < ring.Length; before = at++) twiceM2 += (ring[before].X * ring[at].Y) - (ring[at].X * ring[before].Y);

        return twiceM2 * 0.5f;
    }
}
