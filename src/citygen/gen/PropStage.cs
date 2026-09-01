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
/// passes that answer different questions (GEN-6b). <b>The paved edges are walked first</b> — every road's
/// two kerbs and every car park's four sides — because a verge is a line and not an area: what stands along
/// one is found by following the thing it belongs to, on that thing's own bearing, and not by sweeping a
/// lattice and asking each square whether it happens to be near a street. <b>Then the ground the town is
/// not on is swept</b>, well clear of the walk those passes took.
/// </para>
/// <para>
/// <b>Neither pass searches for anything</b>: a candidate that does not stand is not a prop, and the ground
/// it was refused for is not tried again from another angle. Everything the stage needs was laid before it —
/// the roads, the pavement, the bays and the buildings — which is what makes one pass over each enough.
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
        TownBrief brief, ArcSeg[][] chains, CityPlan.ParkingLotArrays lots, GenRaster raster,
        GenClaims claims, SimConfig config, ref Rng draw)
    {
        var acrossM = new Vector2(brief.WidthM, brief.HeightM);
        var widestM = MathF.Max(config.CityGen.PropDiameterMaxM, config.CityGen.PropWildDiameterMaxM);
        var scatter = PropScatter.Over(acrossM, widestM, config.CityGen.PropApartM);

        AlongTheKerbs(chains, raster, claims, config, scatter, ref draw);
        AroundTheLots(lots, raster, claims, config, scatter, ref draw);
        OverWhatIsLeft(acrossM, raster, claims, config, scatter, ref draw);

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
        ArcSeg[][] chains, GenRaster raster, GenClaims claims, SimConfig config, PropScatter scatter,
        ref Rng draw)
    {
        var kerbM = (config.RoadWidthM * 0.5f) + config.PavementWidthM;
        var nearM = config.CityGen.PropVergeNearM;
        var bandM = config.CityGen.PropVergeFarM - nearM;
        var pitchM = config.CityGen.PropVergePitchM;
        var stubM = RoadStage.StubM(config);

        // A car park's tarmac is on the far side of the pavement the prop is standing off, so what says a
        // prop is beside one is the whole verge and that pavement together.
        var lotReachM = config.CityGen.PropVergeFarM + config.PavementWidthM;

        for (var road = 0; road < chains.Length; road++)
        {
            if (chains[road].Length == 0) continue;

            var lengthM = Spline.TotalLengthM(chains[road]);
            foreach (var hand in (ReadOnlySpan<int>)[-1, 1])
            {
                for (var alongM = stubM; alongM <= lengthM - stubM; alongM += pitchM)
                {
                    var stationM = alongM + (draw.NextFloat() * pitchM);
                    if (stationM > lengthM - stubM) continue;

                    var on = Spline.SampleAt(chains[road], stationM);
                    var atM = on.PositionM + (on.Right * hand * (kerbM + nearM + (draw.NextFloat() * bandM)));
                    if (raster.At(atM) != Ground.Grass) continue;

                    var kind = OnAVerge(Holds(raster.GroundsUnder(atM, lotReachM), Ground.Parking), config, ref draw);
                    var reachM = draw.NextFloat(config.CityGen.PropDiameterMinM, WidestM(kind, config)) * 0.5f;

                    // <b>Its girth and no collar</b> (GEN-6a): this prop is not cleared against the cells
                    // alone. It stands a known distance off the pavement of the road it was laid from, and
                    // the walk began past the stub every junction lays its own ground and fillets across —
                    // so the drawn kerb corner a collar is there to keep a blind candidate out of is
                    // nowhere near it.
                    if (!Stands(atM, reachM, 0f, raster, claims, scatter)) continue;

                    scatter.Add(atM, reachM, on.HeadingRad, kind);
                }
            }
        }
    }

    /// <summary>
    /// <b>And around the car parks, on the same terms</b> (GEN-6b). A lot reaches back over the pavement it
    /// fronts, so the grass beyond it is on nobody's kerb: the road is a car park's depth away and its own
    /// walk lands on tarmac there. All four sides are walked, and <b>the ones facing the street are refused
    /// by the ground under them</b> rather than by the stage knowing which way out is — a lot carries its
    /// axis and its extent and never which of its sides the road was on.
    /// </summary>
    static void AroundTheLots(
        CityPlan.ParkingLotArrays lots, GenRaster raster, GenClaims claims, SimConfig config,
        PropScatter scatter, ref Rng draw)
    {
        for (var lot = 0; lot < lots.Count; lot++)
        {
            var centreM = lots.CentreM[lot];
            var along = lots.Axis[lot];
            var across = Heading.RightOf(along);
            var halfM = lots.HalfExtentM[lot];

            foreach (var side in (ReadOnlySpan<int>)[-1, 1])
            {
                AlongAnEdge(
                    centreM + (across * (halfM.Y * side)), along, across * side, halfM.X,
                    raster, claims, config, scatter, ref draw);
                AlongAnEdge(
                    centreM + (along * (halfM.X * side)), across, along * side, halfM.Y,
                    raster, claims, config, scatter, ref draw);
            }
        }
    }

    /// <summary>
    /// One straight edge walked and furnished on its own bearing, which is the whole of what a car park's
    /// side is — a road's is walked rather than stepped, because a road bends and an edge does not.
    /// </summary>
    /// <remarks>
    /// <b>The verge begins past the walk that wraps the lot</b> (GEN-4d), which is that lot's pavement: the
    /// ring of grass a body gets round a car park is claimed before anything fills it, so a prop laid into
    /// it would be a planter in the middle of the only way past. It is the same relation a street's verge
    /// stands in — the walk, then the band — measured off a rectangle instead of off a curve.
    /// </remarks>
    static void AlongAnEdge(
        Vector2 middleM, Vector2 tangent, Vector2 outward, float halfM, GenRaster raster, GenClaims claims,
        SimConfig config, PropScatter scatter, ref Rng draw)
    {
        var wrapM = config.PavementWidthM;
        var nearM = config.CityGen.PropVergeNearM;
        var bandM = config.CityGen.PropVergeFarM - nearM;
        var pitchM = config.CityGen.PropVergePitchM;
        var bearingRad = RoadStage.Facing(tangent);

        for (var alongM = -halfM; alongM <= halfM; alongM += pitchM)
        {
            var stationM = alongM + (draw.NextFloat() * pitchM);
            if (stationM > halfM) continue;

            var atM = middleM + (tangent * stationM)
                      + (outward * (wrapM + nearM + (draw.NextFloat() * bandM)));
            if (raster.At(atM) != Ground.Grass) continue;

            var kind = OnAVerge(true, config, ref draw);
            var reachM = draw.NextFloat(config.CityGen.PropDiameterMinM, WidestM(kind, config)) * 0.5f;
            if (!Stands(atM, reachM, 0f, raster, claims, scatter)) continue;

            scatter.Add(atM, reachM, bearingRad, kind);
        }
    }

    /// <summary>
    /// <b>The last pass: what grows where the town is not</b> (GEN-6b). A stratified sweep — one candidate
    /// per cell of a coarse lattice, jittered inside it — so the cost is the ground rather than the count,
    /// and every candidate standing within a stand-off of a walk or a car park is left to the first pass.
    /// <b>Nothing here is laid on a bearing</b>: what the wild set holds has no front to turn.
    /// </summary>
    static void OverWhatIsLeft(
        Vector2 acrossM, GenRaster raster, GenClaims claims, SimConfig config, PropScatter scatter,
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
                if (raster.At(atM) != Ground.Grass) continue;

                var beside = raster.GroundsUnder(atM, standOffM);
                if (Holds(beside, Ground.Sidewalk) || Holds(beside, Ground.Parking)) continue;

                var reachM = draw.NextFloat(
                    config.CityGen.PropDiameterMinM, config.CityGen.PropWildDiameterMaxM) * 0.5f;
                if (!Stands(atM, reachM, config.PavementCornerRadiusM, raster, claims, scatter)) continue;

                scatter.Add(atM, reachM, 0f, PropKind.WildNature);
            }
        }
    }

    /// <summary>
    /// Whether a prop of this girth stands here at all, keeping whatever collar its pass owes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Its whole girth on grass</b> (GEN-6a). Grass is what is left over — everything laid before this
    /// painted its own ground, and everything standing on grass claimed it — and the same test is what keeps
    /// a prop on the map (GEN-2b): off the grid is not grass.
    /// </para>
    /// <para>
    /// <b>The collar is the sweep's and not the verge's.</b> The ground is classified cell by cell where the
    /// walk is <em>drawn</em> as one union with its re-entrant corners rounded off (TER-3c.4), so a
    /// candidate whose only knowledge of the pavement is the cells can be standing in the middle of a drawn
    /// kerb corner — and keeps the radius the pavement turns those on. A prop laid off a road's own line
    /// knows exactly where that pavement is and never walks the stub the corners are inside of.
    /// </para>
    /// <para>
    /// <b>And clear of the props already laid</b> (GEN-6c). The ground test cannot see them: a prop paints
    /// no cell and claims none, because the only thing that ever has to know where one stands is the next
    /// candidate along.
    /// </para>
    /// </remarks>
    static bool Stands(
        Vector2 atM, float reachM, float collarM, GenRaster raster, GenClaims claims, PropScatter scatter) =>
        raster.IsAll(atM, reachM + collarM, Ground.Grass)
        && claims.IsFree(atM, reachM)
        && !scatter.Reaches(atM, reachM);

    /// <summary>
    /// What a prop on a verge is (GEN-6b): furniture where there is a car park to stand it beside, and
    /// planting everywhere else.
    /// </summary>
    static PropKind OnAVerge(bool besideALot, SimConfig config, ref Rng draw)
    {
        if (besideALot && draw.NextFloat() < config.CityGen.PropFurnitureShare) return PropKind.UrbanFurniture;

        // A verge is not a flower bed end to end: a share of what is planted along one is whatever the
        // country either side of the town grows anyway, which is what keeps a street from reading as a
        // catalogue of the things a town plants, laid out along the kerb.
        return draw.NextFloat() < config.CityGen.PropWildOnAVergeShare ? PropKind.WildNature : PropKind.UrbanNature;
    }

    static bool Holds(int grounds, Ground ground) => (grounds & (1 << (int)ground)) != 0;

    /// <summary>The widest a prop of one kind is drawn: only the wild set holds art authored past the ordinary band (GEN-6b).</summary>
    static float WidestM(PropKind kind, SimConfig config) =>
        kind == PropKind.WildNature ? config.CityGen.PropWildDiameterMaxM : config.CityGen.PropDiameterMaxM;
}
