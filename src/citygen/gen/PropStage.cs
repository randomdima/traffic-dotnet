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
/// line and not an area: what stands along one is found by following the road it belongs to, on that road's
/// own bearing, and not by sweeping a lattice and asking each square whether it happens to be near a
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
        TownBrief brief, CityPlan.RoadArrays roads, GroundShapes ground, GenClaims claims, SimConfig config,
        ref Rng draw)
    {
        var acrossM = new Vector2(brief.WidthM, brief.HeightM);
        var widestM = MathF.Max(config.CityGen.PropDiameterMaxM, config.CityGen.PropWildDiameterMaxM);
        var scatter = PropScatter.Over(acrossM, widestM, config.CityGen.PropApartM);

        AlongTheKerbs(roads, ground, claims, config, scatter, ref draw);
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
    /// <b>The first pass: what a town puts along its own kerbs</b> (GEN-6b). Every road is walked on both
    /// hands and a candidate stands out in the verge — the band of grass beyond the pavement's outer edge —
    /// <b>on the road's own bearing there</b>, so a look with a front runs with the street rather than with
    /// the compass. The ends the walk leaves out are the stub every junction lays its own ground across.
    /// </summary>
    static void AlongTheKerbs(
        CityPlan.RoadArrays roads, GroundShapes ground, GenClaims claims, SimConfig config, PropScatter scatter,
        ref Rng draw)
    {
        var nearM = config.CityGen.PropVergeNearM;
        var bandM = config.CityGen.PropVergeFarM - nearM;
        var pitchM = config.CityGen.PropVergePitchM;
        // How much of each end of a road the junction's own ground reaches over: the standoff its arms'
        // lanes end at, and the movements between them (TER-5). A verge walked into that is a verge over
        // tarmac.
        var stubM = config.JunctionRadiusM + config.LaneWidthM;

        for (var road = 0; road < roads.Count; road++)
        {
            var chain = roads.SegmentsOf(road);
            if (chain.Length == 0) continue;

            // The edge of the road being walked and never the catalogue's: a one-way street stands half a
            // carriageway nearer its middle (TER-4d). <b>The road's own half and no walk beside it</b> —
            // nothing lays a pavement, so the verge begins where the tarmac stops, and the figure the
            // scatter is cleared against has to be the one the ground answers with (TER-7).
            var kerbM = roads.WidthM[road] * 0.5f;
            var lengthM = Spline.TotalLengthM(chain);
            foreach (var hand in (ReadOnlySpan<int>)[-1, 1])
            {
                for (var alongM = stubM; alongM <= lengthM - stubM; alongM += pitchM)
                {
                    var stationM = alongM + (draw.NextFloat() * pitchM);
                    if (stationM > lengthM - stubM) continue;

                    var on = Spline.SampleAt(chain, stationM);
                    var atM = on.PositionM + (on.Right * hand * (kerbM + nearM + (draw.NextFloat() * bandM)));
                    if (ground.At(atM) != Ground.Grass) continue;

                    var kind = OnAVerge(config, ref draw);
                    var reachM = draw.NextFloat(config.CityGen.PropDiameterMinM, WidestM(kind, config)) * 0.5f;
                    if (!Stands(atM, reachM, config.Terrain.GroundStepM, ground, claims, scatter)) continue;

                    scatter.Add(atM, reachM, on.HeadingRad, kind);
                }
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
    /// <b>And no collar</b>, because the ground answered here is the ground that is drawn (TER-7). Every
    /// pass used to owe one: the walk is drawn as a union of pieces grown by one figure (TER-3c.3), and a
    /// candidate reading a raster painted from the pieces alone could stand in the middle of a corner
    /// nothing had stamped. The corners are in the answer now, so a candidate cleared against it is
    /// clear — and a collar over that would only hold the verge back from the street it is a verge of.
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
