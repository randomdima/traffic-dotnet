using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Traced;

/// <summary>
/// <b>Cars stood at a traced town's bridges over its roads</b> (GEN-57, PHY-1a): at each of the widest crossings
/// (<see cref="CityGenFigures.TracedBridgesWithCars"/>), a car each way on the bridge right over the road it
/// crosses, and one each way on that road a deck and a car short of it — so the level above has bodies on it and
/// under it to be looked at by.
/// </summary>
/// <remarks>
/// <para>
/// <b>Laid by rule and not drawn</b>: which crossings is the widths of the two roads, the narrower first ranked, and
/// where on them is the crossing itself — so the same survey stands the same cars every time it is opened.
/// </para>
/// <para>
/// <b>Stood and nothing more</b>, as every car a map puts down is (GEN-7): what drives them is the rule a map with
/// nowhere to park on drives its own (CAR-8), and each is on its lane's channel from its first step (PHY-1a).
/// </para>
/// </remarks>
internal static class TracedBridgeCars
{
    /// <summary>A car's own kind among a plan's spawns.</summary>
    const byte SpawnKindCar = 1;

    /// <summary>One road on the level above crossing one on the ground, and where along each.</summary>
    readonly record struct Crossing(int Over, float OverM, int Under, float UnderM, float WidthM);

    public static CityPlan.SpawnArrays Lay(CityPlan.RoadArrays roads, SimConfig config)
    {
        var crossings = Crossings(roads, config);
        crossings.Sort(static (one, other) =>
            one.WidthM != other.WidthM ? other.WidthM.CompareTo(one.WidthM)
            : one.Over != other.Over ? one.Over.CompareTo(other.Over)
            : one.OverM.CompareTo(other.OverM));

        var kind = new List<byte>();
        var positionM = new List<Vector2>();
        var headingRad = new List<float>();
        var standing = new HashSet<int>();
        var shortOfM = config.Car.LengthM;
        foreach (var crossing in crossings)
        {
            if (standing.Count >= config.CityGen.TracedBridgesWithCars) break;
            if (!standing.Add(crossing.Over)) continue;

            StandBothWays(crossing.Over, crossing.OverM, 0f);
            StandBothWays(crossing.Under, crossing.UnderM, (roads.WidthM[crossing.Over] * 0.5f) + config.WalkOuterM + shortOfM);
        }

        return new CityPlan.SpawnArrays { Kind = [.. kind], PositionM = [.. positionM], HeadingRad = [.. headingRad] };

        // The kerb lane each way, <paramref name="backM"/> short of the place in the way it is driven.
        void StandBothWays(int road, float atM, float backM)
        {
            var arcs = roads.SegmentsOf(road);
            var lengthM = Spline.TotalLengthM(arcs);
            if (roads.LanesWithTheRoad(road) > 0) Stand(arcs, atM - backM, lengthM, roads.LaneOffsetM(road, 0, withTheRoad: true), forward: true);
            if (roads.LanesAgainstTheRoad(road) > 0) Stand(arcs, atM + backM, lengthM, roads.LaneOffsetM(road, 0, withTheRoad: false), forward: false);
        }

        void Stand(ReadOnlySpan<ArcSeg> arcs, float atM, float lengthM, float offsetM, bool forward)
        {
            if (atM < shortOfM || atM > lengthM - shortOfM) return;

            var on = Spline.SampleAt(arcs, atM);
            var side = forward ? config.RoadSideSign : -config.RoadSideSign;
            kind.Add(SpawnKindCar);
            positionM.Add(on.PositionM + (on.Right * offsetM * side));
            headingRad.Add(forward ? on.HeadingRad : Spline.WrapRad(on.HeadingRad + MathF.PI));
        }
    }

    /// <summary>
    /// Every place a road above the ground crosses one on it, a car's length clear of both roads' ends — where a
    /// bridge leaves its bridgehead it meets the road it lands on, and that is no crossing.
    /// </summary>
    static List<Crossing> Crossings(CityPlan.RoadArrays roads, SimConfig config)
    {
        var crossings = new List<Crossing>();
        if (!Array.Exists(roads.Level, static level => level != CityPlan.RoadArrays.Ground)) return crossings;

        var building = new ChainIndex.Builder();
        var widestM = 0f;
        for (var road = 0; road < roads.Count; road++)
        {
            if (roads.LevelOf(road) != CityPlan.RoadArrays.Ground) continue;

            building.Add(road, roads.SegmentsOf(road), Spline.TotalLengthM(roads.SegmentsOf(road)));
            widestM = MathF.Max(widestM, roads.WidthM[road]);
        }

        var index = building.Seal(config.Grid.Main);
        var scan = index.NewScan();
        var near = new int[index.ChainCount];
        var alongM = new float[index.ChainCount];
        Span<SplineCrossing> found = stackalloc SplineCrossing[2];
        var clearM = config.Car.LengthM;
        var asked = new HashSet<int>();

        for (var over = 0; over < roads.Count; over++)
        {
            if (roads.LevelOf(over) == CityPlan.RoadArrays.Ground) continue;

            var arcs = roads.SegmentsOf(over);
            var lengthM = Spline.TotalLengthM(arcs);
            asked.Clear();
            for (var atM = 0f; atM <= lengthM; atM += widestM)
            {
                var count = Math.Min(index.Near(scan, Spline.SampleAt(arcs, atM).PositionM, widestM, near, alongM), near.Length);
                for (var slot = 0; slot < count; slot++)
                {
                    var under = near[slot];
                    if (!asked.Add(under) || Meets(roads, over, under)) continue;

                    var underArcs = roads.SegmentsOf(under);
                    var underM = Spline.TotalLengthM(underArcs);
                    var crossed = Spline.CrossingsM(arcs, underArcs, lengthM * 0.5f, underM * 0.5f, found);
                    for (var at = 0; at < crossed; at++)
                    {
                        if (found[at].OneM < clearM || found[at].OneM > lengthM - clearM) continue;
                        if (found[at].OtherM < clearM || found[at].OtherM > underM - clearM) continue;

                        crossings.Add(new Crossing(
                            over, found[at].OneM, under, found[at].OtherM, MathF.Min(roads.WidthM[over], roads.WidthM[under])));
                    }
                }
            }
        }

        return crossings;
    }

    /// <summary>Whether two roads share a junction — a bridge and the road it lands on.</summary>
    static bool Meets(CityPlan.RoadArrays roads, int one, int other) =>
        roads.FromJunction[one] == roads.FromJunction[other] || roads.FromJunction[one] == roads.ToJunction[other]
        || roads.ToJunction[one] == roads.FromJunction[other] || roads.ToJunction[one] == roads.ToJunction[other];
}
