using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>The first stage: the ground everything else is laid on, and the water cut out of it.</b> What it
/// hands the stages after it is the raster they paint into and the outlines the map carries.
/// </summary>
/// <remarks>
/// <b>The water is decided before a single node is placed</b>, because every later stage asks the ground
/// whether it is free and the answer has to already be true. A river discovered after the streets were laid
/// is a town whose roads have to be taken back, which is the retry this generator does not do.
/// </remarks>
internal static class TerrainStage
{
    /// <summary>The fewest points a shoreline is drawn through, however straight the wave it is drawn from.</summary>
    const int FewestOutlinePoints = 24;

    /// <summary>How far past the map's own edge the far side of an outline is pushed, so no shore closes inside the town.</summary>
    const float OverrunM = 200f;

    /// <summary>
    /// The three waves a bank wanders on: how many times each runs over the shore's length, and how much of
    /// the wander is its own. <b>Three of them, of their own phase and period</b> — a shore drawn from one is
    /// a sine and one drawn from noise per point is a saw.
    /// </summary>
    static readonly Vector3 WavesOver = new(1f, 2.3f, 4.1f);

    static readonly Vector3 WaveShare = new(0.25f, 0.15f, 0.1f);

    /// <summary>
    /// The water a town stands on: what it is, the four rings the map draws it and its shore from, and the
    /// line it runs down.
    /// </summary>
    /// <param name="Rings">
    /// The water's own edge, the outer edge of the shore it is set in, and the two rings that leave a line's
    /// width along each of those edges (GEN-2c). <b>All four are the same wave at four offsets</b>, so the
    /// strip is one width the whole way round and each line runs true along it.
    /// </param>
    /// <param name="CentreM">
    /// The middle of the river, or the shore of the sea — the line the water runs along, which is what a
    /// crossing wants to stand square to (GEN-14b).
    /// </param>
    internal readonly record struct Water(WaterKind Kind, CityPlan.WaterArrays Rings, Vector2[] CentreM)
    {
        public bool Any => Rings.Outline.Count > 0;

        /// <summary>
        /// Whether a road may ever stand over it. <b>A town bridges its river and never its sea</b>
        /// (GEN-14b): a coast has one shore inside the town and whatever is past it is off the map, so a
        /// deck laid over it goes nowhere.
        /// </summary>
        public bool Bridgeable => Kind == WaterKind.River;

        /// <summary>The way the water runs where it passes nearest a point.</summary>
        public Vector2 Along(Vector2 pointM)
        {
            var nearest = Vector2.UnitX;
            var nearestM = float.PositiveInfinity;
            for (var point = 0; point + 1 < CentreM.Length; point++)
            {
                var runM = CentreM[point + 1] - CentreM[point];
                var lengthM = runM.Length();
                if (lengthM <= 0f) continue;

                var alongM = Math.Clamp(Vector2.Dot(pointM - CentreM[point], runM / lengthM), 0f, lengthM);
                var awayM = (CentreM[point] + (runM / lengthM * alongM) - pointM).LengthSquared();
                if (awayM >= nearestM) continue;

                nearestM = awayM;
                nearest = runM / lengthM;
            }

            return nearest;
        }

        /// <summary>And the way across it, which is the shortest line over it.</summary>
        public Vector2 Across(Vector2 pointM) => Heading.RightOf(Along(pointM));
    }

    public static Water Lay(TownBrief brief, SimConfig config, GenRaster raster, ref Rng draw)
    {
        raster.Fill(Ground.Grass);
        if (brief.Water == WaterKind.None) return new Water(brief.Water, CityPlan.WaterArrays.None, []);

        var extentM = new Vector2(brief.WidthM, brief.HeightM);
        var shortSideM = MathF.Min(extentM.X, extentM.Y);
        var bearing = Heading.Unit(brief.WaterBearingDeg * MathF.PI / 180f);
        var across = Heading.RightOf(bearing);
        var widthM = shortSideM * brief.WaterShare;
        var wanderM = widthM * brief.WaterMeander;
        var runM = extentM.Length() + (OverrunM * 2f);
        var centreM = extentM * 0.5f;
        var phase = new Vector3(draw.NextFloat(0f, MathF.Tau), draw.NextFloat(0f, MathF.Tau), draw.NextFloat(0f, MathF.Tau));

        // A river runs through the middle of the town; a coast stands its own width in from one edge, so what
        // is left is the land rather than half a map of sea.
        var strandedM = brief.Water == WaterKind.Coast ? shortSideM * (0.5f - brief.WaterShare) : 0f;

        var points = PointsAlong(runM, wanderM, config.CityGen.ShoreChordToleranceM);
        var middleM = new Vector2[points];
        for (var point = 0; point < points; point++)
        {
            var along = point / (float)(points - 1);
            middleM[point] = centreM + (bearing * ((along - 0.5f) * runM))
                             + (across * ((Wander(along, phase) * wanderM) + strandedM));
        }

        // How far each bank stands off that line. <b>A coast is a river with one bank pushed off the map</b>:
        // the sea has no far shore inside the town, and pushing it out is what keeps the outline one closed
        // ring rather than two kinds of shape.
        var seaM = extentM.Length() + OverrunM;
        var coast = brief.Water == WaterKind.Coast;
        var nearM = coast ? 0f : widthM * 0.5f;
        var farM = coast ? seaM : -widthM * 0.5f;
        var shoreM = config.CityGen.ShoreWidthM;
        var lineM = config.CityGen.ShoreEdgeWidthM;

        // Which way is away from the water on the near bank, so that a shore laid on either side of the line
        // is laid on the land side of it.
        var landward = MathF.Sign(nearM - farM);

        var outlineM = Ring(middleM, across, nearM, farM);
        var bankM = Ring(middleM, across, nearM + (landward * shoreM), farM - (landward * shoreM));
        var bankLineM = Ring(
            middleM, across, nearM + (landward * (shoreM - lineM)), farM - (landward * (shoreM - lineM)));
        var waterLineM = Ring(middleM, across, nearM + (landward * lineM), farM - (landward * lineM));

        // The shore first and the water set into it, so what is left of the wider ring is the strip between
        // the two (GEN-2c). Both are the same wave, so the strip is a shore's width everywhere — and the two
        // rings between them are lines and not ground, so nothing is classified from either.
        raster.FillOutline(bankM, Ground.Sidewalk);
        raster.FillOutline(outlineM, Ground.Water);

        // The raster took what fell on it; what the map carries is the same shapes cut to the map's own edges
        // (GEN-2b), because the rings are what the water and its shore are drawn from.
        return new Water(
            brief.Water,
            new CityPlan.WaterArrays
            {
                Outline = OneRing(outlineM, extentM),
                Shore = OneRing(bankM, extentM),
                ShoreEdge = OneRing(bankLineM, extentM),
                WaterEdge = OneRing(waterLineM, extentM),
            },
            middleM);
    }

    /// <summary>One ring, cut to the map (GEN-2b), as the arrays a plan carries a set of them in.</summary>
    static CityPlan.RingArrays OneRing(ReadOnlySpan<Vector2> ringM, Vector2 extentM)
    {
        var cutM = WaterOutline.CutToTheMap(ringM, extentM);
        return cutM.Length == 0
            ? CityPlan.RingArrays.None
            : new CityPlan.RingArrays { Offsets = [0, cutM.Length], PointM = cutM };
    }

    /// <summary>One closed ring around a line, as the two banks standing at their own offsets across it.</summary>
    static Vector2[] Ring(ReadOnlySpan<Vector2> middleM, Vector2 across, float nearM, float farM)
    {
        var ring = new Vector2[middleM.Length * 2];
        for (var point = 0; point < middleM.Length; point++)
        {
            ring[point] = middleM[point] + (across * nearM);
            ring[^(point + 1)] = middleM[point] + (across * farM);
        }

        return ring;
    }

    /// <summary>
    /// How many points a bank is drawn through: enough that no chord of it stands further off the wave than
    /// the tolerance. <b>Derived from the wave's own curvature</b> — the sharpest a sum of sines bends is the
    /// sum of what each of them bends — so a wild meander is drawn through more points and a straight coast
    /// through few, and neither is a count anybody has to keep true by eye.
    /// </summary>
    static int PointsAlong(float runM, float wanderM, float toleranceM)
    {
        var bendsPerM = 0f;
        for (var wave = 0; wave < 3; wave++)
        {
            var perM = MathF.Tau * At(WavesOver, wave) / runM;
            bendsPerM += perM * perM * At(WaveShare, wave) * wanderM;
        }

        if (bendsPerM <= 0f) return FewestOutlinePoints;

        // A chord of a curve stands off it by about r²/8 of the curve's own bend, which is the sagitta.
        var stepM = MathF.Sqrt(8f * toleranceM / bendsPerM);
        return Math.Max(FewestOutlinePoints, (int)MathF.Ceiling(runM / stepM) + 1);
    }

    static float Wander(float along, Vector3 phase) =>
        (MathF.Sin((along * MathF.Tau * WavesOver.X) + phase.X) * WaveShare.X)
        + (MathF.Sin((along * MathF.Tau * WavesOver.Y) + phase.Y) * WaveShare.Y)
        + (MathF.Sin((along * MathF.Tau * WavesOver.Z) + phase.Z) * WaveShare.Z);

    static float At(Vector3 of, int index) => index switch { 0 => of.X, 1 => of.Y, _ => of.Z };

}
