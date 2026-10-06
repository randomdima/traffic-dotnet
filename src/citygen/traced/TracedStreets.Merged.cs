using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Traced;

internal static partial class TracedStreets
{
    /// <summary>
    /// <b>One road's line as its corners were rounded, and merged into fewer pieces each way its two ends allow</b>
    /// (<see cref="TracedPieces"/>), each laid the first time it is asked for.
    /// </summary>
    sealed class Merging(ArcSeg[] rounded, Vector2[] surveyedM, Vector2 mapM, float leastRadiusM, float toleranceM)
    {
        const int Ends = (int)RoadEnd.Place + 1;

        readonly (ArcSeg[] Line, Vector2[] ThroughM)?[] _merged = new (ArcSeg[], Vector2[])?[Ends * Ends];

        public (ArcSeg[] Line, Vector2[] ThroughM) Unmerged { get; } = (rounded, TracedPieces.Corners(rounded));

        public (ArcSeg[] Line, Vector2[] ThroughM) Merged(RoadEnd atStart, RoadEnd atEnd)
        {
            var at = ((int)atStart * Ends) + (int)atEnd;
            if (_merged[at] is { } merged) return merged;

            var line = rounded;
            for (var round = 0; round < TracedPieces.MergeRounds; round++)
            {
                var fewer = TracedPieces.Fewest(line, surveyedM, mapM, leastRadiusM, toleranceM, atStart, atEnd);
                if (fewer.Length >= line.Length) break;

                line = fewer;
            }

            return (_merged[at] = (line, TracedPieces.Corners(line))).Value;
        }
    }

    /// <summary>How much of its rounded line one road keeps: at each end, or none of it merged at all.</summary>
    record struct Kept(RoadEnd AtStart, RoadEnd AtEnd, bool Unmerged)
    {
        public static Kept Least => new(RoadEnd.Place, RoadEnd.Place, false);

        /// <summary>One more step kept at an end: its pose, its piece, and past that the road unmerged.</summary>
        public Kept More(bool atStart) => (atStart ? AtStart : AtEnd) switch
        {
            RoadEnd.Piece => this with { Unmerged = true },
            var end when atStart => this with { AtStart = end - 1 },
            var end => this with { AtEnd = end - 1 },
        };
    }

    /// <summary>
    /// <b>Every road merged into as few pieces as its junctions lay the same movements with</b> (GEN-57,
    /// <see cref="TracedPieces"/>): each road is merged keeping only where its ends stand, and at a junction whose
    /// movements differ from those it lays with every road unmerged, every road's end there keeps a step more — its
    /// pose, then the piece it ends in, and then the road is not merged — until none differs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Asked of the movements and not of the shapes they are laid off</b>: a junction lays its movements off the
    /// poses its roads meet it on and the bend each arrives in (<see cref="LaneLines"/>), so a road's end moved by a
    /// hair can lay a turn or refuse one, and a bend reshaped at its mouth relabel one. A movement is which lane joins
    /// which and the turn it is; one lost, gained or relabelled is a junction that differs, whatever moved it.
    /// </para>
    /// <para>
    /// <b>It ends, on every junction as it was</b>: a junction's movements are its own roads' ends', so one whose roads
    /// are all unmerged lays them as it did, and every step keeps more of one road.
    /// </para>
    /// </remarks>
    /// <param name="roadOf">The road each laid road was settled as, by the index <paramref name="merging"/> is in.</param>
    static Laid MergedAsFarAsKept(
        Survey survey, SimConfig config, int[] roadOf, Merging[] merging, Func<(ArcSeg[] Line, Vector2[] ThroughM)[], Laid> laidAs)
    {
        var chosen = new (ArcSeg[] Line, Vector2[] ThroughM)[merging.Length];
        foreach (var road in roadOf) chosen[road] = merging[road].Unmerged;
        var unmerged = laidAs(chosen);
        var asSurveyed = Movements(survey, config, unmerged);

        // Every road's own and none of the others', so laid side by side; the steps after ask for few.
        Parallel.ForEach(roadOf, road => merging[road].Merged(Kept.Least.AtStart, Kept.Least.AtEnd));

        var kept = Filled(merging.Length, Kept.Least);
        while (true)
        {
            foreach (var road in roadOf)
            {
                chosen[road] = kept[road].Unmerged ? merging[road].Unmerged : merging[road].Merged(kept[road].AtStart, kept[road].AtEnd);
            }

            var laid = laidAs(chosen);
            var movements = Movements(survey, config, laid);
            var (differs, stepped) = (false, false);
            for (var road = 0; road < roadOf.Length; road++)
            {
                var was = kept[roadOf[road]];
                if (was.Unmerged) continue;

                var now = was;
                if (!Alike(laid.Roads.FromJunction[road])) now = now.More(atStart: true);
                if (!Alike(laid.Roads.ToJunction[road]) && !now.Unmerged) now = now.More(atStart: false);
                (kept[roadOf[road]], stepped) = (now, stepped || now != was);
            }

            for (var junction = 0; junction < movements.Length && !differs; junction++) differs = !Alike(junction);
            if (!differs) return laid;
            if (!stepped) return unmerged;

            bool Alike(int junction) => movements[junction].AsSpan().SequenceEqual(asSurveyed[junction]);
        }
    }

    /// <summary>
    /// Each junction's movements, as the lanes laid off these streets have them: one a movement, sorted — the two lanes
    /// it joins by road, way and place from the kerb, and the turn it is (<see cref="LaneTurn"/>).
    /// </summary>
    static long[][] Movements(Survey survey, SimConfig config, Laid laid)
    {
        var lanes = LaneLines.Of(
            new GroundPieces(
                survey.Seed, new Vector2(survey.WidthM, survey.HeightM), config.PavementWidthM, laid.Roads,
                laid.Bridges, laid.Junctions, NoCorners, laid.Roundabouts, NoLots, CityPlan.PavedAreaArrays.None, NoCrosswalks,
                CityPlan.WaterArrays.None),
            config);

        var at = new List<long>[laid.Junctions.CentreM.Length];
        for (var junction = 0; junction < at.Length; junction++) at[junction] = [];
        for (var connector = 0; connector < lanes.ConnectorCount; connector++)
        {
            var (from, to) = (lanes.ConnectorFromLane[connector], lanes.ConnectorToLane[connector]);
            at[lanes.JunctionOfConnector(connector)].Add(
                ((long)lanes.LaneRoad[from] << 42) | ((long)lanes.LaneRoad[to] << 20) | ((long)lanes.LaneFromKerb[from] << 15)
                | ((long)lanes.LaneFromKerb[to] << 10) | ((lanes.LaneForward[from] ? 1L : 0L) << 9) | ((lanes.LaneForward[to] ? 1L : 0L) << 8)
                | (byte)lanes.ConnectorKind[connector]);
        }

        var movements = new long[at.Length][];
        for (var junction = 0; junction < at.Length; junction++)
        {
            at[junction].Sort();
            movements[junction] = [.. at[junction]];
        }

        return movements;
    }

    static readonly CityPlan.JunctionCornerArrays NoCorners = new() { CornerM = [], ArcCentreM = [], RadiusM = [], TangentAM = [], TangentBM = [] };

    static readonly CityPlan.ParkingLotArrays NoLots = new()
    {
        CentreM = [], Axis = [], HalfExtentM = [], SpaceOffsets = [0], SpacePositionM = [], SpaceHeadingRad = [],
    };

    static readonly CityPlan.CrosswalkArrays NoCrosswalks = new() { CentreM = [], Axis = [], DepthM = [], Road = [], Junction = [] };
}
