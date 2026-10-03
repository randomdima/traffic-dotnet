using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Traced;

/// <summary>
/// <b>A survey's sea as the four rings a plan draws its water from</b> (GEN-2c, GEN-57): the water's own
/// edge, and that edge moved a line, a shore less a line and a shore onto the land.
/// </summary>
/// <remarks>
/// <para>
/// <b>Moved as an area and not as a line</b> (<see cref="ArcOutset"/>). A generated coast is one wave and its
/// shore is the wave laid wider; a surveyed one has piers, moles and harbour basins narrower than the shore,
/// and a line moved off each of them separately crosses itself wherever two of them face. Moved as the shape
/// of the sea, a pier thinner than its shore is shore all through and the rings nest by construction.
/// </para>
/// <para>
/// <b>Read at the bank's own tolerance</b> (<see cref="CityGenFigures.ShoreChordToleranceM"/>): a survey draws
/// a straight beach through as many points as a cove, and the corners nearer the straight past them than a
/// chord may stand off it are the straight.
/// </para>
/// </remarks>
internal static class TracedSea
{
    public static CityPlan.WaterArrays Lay(Survey survey, SimConfig config)
    {
        if (survey.Sea.Length == 0) return CityPlan.WaterArrays.None;

        var extentM = new Vector2(survey.WidthM, survey.HeightM);
        var toleranceM = config.CityGen.ShoreChordToleranceM;

        var surveyed = new Vector2[survey.Sea.Length][];
        for (var ring = 0; ring < surveyed.Length; ring++) surveyed[ring] = SeaOnTheRight(survey.Sea[ring]);

        var edge = ShellFill.Outline(surveyed, toleranceM);
        var sea = new ArcSeg[edge.Length][];
        for (var ring = 0; ring < edge.Length; ring++) sea[ring] = Straights(edge[ring]);

        var shoreM = config.CityGen.ShoreWidthM;
        var lineM = config.CityGen.ShoreEdgeWidthM;
        return new CityPlan.WaterArrays
        {
            Outline = Cut(edge, extentM),
            WaterEdge = Moved(sea, lineM, config, extentM),
            ShoreEdge = Moved(sea, shoreM - lineM, config, extentM),
            Shore = Moved(sea, shoreM, config, extentM),
        };
    }

    /// <summary>
    /// One ring of the survey's, walked with the sea on its right — the hand <see cref="ArcOutset"/> keeps a
    /// shape's ground on, and a positive area with y running south.
    /// </summary>
    static Vector2[] SeaOnTheRight(float[] flat)
    {
        var ring = new Vector2[flat.Length / 2];
        for (var point = 0; point < ring.Length; point++) ring[point] = new Vector2(flat[2 * point], flat[(2 * point) + 1]);

        if (TwiceTheAreaM2(ring) < 0.0) Array.Reverse(ring);
        return ring;
    }

    static double TwiceTheAreaM2(ReadOnlySpan<Vector2> ring)
    {
        var area = 0.0;
        for (var point = 0; point < ring.Length; point++)
        {
            var atM = ring[point];
            var nextM = ring[(point + 1) % ring.Length];
            area += ((double)atM.X * nextM.Y) - ((double)nextM.X * atM.Y);
        }

        return area;
    }

    static ArcSeg[] Straights(ReadOnlySpan<Vector2> ring)
    {
        var pieces = new List<ArcSeg>(ring.Length);
        for (var point = 0; point < ring.Length; point++)
        {
            var atM = ring[point];
            var runM = ring[(point + 1) % ring.Length] - atM;
            if (runM.LengthSquared() > 0f) pieces.Add(new ArcSeg(atM, MathF.Atan2(runM.Y, runM.X), runM.Length(), 0f));
        }

        return [.. pieces];
    }

    /// <summary>
    /// <b>The sea grown onto the land by a distance</b>, read back at the bank's tolerance and cut to the map
    /// (GEN-2b). A ring the move turned the other way round is a pocket of land the sea closed round, which the
    /// water carries no hole for, so it is the shore the move made of it and is not drawn as water.
    /// </summary>
    static CityPlan.RingArrays Moved(ArcSeg[][] sea, float landwardM, SimConfig config, Vector2 extentM)
    {
        var (moved, _) = ArcOutset.Of(sea, landwardM, 0f, config.Grid);
        var rings = ShellFill.Outline(moved, config.CityGen.ShoreChordToleranceM);

        var kept = new List<Vector2[]>(rings.Length);
        foreach (var ring in rings)
        {
            if (TwiceTheAreaM2(ring) > 0.0) kept.Add(ring);
        }

        return Cut([.. kept], extentM);
    }

    static CityPlan.RingArrays Cut(Vector2[][] rings, Vector2 extentM)
    {
        var offsets = new List<int> { 0 };
        var pointsM = new List<Vector2>();
        foreach (var ring in rings)
        {
            var cut = WaterOutline.CutToTheMap(ring, extentM);
            if (cut.Length < 3) continue;

            pointsM.AddRange(cut);
            offsets.Add(pointsM.Count);
        }

        return new CityPlan.RingArrays { Offsets = [.. offsets], PointM = [.. pointsM] };
    }
}
