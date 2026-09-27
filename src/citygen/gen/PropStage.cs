using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>The street furniture, scattered over what is left</b>: the one thing a town has more of than anything
/// else, and the only stage whose failure to place something costs nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Props are what the ground affords and not a count anybody authored</b> (GEN-6), and they are laid in
/// passes that answer different questions (GEN-6b). <b>The kerbs are walked first</b>, because a verge is a
/// line and not an area: what stands along one is found by following the line the concrete stops at, on that
/// line's own bearing, and not by sweeping a lattice and asking each square whether it happens to be near a
/// street. <b>Then the ground the town is not on is swept</b>, well clear of the walk that pass took.
/// </para>
/// <para>
/// <b>Neither pass searches for anything</b>: a candidate that does not stand is not a prop, and the ground
/// it was refused for is not tried again from another angle. Everything the stage needs was laid before
/// it — the roads and the boundary they leave — which is what makes one pass over each enough.
/// </para>
/// <para>
/// <b>A kind carries its own size band</b>, because a set is only as wide as the art authored for it: the
/// great trees put the wild set's band past the other two, and a wild look drawn at that size on a verge is
/// what a street tree is.
/// </para>
/// </remarks>
internal static class PropStage
{
    public static CityPlan.PropArrays Lay(
        TownBrief brief, Paving paving, GroundShapes ground, GenClaims claims, SimConfig config,
        ref Rng draw)
    {
        var acrossM = new Vector2(brief.WidthM, brief.HeightM);
        var widestM = MathF.Max(config.CityGen.PropDiameterMaxM, config.CityGen.PropWildDiameterMaxM);
        var scatter = PropScatter.Over(acrossM, widestM, config.CityGen.PropApartM);

        AlongTheKerbs(paving.Rings(config), ground, claims, config, scatter, ref draw);
        OverWhatIsLeft(acrossM, ground, claims, config, scatter, ref draw);

        return new CityPlan.PropArrays
        {
            CentreM = [.. scatter.CentreM],
            RadiusM = [.. scatter.RadiusM],
            BearingRad = [.. scatter.BearingRad],
            Kind = [.. scatter.Kind],
        };
    }

    /// <summary>
    /// <b>The first pass: what a town puts along its own kerbs</b> (GEN-6b). The line walked is the walk's
    /// outer face (<see cref="GroundRings.WalkEdge"/>) — the whole town's kerb in one set of closed rings —
    /// and a candidate stands out in the verge beyond it, <b>on that face's own bearing there</b>, so a look
    /// with a front runs with the street rather than with the compass.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The band is measured to the prop's own near rim and not to its centre</b>, which is what puts a
    /// narrow look at the near edge of the verge and pushes a wide one out by its own width (GEN-6b). The
    /// girth is still asked of the ground (GEN-6a), but the ground's answer parts from the drawn walk at a
    /// corner (<see href="../../../docs/index.md">known gaps</see>, <c>GroundShapes.At</c>), so what places a
    /// prop off the walk is the figure the walk was struck at rather than an answer about the point it
    /// stands on.
    /// </para>
    /// <para>
    /// <b>A ring is walked once and the outward hand is the ring's own</b>: a shell is walked with the ground
    /// it covers on its right (<see cref="BandShell.Chains"/>), so the verge is to its left, whether the ring
    /// is the one round the outside of the town or one round a block inside it. Nothing here knows what a
    /// junction, a roundabout or a bay is — the concrete wraps all of them, and the face is continuous over
    /// every one.
    /// </para>
    /// </remarks>
    static void AlongTheKerbs(
        GroundRings rings, GroundShapes ground, GenClaims claims, SimConfig config, PropScatter scatter,
        ref Rng draw)
    {
        var nearM = config.CityGen.PropVergeNearM;
        var bandM = config.CityGen.PropVergeFarM - nearM;
        var pitchM = config.CityGen.PropVergePitchM;

        foreach (var face in rings.WalkEdge)
        {
            var lengthM = Spline.TotalLengthM(face);
            var cursor = default(SplineCursor);
            for (var alongM = 0f; alongM < lengthM; alongM += pitchM)
            {
                var stationM = alongM + (draw.NextFloat() * pitchM);
                if (stationM >= lengthM) continue;

                // Drawn before the ground is asked about anything, so that what the stream has spent by
                // station n is the rings' own length and never what the ground answered at the stations
                // before it.
                var kind = OnAVerge(config, ref draw);
                var reachM = draw.NextFloat(config.CityGen.PropDiameterMinM, WidestM(kind, config)) * 0.5f;
                var outM = reachM + nearM + (draw.NextFloat() * bandM);

                var on = Spline.SampleFrom(face, stationM, ref cursor);
                var atM = on.PositionM - (on.Right * outM);

                // The one point before the girth, because a disc is walked from its rim and most of a verge
                // is somebody's tarmac: reading the ground round a place that has already failed buys
                // nothing.
                if (ground.At(atM) != Ground.Grass) continue;
                if (!Stands(atM, reachM, config.Terrain.GroundStepM, ground, claims, scatter)) continue;

                scatter.Add(atM, reachM, on.HeadingRad, kind);
            }
        }
    }

    /// <summary>
    /// <b>The last pass: what grows where the town is not</b> (GEN-6b). A stratified sweep — one candidate
    /// per cell of a coarse lattice, jittered inside it — so the cost is the ground rather than the count,
    /// and every candidate standing within a stand-off of a walk or a car park is left to the first pass.
    /// <b>Nothing here is laid on a bearing</b>: what the wild set holds has no front to turn.
    /// </summary>
    static void OverWhatIsLeft(
        Vector2 acrossM, GroundShapes ground, GenClaims claims, SimConfig config, PropScatter scatter,
        ref Rng draw)
    {
        var spacingM = config.CityGen.PropSpacingM;
        var standOffM = config.CityGen.PropWildStandOffM;
        var columns = (int)(acrossM.X / spacingM);
        var rows = (int)(acrossM.Y / spacingM);

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var atM = new Vector2(
                    (column + draw.NextFloat()) * spacingM,
                    (row + draw.NextFloat()) * spacingM);

                // The cell the candidate stands on is the cheapest corner of the tests below, and most of a
                // town is not grass: reading the ground around one that has already failed buys nothing.
                if (ground.At(atM) != Ground.Grass) continue;

                if (ground.PavingWithin(atM, standOffM)) continue;

                var reachM = draw.NextFloat(
                    config.CityGen.PropDiameterMinM, config.CityGen.PropWildDiameterMaxM) * 0.5f;
                if (!Stands(atM, reachM, config.Terrain.GroundStepM, ground, claims, scatter)) continue;

                scatter.Add(atM, reachM, 0f, PropKind.WildNature);
            }
        }
    }

    /// <summary>Whether a prop of this girth stands here at all.</summary>
    /// <remarks>
    /// <para>
    /// <b>Its whole girth on grass</b> (GEN-6a). Grass is what is left over — every shape laid before this
    /// answers for its own ground, and everything standing on grass claimed it — and the same test is what
    /// keeps a prop on the map (GEN-2b): off the town is not grass.
    /// </para>
    /// <para>
    /// <b>And no collar</b> (GEN-6a): the girth is cleared against the ground's answer, which is the walk
    /// down every street, and a collar over that would only hold the verge back from the street it is a
    /// verge of. The answer is the drawn walk itself — the rings it is filled from (<c>GroundShapes.At</c>) — so
    /// a girth cleared of it is clear of what is drawn, at a corner as much as down a street.
    /// </para>
    /// <para>
    /// <b>And clear of the props already laid</b> (GEN-6c). The ground cannot see them: a prop is no shape
    /// of the town's and claims nothing, because the only thing that ever has to know where one stands is
    /// the next candidate along.
    /// </para>
    /// </remarks>
    static bool Stands(
        Vector2 atM, float reachM, float stepM, GroundShapes ground, GenClaims claims, PropScatter scatter) =>
        ground.IsAll(atM, reachM, stepM, Ground.Grass)
        && claims.IsFree(atM, reachM)
        && !scatter.Reaches(atM, reachM);

    /// <summary>
    /// What a prop on a verge is (GEN-6b): a share of the street's furniture, and planting for the rest.
    /// </summary>
    /// <remarks>
    /// <b>It used to depend on whether a car park stood behind the verge</b>, and there is no answer to that
    /// question any more — the subject is gone — so the rule it fed is what changed rather than being
    /// worked around with a second source for the same answer.
    /// </remarks>
    static PropKind OnAVerge(SimConfig config, ref Rng draw)
    {
        if (draw.NextFloat() < config.CityGen.PropFurnitureShare) return PropKind.UrbanFurniture;

        // A verge is not a flower bed end to end: a share of what is planted along one is whatever the
        // country either side of the town grows anyway, which is what keeps a street from reading as a
        // catalogue of the things a town plants, laid out along the kerb.
        return draw.NextFloat() < config.CityGen.PropWildOnAVergeShare ? PropKind.WildNature : PropKind.UrbanNature;
    }

    /// <summary>The widest a prop of one kind is drawn: only the wild set holds art authored past the ordinary band (GEN-6b).</summary>
    static float WidestM(PropKind kind, SimConfig config) =>
        kind == PropKind.WildNature ? config.CityGen.PropWildDiameterMaxM : config.CityGen.PropDiameterMaxM;
}
