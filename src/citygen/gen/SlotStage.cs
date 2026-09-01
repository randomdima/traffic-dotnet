using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>What stands along the streets</b>: the frontage cut into slots, and each slot filled with a building
/// or with a car park.
/// </summary>
/// <remarks>
/// <para>
/// <b>A slot is legal before anything is put in it.</b> It is cut at a fixed stand-off from the kerb, on the
/// road's own bearing, with the walkable padding GEN-3 wants already inside the ground it claims — so a slot
/// that was cut is a slot that can be filled, and a slot that could not be cut simply is not there. Nothing
/// is placed and taken back.
/// </para>
/// <para>
/// <b>A car park is a slot with bays in it</b> and not a second kind of placement (GEN-4b): the share of
/// the frontage given over to parking is the brief's, and how many bays one holds is its own draw between
/// the fewest and the most a lot may be. <b>Neighbouring slots that both drew one leave one lot and not a
/// longer one</b> (GEN-16): a car park is a handful of spaces beside a street, and frontage joined end to
/// end is an apron rather than a bigger one of those.
/// </para>
/// <para>
/// <b>A building fronts the street it stands on</b> (GEN-2a): its bearing is the road's, and its way in is
/// on the pavement between its front wall and the kerb, which is walkable ground by construction because the
/// pavement was painted the length of that road.
/// </para>
/// </remarks>
internal static class SlotStage
{
    /// <summary>
    /// How much of a building's own size is left clear around it, so two neighbours cannot touch (GEN-3).
    /// <b>A car park's clearance is not a share of itself</b> but the walk that wraps it (GEN-4d).
    /// </summary>
    const float PaddingShare = 0.25f;

    internal readonly record struct Laid(
        CityPlan.BuildingArrays Buildings, CityPlan.ParkingLotArrays ParkingLots);

    public static Laid Lay(
        TownLayout layout, ArcSeg[][] chains, TownBrief brief, GenRaster raster, GenClaims claims,
        GroundPainter painter, SimConfig config, ReadOnlySpan<Vector2> roofsM, ref Rng draw)
    {
        var centreM = new List<Vector2>();
        var sizeM = new List<Vector2>();
        var headingRad = new List<float>();
        var entryM = new List<Vector2>();

        var lotCentreM = new List<Vector2>();
        var lotAxis = new List<Vector2>();
        var lotHalfM = new List<Vector2>();
        var bayOffsets = new List<int> { 0 };
        var bayM = new List<Vector2>();
        var bayHeadingRad = new List<float>();

        var widthM = config.RoadWidthM;
        var walkM = config.PavementWidthM;
        var kerbM = (widthM * 0.5f) + walkM;
        var bayLengthM = config.ParkingSpaceLengthM;
        var bayWidthM = config.ParkingSpaceWidthM;
        var pitchM = config.CityGen.BuildingSideMaxM * (1f + PaddingShare);
        var sectionM = config.ParkingSectionShortestStretchM + config.ParkingSectionSetbackM;

        // Room is asked for the widest lot the draw can produce, so a slot that has room has it whatever
        // the draw then asks for.
        var lotHalfAlongM = config.CityGen.BaysPerLotMost * bayWidthM * 0.5f;

        // <b>Every slot in the town is cut before any of them is filled, and they are filled in a drawn
        // order.</b> Filled road by road instead, a town whose brief asks for fewer buildings than its
        // frontages afford is a town built solid along whichever streets came first and empty everywhere
        // else — the count runs out before the sweep reaches the rest of the map.
        //
        // <b>They are cut kerb by kerb</b>, each one's slots in the order they stand along it, because
        // whether two of them are one car park is a question about the frontage between them (GEN-16).
        var slots = new List<Slot>();
        var stubM = RoadStage.StubM(config);
        for (var road = 0; road < chains.Length; road++)
        {
            if (chains[road].Length == 0) continue;

            var lengthM = Spline.TotalLengthM(chains[road]);
            foreach (var hand in (ReadOnlySpan<int>)[-1, 1])
            {
                for (var alongM = stubM; alongM <= lengthM - stubM; alongM += pitchM)
                {
                    // A lot's frontage is a stretch of road cut out for the ways into it, and a cut has to
                    // leave a stretch standing either side of itself (<c>ParkingSections</c>). Where the
                    // road does not afford that, the slot is a building's rather than a lot's — the
                    // alternative is a car park no lane can be entered from.
                    var roomForALot = alongM - lotHalfAlongM >= sectionM
                                      && alongM + lotHalfAlongM <= lengthM - sectionM;
                    slots.Add(new Slot(road, hand, alongM, roomForALot));
                }
            }
        }

        // <b>Which slots want a car park, and how many bays each of them wants</b>, drawn slot by slot in
        // the order they were cut so that what a kerb carries is what the frontage along it drew rather
        // than whichever of them a shuffle reached first. A slot nobody parks on carries no bays at all.
        var bays = new int[slots.Count];
        var span = config.CityGen.BaysPerLotMost - config.CityGen.BaysPerLotFewest + 1;
        for (var slot = 0; slot < slots.Count; slot++)
        {
            if (!slots[slot].RoomForALot || draw.NextFloat() >= brief.ParkingSlotShare) continue;

            bays[slot] = config.CityGen.BaysPerLotFewest + draw.NextInt(span);
        }

        LayTheLots(
            slots, bays, chains, config, raster, claims, painter, bayLengthM, bayWidthM,
            widthM * 0.5f, lotCentreM, lotAxis, lotHalfM, bayOffsets, bayM, bayHeadingRad);

        var frontages = new List<Slot>(slots.Count);
        for (var slot = 0; slot < slots.Count; slot++)
        {
            if (bays[slot] == 0) frontages.Add(slots[slot]);
        }

        for (var slot = frontages.Count - 1; slot > 0; slot--)
        {
            var other = draw.NextInt(slot + 1);
            (frontages[slot], frontages[other]) = (frontages[other], frontages[slot]);
        }

        foreach (var (road, hand, alongM, _) in frontages)
        {
            if (centreM.Count >= brief.Buildings) break;

            var on = Spline.SampleAt(chains[road], alongM);
            LayABuilding(
                on.PositionM, on.Right * hand, kerbM, walkM, config, raster, claims, roofsM,
                centreM, sizeM, headingRad, entryM, ref draw);
        }

        var uses = Services.Decide(centreM, lotCentreM, brief, config);
        return new Laid(
            new CityPlan.BuildingArrays
            {
                CentreM = [.. centreM], SizeM = [.. sizeM], HeadingRad = [.. headingRad],
                Capacity = Filled(centreM.Count, config.CityGen.BuildingCapacity),
                Use = uses,
                EntryOffsets = OneEach(entryM.Count), EntryPointM = [.. entryM],
            },
            new CityPlan.ParkingLotArrays
            {
                CentreM = [.. lotCentreM], Axis = [.. lotAxis], HalfExtentM = [.. lotHalfM],
                SpaceOffsets = [.. bayOffsets], SpacePositionM = [.. bayM], SpaceHeadingRad = [.. bayHeadingRad],
            });
    }

    /// <summary>
    /// One building on one slot. <b>It is sized by the roof it will wear</b> — the footprints come from the
    /// catalogue through the caller (<c>BuildingCatalog.OrdinaryFootprintsM</c>), so the picture drawn on it
    /// is the size the plan authored rather than the nearest thing to it.
    /// </summary>
    static void LayABuilding(
        Vector2 onRoadM, Vector2 outward, float kerbM, float walkM, SimConfig config, GenRaster raster,
        GenClaims claims, ReadOnlySpan<Vector2> roofsM, List<Vector2> centreM, List<Vector2> sizeM,
        List<float> headingRad, List<Vector2> entryM, ref Rng draw)
    {
        var footprintM = roofsM[draw.NextInt(roofsM.Length)];

        // <b>It stands square to the street, with its door on the street's side</b> (GEN-2a). The door is
        // the wall a quarter turn from the heading the plan carries, so the heading is the one that puts
        // that wall against the kerb rather than along it.
        var facing = Heading.RightOf(outward);
        var halfM = new Vector2(footprintM.X * 0.5f, footprintM.Y * 0.5f) * (1f + PaddingShare);
        var standM = onRoadM + (outward * (kerbM + config.Building.FrontGapM + halfM.Y));

        if (!raster.IsAll(standM, facing, halfM, Ground.Grass)) return;
        if (!claims.IsFree(standM, facing, halfM)) return;

        claims.Claim(standM, facing, halfM);
        centreM.Add(standM);
        sizeM.Add(footprintM);
        headingRad.Add(RoadStage.Facing(facing));

        // The way in stands between the front wall and the kerb, on the pavement the road already laid —
        // walkable ground by construction, which is what GEN-5 asks of every entrance.
        entryM.Add(onRoadM + (outward * (kerbM - (walkM * 0.5f))));
    }

    /// <summary>One cut of frontage: which kerb it is on, how far along the road it stands, and what may fill it.</summary>
    readonly record struct Slot(int Road, int Hand, float AlongM, bool RoomForALot);

    /// <summary>
    /// The rectangle a slot stands its bays on, and how many of them fill it — everything about a car park
    /// that is settled before any of it is laid.
    /// </summary>
    readonly record struct LotShape(Vector2 BayCentreM, Vector2 Along, Vector2 Outward, Vector2 BayHalfM, int Bays);

    /// <summary>
    /// <b>Every car park the town stands, each one the handful of bays its own slot drew</b> (GEN-4b). A
    /// kerb's slots are walked in the order they stand along it, and each lot that wanted one is laid as the
    /// rectangle its own count of bays takes.
    /// </summary>
    /// <remarks>
    /// <b>Two lots that would stand inside a locality of each other are one lot fewer and never one longer</b>
    /// (GEN-16). A lot is a handful of spaces beside a street and a run of frontage joined end to end is an
    /// apron, so where the next slot along drew one too it is dropped rather than merged into its neighbour —
    /// and a lot fewer is a shortfall the census reports (GEN-8) rather than a car park the length of a block.
    /// </remarks>
    static void LayTheLots(
        List<Slot> slots, int[] bays, ArcSeg[][] chains, SimConfig config, GenRaster raster,
        GenClaims claims, GroundPainter painter, float bayLengthM, float bayWidthM,
        float roadHalfM, List<Vector2> lotCentreM, List<Vector2> lotAxis, List<Vector2> lotHalfM,
        List<int> bayOffsets, List<Vector2> bayM, List<float> bayHeadingRad)
    {
        var localityM = config.CityGen.LocalityM;
        var mouthM = raster.CellSizeM;

        // How far the kerb may stand off the chord the lot is laid on (GEN-4b): a hand's width, which is
        // the same figure the kerb line's own break is judged by (<c>RoadFrontages</c>). A lot whose kerb
        // bows further than that off its chord is one whose middle no longer reaches the carriageway.
        var straightM = config.Road.PaintLineWidthM;
        var laidRoad = -1;
        var laidHand = 0;
        var laid = default(LotShape);

        for (var at = 0; at < slots.Count; at++)
        {
            if (bays[at] == 0) continue;

            var kerb = slots[at];
            var halfAlongM = bays[at] * bayWidthM * 0.5f;
            var chain = chains[kerb.Road];
            var fromM = kerb.AlongM - halfAlongM;
            var toM = kerb.AlongM + halfAlongM;
            if (!KeepsToItsChord(chain, fromM, toM, straightM)) continue;

            var shape = Shape(
                chain, fromM, toM, bays[at], kerb.Hand, bayWidthM, bayLengthM, roadHalfM, mouthM);
            if (kerb.Road == laidRoad && kerb.Hand == laidHand && ApartM(laid, shape) < localityM) continue;
            if (!Stands(shape, raster, claims, config.PavementWidthM)) continue;

            LayALot(
                shape, config, claims, painter, mouthM, bayWidthM, lotCentreM, lotAxis, lotHalfM, bayOffsets,
                bayM, bayHeadingRad);

            laidRoad = kerb.Road;
            laidHand = kerb.Hand;
            laid = shape;
        }
    }

    /// <summary>
    /// How much kerb stands between two lots on it, rectangle to rectangle. <b>Measured along the kerb and
    /// between the two shapes</b> (GEN-4d), which on a bend is shorter than the road between their middles:
    /// a lot is a rectangle on a chord, and the arc it was cut from is the longer of the two.
    /// </summary>
    static float ApartM(LotShape laid, LotShape next) =>
        MathF.Abs(Vector2.Dot(laid.Along, next.BayCentreM - laid.BayCentreM))
        - laid.BayHalfM.X - next.BayHalfM.X;

    /// <summary>
    /// Where a stretch of frontage puts its bays. <b>The rectangle stands on the chord between the run's two
    /// ends</b> (GEN-4b) — a lot is an oriented rectangle, so the kerb it hangs off is the line between where
    /// it starts and where it stops rather than the tangent at any one point of it.
    /// </summary>
    static LotShape Shape(
        ArcSeg[] chain, float fromM, float toM, int bays, int hand, float bayWidthM, float bayLengthM,
        float roadHalfM, float mouthM)
    {
        var start = Spline.SampleAt(chain, fromM);
        var end = Spline.SampleAt(chain, toM);
        var chordM = end.PositionM - start.PositionM;
        var along = chordM.LengthSquared() > 0f ? Vector2.Normalize(chordM) : start.Direction;
        var outward = Heading.RightOf(along) * hand;

        // <b>Whole bays and no remainder</b>: the stretch was cut to a count of them in the first place, and
        // they stand side by side at their own width sharing the line between each pair (GEN-4c), so the
        // rectangle is that count wide rather than a run with a stripe of bare tarmac at one end.
        var bayHalfM = new Vector2(bays * bayWidthM * 0.5f, bayLengthM * 0.5f);
        var middleM = (start.PositionM + end.PositionM) * 0.5f;
        return new LotShape(
            middleM + (outward * (roadHalfM + mouthM + bayHalfM.Y)), along, outward, bayHalfM, bays);
    }

    /// <summary>
    /// Whether the kerb keeps to its own chord over a stretch of it (GEN-4b). <b>A lot is a rectangle laid on
    /// that chord</b>, so a kerb that bows off it by more than the mouth in front of the bays is one the lot's
    /// middle no longer reaches the carriageway over — which is a car park behind the pavement rather than
    /// against it, and is what bounds how much frontage one run may take.
    /// </summary>
    static bool KeepsToItsChord(ArcSeg[] chain, float fromM, float toM, float toleranceM)
    {
        var startM = Spline.SampleAt(chain, fromM).PositionM;
        var chordM = Spline.SampleAt(chain, toM).PositionM - startM;
        var lengthM = chordM.Length();
        if (lengthM <= 0f) return true;

        var unit = chordM / lengthM;
        for (var alongM = fromM; alongM < toM; alongM += toleranceM)
        {
            var offM = Spline.SampleAt(chain, alongM).PositionM - startM;
            if (MathF.Abs(Spline.Cross(unit, offM)) > toleranceM) return false;
        }

        return true;
    }

    /// <summary>
    /// Whether the ground and what is already on it take a car park of this shape.
    /// </summary>
    /// <remarks>
    /// <b>The ground is tested a cell clear of the road and the tarmac is laid back over that cell.</b> A
    /// carriageway is painted cell by cell, so its edge stands wherever the centreline's own offset rounded
    /// to — up to a cell wider than the road is. Tested against that, nearly every lot in the town is refused
    /// for standing on a road it does not touch; laid short of it, every lot has a strip of pavement between
    /// its bays and the street.
    /// <para>
    /// <b>And a lot claims the walk that wraps it</b> (TER-3c.3), not a share of its own size (GEN-4d): the
    /// pavement is drawn a walk's width round every lot, so two standing closer than two walks apart have one
    /// wrap between them and the verge they pinch out is a cusp rather than a corner the walk can turn
    /// (TER-3c.4).
    /// </para>
    /// </remarks>
    static bool Stands(LotShape shape, GenRaster raster, GenClaims claims, float walkM) =>
        raster.IsAll(shape.BayCentreM, shape.Along, shape.BayHalfM, Ground.Grass, Ground.Sidewalk)
        && claims.IsFree(shape.BayCentreM, shape.Along, shape.BayHalfM + new Vector2(walkM));

    /// <summary>
    /// <b>A car park reaches the carriageway</b> (GEN-4b): its own tarmac stands where the pavement would be,
    /// so a car turns off the road onto ground it may drive on and the kerb line is broken over its mouth. A
    /// lot standing back behind the walk is one that every way in crosses a pavement to reach.
    /// </summary>
    static void LayALot(
        LotShape shape, SimConfig config, GenClaims claims, GroundPainter painter, float mouthM,
        float bayWidthM, List<Vector2> lotCentreM, List<Vector2> lotAxis, List<Vector2> lotHalfM,
        List<int> bayOffsets, List<Vector2> bayM, List<float> bayHeadingRad)
    {
        claims.Claim(shape.BayCentreM, shape.Along, shape.BayHalfM + new Vector2(config.PavementWidthM));

        // The lot itself is the bays and the mouth in front of them, which is what reaches the road.
        var halfM = new Vector2(shape.BayHalfM.X, shape.BayHalfM.Y + (mouthM * 0.5f));
        var standM = shape.BayCentreM - (shape.Outward * (mouthM * 0.5f));
        painter.Lot(standM, shape.Along, halfM);

        lotCentreM.Add(standM);
        lotAxis.Add(shape.Along);
        lotHalfM.Add(halfM);

        // Nose in, off the road: a bay's own heading is the way a car stands in it, and a rank of them
        // facing away from the kerb is what the mouth of the lot is for. <b>They stand side by side at
        // their own width</b>, because the room between two parked cars is the clearance each bay already
        // carries round the car in it (GEN-4c) and counting it twice is a lot of detached bays with a
        // stripe of bare tarmac between every pair.
        for (var bay = 0; bay < shape.Bays; bay++)
        {
            var acrossM = ((bay + 0.5f) * bayWidthM) - (shape.Bays * bayWidthM * 0.5f);
            bayM.Add(shape.BayCentreM + (shape.Along * acrossM));
            bayHeadingRad.Add(RoadStage.Facing(shape.Outward));
        }

        bayOffsets.Add(bayM.Count);
    }

    static int[] Filled(int count, int value)
    {
        var filled = new int[count];
        Array.Fill(filled, value);
        return filled;
    }

    /// <summary>Every building has one way in, so its offsets are the numbers up to its count.</summary>
    static int[] OneEach(int count)
    {
        var offsets = new int[count + 1];
        for (var at = 0; at <= count; at++) offsets[at] = at;
        return offsets;
    }
}
