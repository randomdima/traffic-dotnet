using System.Numerics;
using TrafficSimulation.App.Screen;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Debug;

/// <summary>
/// <b>What the map is zoned for</b> (OBS-2z): the ground in view washed by the kind of the zone each place stands in, every
/// zone's outline over it in its kind's hue, and under the pointer the zone a place stands in and everything it is laid
/// by (<see cref="ZoneTree"/>).
/// </summary>
internal sealed partial class DebugOverlay
{
    /// <summary>
    /// How many squares the wash is laid in across the view's longer side: a city's land uses read at a city's framing, a
    /// block's at a street's, and never more than a few thousand quads.
    /// </summary>
    const int ZoneWashAcross = 96;

    /// <summary>A zone's outline, and the least it is held to on screen.</summary>
    const float ZoneLineM = 0.8f;

    const float ZoneLineFloorPx = 1.5f;

    /// <summary>The kinds' and the looks' names, read once rather than spelled out of an enum every frame.</summary>
    static readonly string[] ZoneKindNames = Enum.GetNames<ZoneKind>();

    static readonly string[] LookNames = Enum.GetNames<BuildingLook>();

    /// <summary>The town's zones as a tree to be asked, laid when a town is first asked about and kept while it is the town.</summary>
    ZoneTree? _zoneTree;

    CityPlan? _zonedPlan;

    /// <summary>The town's zones, or null for a map laid in code, which has none.</summary>
    ZoneTree? ZonesOf(TownWorld world, SimConfig config)
    {
        if (world.Plan.Zones.Count == 0) return null;
        if (ReferenceEquals(_zonedPlan, world.Plan)) return _zoneTree;

        (_zoneTree, _zonedPlan) = (new ZoneTree(world.Plan.Zones, config.Zones), world.Plan);
        return _zoneTree;
    }

    /// <summary>
    /// <b>The wash, then the outlines</b>: a square of the view at a time, washed in the hue of the zone its middle stands
    /// in — the whole map's own ground left clear — and every zone whose box meets the view outlined, edge by edge.
    /// </summary>
    void Zones(ref ScreenDraw draw, TownWorld world, SimConfig config, Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre)
    {
        if (ZonesOf(world, config) is not { } zones) return;

        var cellM = MathF.Max(viewSpanM.X, viewSpanM.Y) / ZoneWashAcross;
        var leastM = viewCentreM - (viewSpanM * 0.5f);
        var (across, down) = ((int)MathF.Ceiling(viewSpanM.X / cellM), (int)MathF.Ceiling(viewSpanM.Y / cellM));
        for (var y = 0; y < down; y++)
        {
            for (var x = 0; x < across; x++)
            {
                var middleM = leastM + new Vector2((x + 0.5f) * cellM, (y + 0.5f) * cellM);
                var wash = Theme.ZoneWash((int)zones[zones.At(middleM)].Kind);
                if (wash.W <= 0f) continue;

                draw.BandM(middleM - new Vector2(cellM * 0.5f, 0f), middleM + new Vector2(cellM * 0.5f, 0f), 0f, cellM, wash);
                if (draw.Full) return;
            }
        }

        var lineM = MathF.Max(ZoneLineM, ZoneLineFloorPx / pixelsPerMetre);
        var plan = zones.Zones;
        for (var zone = TownMap.ZoneArrays.Root + 1; zone < plan.Count; zone++)
        {
            var (boxLeastM, boxMostM) = zones.BoxOf(zone);
            if (boxMostM.X < leastM.X || boxMostM.Y < leastM.Y || boxLeastM.X > leastM.X + viewSpanM.X || boxLeastM.Y > leastM.Y + viewSpanM.Y) continue;

            Outline(ref draw, plan, zone, lineM, Theme.ZoneEdge((int)plan.Kind[zone]));
            if (draw.Full) return;
        }
    }

    /// <summary>Every ring of a zone, edge by edge.</summary>
    static void Outline(ref ScreenDraw draw, TownMap.ZoneArrays zones, int zone, float lineM, Vector4 colour)
    {
        for (var ring = zones.RingOffsets[zone]; ring < zones.RingOffsets[zone + 1]; ring++)
        {
            var points = zones.RingOf(ring);
            for (int at = 0, before = points.Length - 1; at < points.Length; before = at++) draw.LineM(points[before], points[at], lineM, colour);
        }
    }

    /// <summary>The zone a place stands in, picked out: its outline at the picked weight.</summary>
    void FocusZone(ref ScreenDraw draw, in Focus focus, int zone)
    {
        if (ZonesOf(focus.World, focus.Config) is not { } zones || zone >= zones.Count) return;

        Outline(ref draw, zones.Zones, zone, focus.LineM, Theme.DebugPicked);
    }

    /// <summary>
    /// <b>The zone a place stands in, told</b>: its kind and the zones it stands inside, and every setting it is
    /// laid by — how much of its frontage is built and how far back, how turned and how varied, what is built behind it,
    /// the looks it builds at their shares, and how thickly it grows — its own and its kind's alike, as the builders read
    /// them.
    /// </summary>
    void DescribeZone(ref InfoCard card, in Focus focus, int zone)
    {
        if (ZonesOf(focus.World, focus.Config) is not { } zones || zone >= zones.Count) return;

        var plan = zones.Zones;
        var said = zones[zone];
        var line = card.Next();
        line.Add("zone ");
        line.Add(zone);
        line.Add(' ');
        line.Add(ZoneKindNames[(int)said.Kind]);
        card.Keep(in line);

        line = card.Next();
        line.Add("in ");
        for (var parent = plan.Parent[zone]; parent >= 0; parent = plan.Parent[parent])
        {
            line.Add(ZoneKindNames[(int)plan.Kind[parent]]);
            if (plan.Parent[parent] >= 0) line.Add(" < ");
        }

        if (plan.Parent[zone] < 0) line.Add("nothing: the whole map");
        card.Keep(in line);

        line = card.Next();
        line.Add(zones.AreaM2Of(zone) / 1e4f, "F1");
        line.Add(" ha, ");
        line.Add(plan.ParamOffsets[zone + 1] - plan.ParamOffsets[zone]);
        line.Add(" settings of its own");
        card.Keep(in line);

        line = card.Next();
        line.Add("frontage ");
        line.Add(said.Frontage * 100f, "F0");
        line.Add("%, front ");
        line.Add(said.FrontM, "F1");
        line.Add(" m +-");
        line.Add(said.FrontSpreadM, "F1");
        line.Add(", skew ");
        line.Add(said.SkewDeg, "F1");
        line.Add(" deg");
        card.Keep(in line);

        line = card.Next();
        line.Add("variety ");
        line.Add(said.Variety * 100f, "F0");
        line.Add("%, footprint ");
        AddFootprint(ref line, said.FootprintM2);
        card.Keep(in line);

        line = card.Next();
        line.Add("behind ");
        line.Add(said.Interior * 100f, "F1");
        line.Add("% built, footprint ");
        AddFootprint(ref line, said.BehindM2);
        line.Add(", bearing ");
        if (said.BearingDeg is { } bearingDeg) line.Add(bearingDeg, "F0");
        else line.Add("the street's");
        card.Keep(in line);

        line = card.Next();
        line.Add("grows ");
        line.Add(said.Growth * 100f, "F0");
        line.Add("% wild");
        card.Keep(in line);

        line = card.Next();
        line.Add("builds ");
        var any = false;
        for (var look = 0; look < said.LookShares.Length; look++)
        {
            if (said.LookShares[look] <= 0f) continue;

            if (any) line.Add(", ");
            line.Add(LookNames[look]);
            line.Add(' ');
            line.Add(said.LookShares[look] * 100f, "F0");
            line.Add('%');
            any = true;
        }

        if (!any) line.Add("nothing");
        card.Keep(in line);
    }

    /// <summary>A footprint a zone leans its prefabs to, or <c>any</c> where it names none and every prefab of a look is
    /// drawn alike.</summary>
    static void AddFootprint(ref TextBuffer into, float footprintM2)
    {
        if (footprintM2 <= 0f)
        {
            into.Add("any");
            return;
        }

        into.Add(footprintM2, "F0");
        into.Add(" m2");
    }
}
