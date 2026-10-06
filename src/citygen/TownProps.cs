using System.Numerics;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>A town's props</b> (GEN-6b, <see cref="PropStage.LayApart"/>): what its map sets down, the furniture, the planting
/// and the trees of its verges, and what grows on the open ground beyond them as thickly as its zones say (GEN-58), drawn
/// off the map's seed so the same map stands the same props every time it is opened — and none on its verges whose crown
/// would reach the ground the traffic drives.
/// </summary>
/// <remarks>
/// <para>
/// <b>What grows beyond the verges is scenery</b> (<see cref="CityPlan.SceneryArrays"/>): drawn, and standing no body.
/// It is a town's yards, parks and waste ground — for a city millions of props, which as bodies would be millions of
/// colliders nothing drives near, the wild pass keeping well clear of the paving.
/// </para>
/// <para>
/// <b>Its buildings are claimed first</b>: a building stands on grass and the ground cannot see it. The claims are kept
/// at <see cref="CityGenFigures.ClaimCellM"/>.
/// </para>
/// <para>
/// <b>Off the driven ground by a lattice step</b> (TER-4c.4): the town's furniture stands on no driven ribbon, and a
/// ribbon is read onto a point within half a lattice diagonal of it, so a crown is kept that much clear of every
/// road's carriageway — and of a junction's disc and the widest arm's width beyond it, which is as far as a movement
/// across it reaches. A wheel's walk holds its props that far off; a fixed road's junction movements can leave its
/// tarmac (<see href="../../docs/index.md">known gaps</see>).
/// </para>
/// </remarks>
internal static class TownProps
{
    /// <summary>How many roads one prop is weighed against at most: more than ever pass within a crown of one place.</summary>
    const int Nearby = 64;

    public static (CityPlan.PropArrays Props, CityPlan.SceneryArrays Scenery) Lay(
        Survey survey, ZoneTree zones, CityPlan.RoadArrays roads, CityPlan.JunctionArrays junctions, Paving paving, GroundShapes ground,
        CityPlan.BuildingArrays buildings, SimConfig config)
    {
        var acrossM = new Vector2(survey.WidthM, survey.HeightM);
        var claims = GenClaims.Over(config.Grid, acrossM, config.CityGen.ClaimCellM);
        for (var building = 0; building < buildings.Count; building++)
        {
            claims.Claim(buildings.CentreM[building], Heading.Unit(buildings.HeadingRad[building]), buildings.SizeM[building] * 0.5f);
        }

        var draw = new Rng(survey.Seed, Streams.Prop);
        var (props, scenery) = PropStage.LayApart(acrossM, paving, ground, claims, survey.Props, atM => zones.SettingsAt(atM).Growth, config, ref draw);
        return (OffTheDrivenGround(props, roads, junctions, config), scenery);
    }

    /// <summary>The props given, less every one whose crown comes within a lattice step of driven ground.</summary>
    internal static CityPlan.PropArrays OffTheDrivenGround(
        CityPlan.PropArrays props, CityPlan.RoadArrays roads, CityPlan.JunctionArrays junctions, SimConfig config)
    {
        var clearM = config.RibbonLatticeStepM;
        var widestCrownM = 0f;
        foreach (var radiusM in props.RadiusM) widestCrownM = MathF.Max(widestCrownM, radiusM);

        var lengthM = new float[roads.Count];
        var widestM = 0f;
        for (var road = 0; road < roads.Count; road++)
        {
            lengthM[road] = Spline.TotalLengthM(roads.SegmentsOf(road));
            widestM = MathF.Max(widestM, roads.WidthM[road]);
        }

        var index = ChainIndex.OfChains(roads.Segments, roads.SegmentOffsets, lengthM, config.Grid.Main);
        var reachOf = Reaches(roads, junctions);
        var discs = Discs(junctions, reachOf, widestCrownM + clearM, out var cellM);

        var ids = new int[Nearby];
        var alongM = new float[Nearby];
        var kept = new List<int>(props.Count);
        for (var prop = 0; prop < props.Count; prop++)
        {
            var (atM, crownM) = (props.CentreM[prop], props.RadiusM[prop]);
            if (!OnARoad(atM, crownM) && !InAJunction(atM, crownM)) kept.Add(prop);
        }

        return new CityPlan.PropArrays
        {
            CentreM = Kept(props.CentreM, kept), RadiusM = Kept(props.RadiusM, kept), BearingRad = Kept(props.BearingRad, kept),
            Kind = Kept(props.Kind, kept),
        };

        bool OnARoad(Vector2 atM, float crownM)
        {
            var near = index.Near(atM, (widestM * 0.5f) + crownM + clearM, ids, alongM);
            for (var at = 0; at < near; at++)
            {
                var road = ids[at];
                var offM = Vector2.Distance(Spline.SampleAt(roads.SegmentsOf(road), alongM[at]).PositionM, atM);
                if (offM < (roads.WidthM[road] * 0.5f) + crownM + clearM) return true;
            }

            return false;
        }

        bool InAJunction(Vector2 atM, float crownM)
        {
            var (column, row) = ((int)MathF.Floor(atM.X / cellM), (int)MathF.Floor(atM.Y / cellM));
            for (var across = column - 1; across <= column + 1; across++)
            {
                for (var down = row - 1; down <= row + 1; down++)
                {
                    if (!discs.TryGetValue((across, down), out var here)) continue;

                    foreach (var junction in here)
                    {
                        if (Vector2.Distance(junctions.CentreM[junction], atM) < reachOf[junction] + crownM + clearM) return true;
                    }
                }
            }

            return false;
        }
    }

    static T[] Kept<T>(T[] all, List<int> kept)
    {
        var into = new T[kept.Count];
        for (var at = 0; at < into.Length; at++) into[at] = all[kept[at]];
        return into;
    }

    /// <summary>
    /// How far a movement across each junction can reach from its centre: its disc, and the widest of its arms beyond
    /// it — a movement leaves from a lane's end at the disc's edge, half that arm's carriageway to one side.
    /// </summary>
    static float[] Reaches(CityPlan.RoadArrays roads, CityPlan.JunctionArrays junctions)
    {
        var reachM = (float[])junctions.RadiusM.Clone();
        var widestM = new float[junctions.Count];
        for (var road = 0; road < roads.Count; road++)
        {
            widestM[roads.FromJunction[road]] = MathF.Max(widestM[roads.FromJunction[road]], roads.WidthM[road]);
            widestM[roads.ToJunction[road]] = MathF.Max(widestM[roads.ToJunction[road]], roads.WidthM[road]);
        }

        for (var junction = 0; junction < reachM.Length; junction++) reachM[junction] += widestM[junction];
        return reachM;
    }

    /// <summary>
    /// Every junction filed under the square cell its centre stands in, a cell as wide as the furthest any reaches and
    /// a crown beyond — so the cells round a prop's own hold every junction it could stand too near.
    /// </summary>
    static Dictionary<(int, int), List<int>> Discs(CityPlan.JunctionArrays junctions, float[] reachM, float beyondM, out float cellM)
    {
        cellM = 1f;
        foreach (var reach in reachM) cellM = MathF.Max(cellM, reach + beyondM);

        var discs = new Dictionary<(int, int), List<int>>();
        for (var junction = 0; junction < junctions.Count; junction++)
        {
            var key = ((int)MathF.Floor(junctions.CentreM[junction].X / cellM), (int)MathF.Floor(junctions.CentreM[junction].Y / cellM));
            if (!discs.TryGetValue(key, out var here)) discs[key] = here = [];
            here.Add(junction);
        }

        return discs;
    }
}
