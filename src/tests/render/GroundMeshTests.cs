using System.Collections.Concurrent;
using System.Numerics;
using TrafficSimulation.App.Render;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Terrain;
using Xunit;

namespace TrafficSimulation.Tests.Render;

/// <summary>
/// The ground's triangles, asked of every shipped map without a GPU in the room. What a picture can
/// only be judged on — a dashed line's pitch, whether the paint sits on the road — is the render and
/// agent tiers' job; what is asserted here is everything about the mesh that <em>is</em> a fact.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class GroundMeshTests
{
    public static TheoryData<string> Maps => Towns.EveryShippedMap();

    static readonly ConcurrentDictionary<string, GroundMesh> Laid = new();

    /// <summary>
    /// <b>One map's ground, laid once and read by every claim about it.</b> The mesh is a function of the
    /// plan and the figures and nothing here writes to it, so eleven claims over eight maps were eighty-eight
    /// triangulations of the same eight towns.
    /// </summary>
    static GroundMesh Ground(string map) => Laid.GetOrAdd(map, at => GroundMesh.Build(Towns.Of(at), SimConfig.Shipped()));

    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryShippedMapLaysGroundThatIsWellFormed(string map)
    {
        var mesh = Ground(map);

        Assert.True(mesh.Indices.Length > 0, $"{map} lays no ground at all");
        Assert.Equal(0, mesh.Indices.Length % 3);
        foreach (var index in mesh.Indices) Assert.InRange(index, 0u, (uint)mesh.Vertices.Length - 1);

        foreach (var vertex in mesh.Vertices)
        {
            Assert.True(float.IsFinite(vertex.PositionM.X) && float.IsFinite(vertex.PositionM.Y),
                $"{map} lays a corner at {vertex.PositionM}");
            Assert.True(float.IsFinite(vertex.Uv.X) && float.IsFinite(vertex.Uv.Y));
        }
    }

    /// <summary>
    /// Every surface's texture is anchored to the <b>world origin</b> and not to the shape being
    /// painted, which is what makes the triangulation invisible: cut a shape into triangles
    /// differently and the picture does not change. The texture coordinate is therefore the position
    /// over the surface's own period, everywhere, with nothing per-shape in it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryTextureIsAnchoredToTheWorldOrigin(string map)
    {
        var config = SimConfig.Shipped();
        var periods = GroundMesh.Periods(config);
        var mesh = Ground(map);

        foreach (var vertex in mesh.Vertices)
        {
            var period = vertex.Surface == Surface.Paint ? 1f : periods[(int)vertex.Surface];
            Assert.Equal(vertex.PositionM.X / period, vertex.Uv.X, tolerance: 1e-3f);
            Assert.Equal(vertex.PositionM.Y / period, vertex.Uv.Y, tolerance: 1e-3f);
        }
    }

    /// <summary>
    /// The order the triangles are laid in is the order they are painted in, and grass over the whole
    /// world is the first thing painted. There is no depth buffer and nothing sorts, so a
    /// mesh whose first triangle is anything else is a town with a hole in it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void GrassIsPaintedFirstAndCoversTheWholeWorld(string map)
    {
        var plan = Towns.Of(map);
        var mesh = Ground(map);

        var corners = new HashSet<Vector2>();
        for (var vertex = 0; vertex < 4; vertex++)
        {
            Assert.Equal(Surface.Grass, mesh.Vertices[vertex].Surface);
            corners.Add(mesh.Vertices[vertex].PositionM);
        }

        Assert.Contains(Vector2.Zero, corners);
        Assert.Contains(plan.WorldSizeM, corners);
    }

    /// <summary>
    /// A water outline is cut into ears, not fanned: a river is concave, and a fan from one vertex
    /// would paint over its own banks. Ear clipping yields exactly two fewer triangles than the
    /// outline has points, so anything less means it gave up part way and the water has a bite out
    /// of it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryWaterOutlineIsFullyTriangulated(string map)
    {
        var plan = Towns.Of(map);
        var mesh = Ground(map);

        var owed = 0;
        for (var outline = 0; outline < plan.Water.Outline.Count; outline++) owed += plan.Water.Outline.RingOf(outline).Length - 2;

        var laid = 0;
        for (var index = 0; index < mesh.Indices.Length; index += 3)
        {
            if (mesh.Vertices[(int)mesh.Indices[index]].Surface == Surface.Water) laid++;
        }

        Assert.Equal(owed, laid);
    }

    /// <summary>
    /// An edge is the surface drawn darker and paint is the surface drawn brighter — the two shades
    /// the shipped figures carry. The brighter one is why a tint is three floats: an eight-bit tint clamps at
    /// white, which is the ground it was meant to stand out from.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryPaintedBarIsBrighterThanTheGroundItIsOn(string map)
    {
        var plan = Towns.Of(map);
        var mesh = Ground(map);

        var marks = 0;
        var kerb = 0;
        var darker = 0;
        for (var vertex = 0; vertex < mesh.Vertices.Length; vertex++)
        {
            if (mesh.Vertices[vertex].Tint.X <= 1f)
            {
                if (mesh.Vertices[vertex].Tint.X < 1f) darker++;
                continue;
            }

            if (vertex < mesh.FirstMarkVertex) kerb++;
            else marks++;
        }

        // Every mark is one quad: the bars that were painted, the zebras' stripes and the lane dashes.
        Assert.Equal(0, marks % 4);
        Assert.True(marks / 4 >= plan.StopLines.Count + plan.Crosswalks.Count,
            $"{map} paints {marks / 4} marks for {plan.StopLines.Count} bars and {plan.Crosswalks.Count} crossings");
        Assert.True(kerb > 0, $"{map} lays no kerb line anywhere");
        Assert.True(plan.PavementWidthM == 0f || darker > 0, $"{map} draws no edge line anywhere");
    }

    /// <summary>
    /// <b>The dashed centreline stops before a junction rather than running into one</b>
    /// and it is broken by a crossing rather than laid down the middle of a
    /// zebra's bars. The two are one claim, because both are answered by where a mark that is neither a
    /// bar the plan placed nor a stripe on a crossing is allowed to stand.
    /// </summary>
    /// <remarks>
    /// <b>A junction that admits no fork is not a junction to stop before</b> (TER-6): its two arms are one
    /// carriageway, nothing turns across the ground between them, and a line that broke for it would leave a
    /// gap in the middle of a road that only bends.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NoDashIsLaidInAJunctionOrOnACrossing(string map)
    {
        var plan = Towns.Of(map);
        var arms = RoadCuts.ArmsPerJunction(plan);

        foreach (var markM in Marks(Ground(map)))
        {
            if (OnACrossing(plan, markM) || IsOneOfThePlansBars(plan, markM)) continue;

            for (var junction = 0; junction < plan.Junctions.Count; junction++)
            {
                if (arms[junction] < 3) continue;

                var reachM = plan.Junctions.RadiusM[junction];
                Assert.True(Vector2.DistanceSquared(markM, plan.Junctions.CentreM[junction]) > reachM * reachM,
                    $"{map}: a dash at {markM} is laid inside junction {junction}");
            }
        }
    }

    /// <summary>
    /// <b>A dash stops at the stop bar and does not carry on into the junction behind it.</b> The metres
    /// between the two are ground the movements through the box are driven over rather than the middle of
    /// a carriageway, and a dashed line laid down them draws a lane running into the junction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is the stronger half of <see cref="NoDashIsLaidInAJunctionOrOnACrossing"/>: a crossing is set
    /// back onto the arm it approaches (TER-6), so stopping at the disc leaves the metres between the two
    /// to be dashed and every arm of every junction in the town shows it.
    /// </para>
    /// <para>
    /// <b>A junction that admits no fork is not one of them</b> (TER-6): its bars belong to its own crossing
    /// rather than to a box, and what stands behind them is the same road bending, which a lane line runs
    /// down like any other.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NoDashIsLaidBetweenAStopBarAndItsJunction(string map)
    {
        var plan = Towns.Of(map);
        var arms = RoadCuts.ArmsPerJunction(plan);

        foreach (var markM in Marks(Ground(map)))
        {
            if (OnACrossing(plan, markM) || IsOneOfThePlansBars(plan, markM)) continue;

            for (var bar = 0; bar < plan.StopLines.Count; bar++)
            {
                var junction = plan.StopLines.Junction[bar];
                var approach = plan.StopLines.Approach[bar];
                if (junction < 0 || junction >= plan.Junctions.Count || approach.LengthSquared() <= 0f) continue;
                if (arms[junction] < 3) continue;

                approach = Vector2.Normalize(approach);
                var offset = markM - plan.StopLines.CentreM[bar];
                var downM = Vector2.Dot(offset, approach);
                var acrossM = MathF.Abs(Spline.Cross(approach, offset));
                var toJunctionM = Vector2.Dot(plan.Junctions.CentreM[junction] - plan.StopLines.CentreM[bar], approach);

                Assert.False(
                    downM > 0f && downM < toJunctionM && acrossM <= plan.StopLines.SpanM[bar],
                    $"{map}: a dash at {markM} stands {downM:F1} m past the bar on junction {junction}");
            }
        }
    }

    /// <summary>
    /// <b>And it stops at the crossing on an arm that has no bar.</b> A junction the ranking governs carries
    /// no stop bar (TLT-3) and the same metres of turning ground behind its paint, so a rule written round
    /// the bar leaves the throat of every unlit junction in the town dashed up to its own mouth.
    /// </summary>
    /// <remarks>
    /// <b>A junction that admits no fork has no such throat</b> (TER-6): its crossing is a mid-block one on a
    /// road that bends, so the lane line behind the paint runs on to the disc like any other stretch of
    /// carriageway — which is the same reading as <see cref="NoDashIsLaidBetweenAStopBarAndItsJunction"/>.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NoDashIsLaidBetweenACrossingAndTheJunctionItApproaches(string map)
    {
        var plan = Towns.Of(map);
        var arms = RoadCuts.ArmsPerJunction(plan);

        foreach (var markM in Marks(Ground(map)))
        {
            if (OnACrossing(plan, markM) || IsOneOfThePlansBars(plan, markM)) continue;

            for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
            {
                var junction = plan.Crosswalks.Junction[crossing];
                var axis = plan.Crosswalks.Axis[crossing];
                if (junction < 0 || junction >= plan.Junctions.Count || axis.LengthSquared() <= 0f) continue;
                if (arms[junction] < 3) continue;

                // A crossing's axis runs out of the junction it approaches, so the ground in question is
                // the other way: from the near edge of the paint to the junction's own centre.
                var inward = -Vector2.Normalize(axis);
                var offset = markM - plan.Crosswalks.CentreM[crossing];
                var downM = Vector2.Dot(offset, inward);
                var acrossM = MathF.Abs(Spline.Cross(inward, offset));
                var toJunctionM = Vector2.Dot(
                    plan.Junctions.CentreM[junction] - plan.Crosswalks.CentreM[crossing], inward);

                Assert.False(
                    downM > plan.Crosswalks.DepthM[crossing] * 0.5f && downM < toJunctionM
                    && acrossM <= plan.CrossingSpanM(crossing) * 0.5f,
                    $"{map}: a dash at {markM} stands {downM:F1} m behind the crossing on junction {junction}");
            }
        }
    }

    static bool OnACrossing(CityPlan plan, Vector2 pointM)
    {
        for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
        {
            var along = Vector2.Normalize(plan.Crosswalks.Axis[crossing]);
            var offset = pointM - plan.Crosswalks.CentreM[crossing];
            var down = MathF.Abs(Vector2.Dot(offset, along));
            var across = MathF.Abs((offset.X * -along.Y) + (offset.Y * along.X));
            if (down <= plan.Crosswalks.DepthM[crossing] * 0.5f && across <= plan.CrossingSpanM(crossing) * 0.5f) return true;
        }

        return false;
    }

    static bool IsOneOfThePlansBars(CityPlan plan, Vector2 pointM)
    {
        for (var bar = 0; bar < plan.StopLines.Count; bar++)
        {
            if (Vector2.DistanceSquared(pointM, plan.StopLines.CentreM[bar]) < 1e-4f) return true;
        }

        return false;
    }

    /// <summary>
    /// A zebra is a set of parallel bars, evenly spaced, all the same width, spanning the whole
    /// carriageway and <b>centred on it</b> — so it never begins with half a bar at one kerb.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryCrossingIsStripedRightAcrossItsOwnSpan(string map)
    {
        var plan = Towns.Of(map);
        var config = SimConfig.Shipped();
        var marks = Marks(Ground(map));

        for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
        {
            var centreM = plan.Crosswalks.CentreM[crossing];
            var along = Vector2.Normalize(plan.Crosswalks.Axis[crossing]);
            var spanM = plan.CrossingSpanM(crossing);

            var offsets = new List<float>();
            foreach (var markM in marks)
            {
                var offset = markM - centreM;
                if (MathF.Abs(Vector2.Dot(offset, along)) > plan.Crosswalks.DepthM[crossing] * 0.5f) continue;

                var across = (offset.X * -along.Y) + (offset.Y * along.X);
                if (MathF.Abs(across) <= spanM * 0.5f) offsets.Add(across);
            }

            offsets.Sort();
            Assert.True(offsets.Count >= 2, $"{map}: crossing {crossing} is striped with {offsets.Count} bars");

            // Evenly spaced at the catalogue's pitch, and centred: the two end bars stand the same
            // distance from the two kerbs, and what they leave uncovered is under one whole pitch.
            for (var bar = 1; bar < offsets.Count; bar++)
            {
                Assert.Equal(config.Road.ZebraStripePitchM, offsets[bar] - offsets[bar - 1], 3);
            }

            Assert.Equal(0f, offsets[0] + offsets[^1], 3);
            Assert.True(spanM - (offsets[^1] - offsets[0]) - config.Road.ZebraStripeWidthM < config.Road.ZebraStripePitchM,
                $"{map}: crossing {crossing} is {spanM:F2} m across and its bars cover {offsets[^1] - offsets[0]:F2} m");
        }
    }

    /// <summary>
    /// Every bay is drawn on three sides — <b>its mouth is open</b>, so a row of them leaves no line
    /// between the lot and the road for a car entering it to drive across — and <b>the line two bays
    /// share is painted once</b>: paint is the tarmac drawn brighter through a multiplying tint, so a
    /// stroke laid twice reads brighter than its neighbours and a lot drawn bay by bay would show it
    /// down every interior line.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryBayIsDrawnOnThreeSidesAndASharedStrokeIsPaintedOnce(string map)
    {
        var plan = Towns.Of(map);
        var config = SimConfig.Shipped();
        var lots = plan.ParkingLots;
        if (lots.SpaceCount == 0) return;

        // <b>A stroke is counted where it lies and not where its middle is.</b> A side stroke reaches
        // past the mouth to the kerb line the lot fronts, so its middle stands off the middle of the
        // edge it is drawn on by however far it was dragged — and a window round that middle finds
        // nothing while the line is plainly there. The three edge middles are a bay apart and every
        // stroke is a line's width across, so what covers one of them covers nothing else.
        var painted = Quads(Ground(map));

        for (var space = 0; space < lots.SpaceCount; space++)
        {
            var centreM = lots.SpacePositionM[space];
            var headingRad = lots.SpaceHeadingRad[space];
            var along = new Vector2(MathF.Cos(headingRad), MathF.Sin(headingRad));
            var across = new Vector2(-along.Y, along.X);

            var halfLengthM = config.ParkingSpaceLengthM * 0.5f;
            var halfWidthM = config.ParkingSpaceWidthM * 0.5f;

            Assert.Equal(1, Strokes(centreM - across * halfWidthM));
            Assert.Equal(1, Strokes(centreM + across * halfWidthM));
            Assert.Equal(1, Strokes(centreM + along * halfLengthM));
            Assert.Equal(0, Strokes(centreM - along * halfLengthM));
        }

        int Strokes(Vector2 edgeM)
        {
            var strokes = 0;
            foreach (var quad in painted)
            {
                if (Covers(quad, edgeM)) strokes++;
            }

            return strokes;
        }
    }


    /// <summary>
    /// <b>A bay's strokes are laid end to end and each corner is painted exactly once.</b> Laid to the
    /// bay's own size instead, each stroke stops on the line the next one is <em>centred</em> on — which
    /// leaves half a stroke of the corner painted twice and half of it not painted at all, a bright
    /// square and a notch, both of them plain at close range.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ABayCornerIsPaintedExactlyOnce(string map)
    {
        var plan = Towns.Of(map);
        var config = SimConfig.Shipped();
        var lots = plan.ParkingLots;
        if (lots.SpaceCount == 0) return;

        var quads = Quads(Ground(map));
        var halfStrokeM = config.Road.PaintLineWidthM * 0.5f;
        var quarterStrokeM = config.Road.PaintLineWidthM * 0.25f;
        var halfLengthM = config.ParkingSpaceLengthM * 0.5f;
        var halfWidthM = config.ParkingSpaceWidthM * 0.5f;

        for (var space = 0; space < lots.SpaceCount; space++)
        {
            var centreM = lots.SpacePositionM[space];
            var headingRad = lots.SpaceHeadingRad[space];
            var along = new Vector2(MathF.Cos(headingRad), MathF.Sin(headingRad));
            var across = new Vector2(-along.Y, along.X);

            // Where the three strokes actually landed — the lot's own edge as often as the bay's size:
            // the corner is asked about from the paint rather than from what it was laid to.
            var headM = Vector2.Dot(Nearest(quads, centreM + along * halfLengthM) - centreM, along);

            foreach (var side in (ReadOnlySpan<float>)[-1f, 1f])
            {
                var sideM = Vector2.Dot(Nearest(quads, centreM + across * (halfWidthM * side)) - centreM, across);

                // A quarter of a stroke either side of the seam the head runs up to, on the head's own
                // centreline: the near one is the ground the two would overlap on and the far one is the
                // ground neither would reach.
                foreach (var pastM in (ReadOnlySpan<float>)[-quarterStrokeM, quarterStrokeM])
                {
                    var atM = centreM + along * headM + across * (sideM - ((halfStrokeM - pastM) * side));
                    var covering = 0;
                    foreach (var quad in quads)
                    {
                        if (Covers(quad, atM)) covering++;
                    }

                    Assert.Equal(1, covering);
                }
            }
        }
    }

    /// <summary>
    /// <b>A car park's paint meets the road's.</b> Every stroke running to the mouth of a lot that fronts
    /// the kerb ends on the carriageway's own edge — the line the kerb line's outer face stands on — and
    /// not on the lot's rectangle, which is a chord of that edge and stands up to its sag inside it. A
    /// stroke ended on the rectangle stops short of the kerb line it turns into, which is a gap of most of
    /// a line's width at the one place a driver is looking.
    /// </summary>
    /// <remarks>
    /// It is asked of the paint and not of what the paint was laid to, and it is bounded on both sides: a
    /// stroke that crossed the kerb line rather than meeting it would be a bay marking laid down the
    /// carriageway.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryStrokeAtTheMouthOfAKerbedLotEndsOnTheCarriagewaysOwnEdge(string map)
    {
        var plan = Towns.Of(map);
        var config = SimConfig.Shipped();
        var lots = plan.ParkingLots;
        if (lots.SpaceCount == 0) return;

        var quads = Quads(Ground(map));
        var strokeM = config.Road.PaintLineWidthM;
        var halfLengthM = config.ParkingSpaceLengthM * 0.5f;
        var halfWidthM = config.ParkingSpaceWidthM * 0.5f;

        foreach (var front in RoadFrontages.Lay(plan, config).All)
        {
            if (!front.FrontsTheKerb) continue;

            var edgeM = plan.Roads.WidthM[front.Road] * 0.5f;
            for (var space = lots.SpaceOffsets[front.Lot]; space < lots.SpaceOffsets[front.Lot + 1]; space++)
            {
                var along = new Vector2(
                    MathF.Cos(lots.SpaceHeadingRad[space]), MathF.Sin(lots.SpaceHeadingRad[space]));
                var across = new Vector2(-along.Y, along.X);
                var mouthM = lots.SpacePositionM[space] - (along * halfLengthM);

                // A bay of a kerbed lot whose own mouth is nowhere near the kerb — a second row facing an
                // aisle — has nothing at that end to meet. Half a bay is the reach the paint is laid by.
                if (OffTheCentrelineM(plan, front, mouthM) - edgeM > halfLengthM) continue;

                foreach (var side in (ReadOnlySpan<float>)[-1f, 1f])
                {
                    var strokeAtM = NearestQuad(quads, mouthM + (across * (halfWidthM * side)));
                    var reachM = float.NegativeInfinity;
                    foreach (var cornerM in strokeAtM)
                    {
                        reachM = MathF.Max(reachM, edgeM - OffTheCentrelineM(plan, front, cornerM));
                    }

                    Assert.InRange(reachM, 0f, strokeM);
                }
            }
        }
    }

    /// <summary>How far off its road's centreline a place stands, measured over the lot's own frontage.</summary>
    static float OffTheCentrelineM(CityPlan plan, in LotFrontage front, Vector2 pointM)
    {
        var arcs = plan.Roads.SegmentsOf(front.Road);
        var at = Spline.SampleAt(arcs, Spline.ProjectM(
            arcs, pointM, (front.MouthFromM + front.MouthToM) * 0.5f, front.MouthToM - front.MouthFromM));

        return MathF.Abs(Vector2.Dot(pointM - at.PositionM, at.Right));
    }

    /// <summary>
    /// TER-3c.4 — every re-entrant corner the ground is solved to have is turned on its arc: the spike
    /// between the two pieces is paved, and the disc the arc is struck about is left as verge.
    /// </summary>
    /// <remarks>
    /// The pair is what makes it a rounding rather than either extreme. Paving nothing leaves the right
    /// angle the corner exists to cut off; paving the whole wedge fills in the verge behind it, and the
    /// arc's own centre stands a radius clear of both edges, so it is verge under any rounding. The spike
    /// is sampled midway between the arc and the apex, which is the deepest point of it and the one place
    /// no edge line's own inset can reach.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryInnerCornerTheGroundHasIsTurnedOnItsArc(string map)
    {
        var pavement = Triangles(Ground(map), Surface.Pavement);

        foreach (var corner in PavementCorners.Solve(Towns.Of(map), SimConfig.Shipped()))
        {
            var arcCentreM = corner.ArcCentreM;
            var deepM = (corner.RadiusM + Vector2.Distance(arcCentreM, corner.CornerM)) * 0.5f;
            var spikeM = arcCentreM + (deepM * Vector2.Normalize(corner.CornerM - arcCentreM));

            Assert.True(Covered(pavement, spikeM), $"{map} leaves the spike at {corner.CornerM} unpaved");
            Assert.False(Covered(pavement, arcCentreM), $"{map} paves the verge behind the corner at {corner.CornerM}");
        }
    }

    /// <summary>
    /// <b>A road is drawn as wide round a bend as down a straight</b> (GEN-15): the carriageway reaches
    /// half the road either side of the road's own line at every point of every curve, so a lane measured
    /// off a picture is the lane the town was laid with wherever it is measured.
    /// </summary>
    /// <remarks>
    /// <b>A hair inside the edge rather than on it</b>, because a ribbon is sampled to a chord bow and the
    /// drawn kerb cuts that much of the corner between two samples. Only the bends are asked about: a
    /// straight ribbon is two triangles and cannot be pinched. The fixture is where they are asked of,
    /// since it turns 9 m of radius — tighter than the floor a generated town lays a bend to.
    /// </remarks>
    [Fact]
    public void EveryBendIsDrawnAsWideAsItsRoad()
    {
        var plan = Towns.Of(Towns.Fixture);
        var tarmac = Triangles(Ground(Towns.Fixture), Surface.Tarmac);
        const float HairM = 0.05f;

        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var halfM = (plan.Roads.WidthM[road] * 0.5f) - HairM;
            foreach (var arc in plan.Roads.SegmentsOf(road))
            {
                if (arc.Curvature == 0f) continue;

                for (var alongM = 0f; alongM <= arc.LengthM; alongM += 0.5f)
                {
                    var headingRad = arc.HeadingAtRad(alongM);
                    var across = new Vector2(-MathF.Sin(headingRad), MathF.Cos(headingRad));
                    var centreM = arc.PointAtM(alongM);

                    foreach (var side in (ReadOnlySpan<float>)[-1f, 1f])
                    {
                        var edgeM = centreM + (across * side * halfM);
                        Assert.True(
                            Covered(tarmac, edgeM),
                            $"road {road} is drawn short of its own kerb at {edgeM}, {alongM:F1} m into a bend");
                    }
                }
            }
        }
    }

    /// <summary>
    /// <b>TER-3d — the kerb line stands on the kerb, so a lane keeps the whole width it was laid at.</b>
    /// The ground a hair inside either edge of every carriageway is the surface as itself: a line struck
    /// inside the road would be painted over exactly that strip, and a lane measured off the picture would
    /// come out a line short of <c>LaneWidthM</c>.
    /// </summary>
    /// <remarks>
    /// The marks are not asked about — a dash straddles the centreline and a zebra covers the lane, both by
    /// design — so only the ground under them is read (<see cref="GroundMesh.FirstMarkVertex"/>).
    /// </remarks>
    [Fact]
    public void NoLaneIsPaintedOverByTheLineThatMarksIt()
    {
        var plan = Towns.Of(Towns.Fixture);
        var mesh = Ground(Towns.Fixture);
        const float HairM = 0.05f;

        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var halfM = (plan.Roads.WidthM[road] * 0.5f) - HairM;
            foreach (var arc in plan.Roads.SegmentsOf(road))
            {
                for (var alongM = 0f; alongM <= arc.LengthM; alongM += 2f)
                {
                    var headingRad = arc.HeadingAtRad(alongM);
                    var across = new Vector2(-MathF.Sin(headingRad), MathF.Cos(headingRad));
                    var centreM = arc.PointAtM(alongM);

                    foreach (var side in (ReadOnlySpan<float>)[-1f, 1f])
                    {
                        var edgeM = centreM + (across * side * halfM);
                        Assert.False(
                            PaintedGround(mesh, edgeM),
                            $"road {road} carries paint on its own lane at {edgeM}, {alongM:F1} m along");
                    }
                }
            }
        }
    }

    /// <summary>
    /// <b>A dash is laid on the road's own curve and not on the chord of it.</b> Its ends stand on the
    /// line however it is drawn — they are what it was laid between — so what says whether it followed the
    /// bend is its <em>middle</em>: a mark struck straight across one stands its own sag off the line
    /// there, and the line reads as a row of tangents.
    /// </summary>
    /// <remarks>
    /// The fixture is where it is asked, since it turns 9 m of radius — tighter than the floor a generated
    /// town lays a bend to; there a whole dash struck straight bows 5 cm, and a piece of one drawn to the
    /// ground's own chord tolerance under 2 cm. A mark is taken for a dash by where it stands rather than
    /// by how it was laid — on a road's own line, over a stretch that road is dashed over — and the window
    /// that finds one is a whole line's width, wide enough to catch a dash that missed the line as well as
    /// one that kept it. Bars stand a half lane off the line and a zebra's stripes cross it only where
    /// nothing is dashed.
    /// </remarks>
    [Fact]
    public void EveryDashIsLaidOnItsRoadsOwnCurve()
    {
        var plan = Towns.Of(Towns.Fixture);
        var config = SimConfig.Shipped();
        var runs = CentrelineRuns.Lay(plan, config);
        var lineM = config.Road.PaintLineWidthM;
        const float SagM = 0.025f;

        var dashes = 0;
        foreach (var quad in Quads(Ground(Towns.Fixture)))
        {
            var centreM = (quad[0] + quad[1] + quad[2] + quad[3]) * 0.25f;
            for (var road = 0; road < plan.Roads.Count; road++)
            {
                var arcs = plan.Roads.SegmentsOf(road);
                var offM = OffADashedStretchM(arcs, runs.On(road), centreM);
                if (offM > lineM) continue;

                dashes++;
                Assert.True(offM <= SagM, $"a dash on road {road} bows {offM:F3} m off the line it marks");
            }
        }

        Assert.True(dashes > 0, $"{Towns.Fixture} lays no dash on any of its roads");
    }

    /// <summary>
    /// How far a place stands off a road's line over a stretch that road is dashed over, or infinity where
    /// the nearest point of the line to it carries no dashes.
    /// </summary>
    static float OffADashedStretchM(
        ReadOnlySpan<ArcSeg> arcs, ReadOnlySpan<RoadStretch> stretches, Vector2 pointM)
    {
        var lengthM = Spline.TotalLengthM(arcs);
        var alongM = Spline.ProjectM(arcs, pointM, lengthM * 0.5f, lengthM);
        foreach (var stretch in stretches)
        {
            if (alongM >= stretch.FromM && alongM <= stretch.ToM)
            {
                return Vector2.Distance(Spline.SampleAt(arcs, alongM).PositionM, pointM);
            }
        }

        return float.PositiveInfinity;
    }

    /// <summary>
    /// Whether the ground at a place was painted rather than drawn as itself — the ground alone, since
    /// the marks laid over it are paint by definition. The last piece laid over a place is the one that
    /// shows, so it is the last that answers.
    /// </summary>
    static bool PaintedGround(GroundMesh mesh, Vector2 pointM)
    {
        var vertices = mesh.Vertices;
        var painted = false;
        for (var index = 0; index + 2 < mesh.Indices.Length; index += 3)
        {
            var first = (int)mesh.Indices[index];
            if (first >= mesh.FirstMarkVertex) break;

            Vector2[] triangle =
            [
                vertices[first].PositionM, vertices[(int)mesh.Indices[index + 1]].PositionM,
                vertices[(int)mesh.Indices[index + 2]].PositionM,
            ];

            if (Covered([triangle], pointM)) painted = vertices[first].Tint.X > 1f;
        }

        return painted;
    }

    /// <summary>Every triangle of one surface, as three corners each.</summary>
    static List<Vector2[]> Triangles(GroundMesh mesh, Surface surface)
    {
        var vertices = mesh.Vertices;
        var triangles = new List<Vector2[]>();
        for (var index = 0; index + 2 < mesh.Indices.Length; index += 3)
        {
            var first = (int)mesh.Indices[index];
            if (vertices[first].Surface != surface) continue;

            triangles.Add(
            [
                vertices[first].PositionM, vertices[(int)mesh.Indices[index + 1]].PositionM,
                vertices[(int)mesh.Indices[index + 2]].PositionM,
            ]);
        }

        return triangles;
    }

    /// <summary>
    /// Whether any of the triangles holds a point, counting a point on an edge as held.
    /// </summary>
    /// <remarks>
    /// Not <see cref="Covers"/>, which breaks a tie one way round: a fan's spokes all radiate from one
    /// vertex, so a point sampled along a bisector lands exactly on one of them and is inside both the
    /// triangles either side of it.
    /// </remarks>
    static bool Covered(List<Vector2[]> triangles, Vector2 pointM)
    {
        foreach (var triangle in triangles)
        {
            var left = true;
            var right = true;
            for (var corner = 0; corner < 3; corner++)
            {
                var edge = triangle[(corner + 1) % 3] - triangle[corner];
                var reach = pointM - triangle[corner];
                var turn = (edge.X * reach.Y) - (edge.Y * reach.X);
                left &= turn >= 0f;
                right &= turn <= 0f;
            }

            if (left || right) return true;
        }

        return false;
    }

    /// <summary>
    /// Where each painted mark stands: one point per quad of four corners, from the vertex the mesh
    /// says its marks begin at — the kerb line is paint too, so brightness alone no longer says what
    /// was painted <em>on</em> the carriageway rather than <em>at the edge of</em> it.
    /// </summary>
    static List<Vector2> Marks(GroundMesh mesh)
    {
        var vertices = mesh.Vertices;
        var marks = new List<Vector2>((vertices.Length - mesh.FirstMarkVertex) / 4);
        for (var corner = mesh.FirstMarkVertex; corner + 3 < vertices.Length; corner += 4)
        {
            marks.Add((vertices[corner].PositionM + vertices[corner + 1].PositionM +
                       vertices[corner + 2].PositionM + vertices[corner + 3].PositionM) * 0.25f);
        }

        return marks;
    }

    /// <summary>The same marks as four corners each, in the order they were wound.</summary>
    static List<Vector2[]> Quads(GroundMesh mesh)
    {
        var vertices = mesh.Vertices;
        var quads = new List<Vector2[]>((vertices.Length - mesh.FirstMarkVertex) / 4);
        for (var corner = mesh.FirstMarkVertex; corner + 3 < vertices.Length; corner += 4)
        {
            quads.Add(
            [
                vertices[corner].PositionM, vertices[corner + 1].PositionM,
                vertices[corner + 2].PositionM, vertices[corner + 3].PositionM,
            ]);
        }

        return quads;
    }

    /// <summary>The centre of whichever mark stands nearest a place.</summary>
    static Vector2 Nearest(List<Vector2[]> quads, Vector2 toM)
    {
        var quad = NearestQuad(quads, toM);

        return quad.Length == 0 ? Vector2.Zero : (quad[0] + quad[1] + quad[2] + quad[3]) * 0.25f;
    }

    /// <summary>The four corners of whichever mark stands nearest a place.</summary>
    static Vector2[] NearestQuad(List<Vector2[]> quads, Vector2 toM)
    {
        var nearest = Array.Empty<Vector2>();
        var awayM = float.PositiveInfinity;
        foreach (var quad in quads)
        {
            var centreM = (quad[0] + quad[1] + quad[2] + quad[3]) * 0.25f;
            if ((centreM - toM).LengthSquared() >= awayM) continue;

            awayM = (centreM - toM).LengthSquared();
            nearest = quad;
        }

        return nearest;
    }

    /// <summary>
    /// Whether a convex quad holds a point: every edge turns the same way to it, whichever way round the
    /// quad itself is wound. <b>A millimetre of slack</b>, because a stroke laid to end exactly on a face
    /// somebody else measures from is a point a rounding decides either way.
    /// </summary>
    static bool Covers(Vector2[] quad, Vector2 pointM)
    {
        const float slackM = 0.001f;
        var wound = 0f;
        for (var corner = 0; corner < quad.Length; corner++)
        {
            var edge = quad[(corner + 1) % quad.Length] - quad[corner];
            var reach = quad[(corner + 2) % quad.Length] - quad[corner];
            wound += (edge.X * reach.Y) - (edge.Y * reach.X);
        }

        var sign = wound < 0f ? -1f : 1f;
        for (var corner = 0; corner < quad.Length; corner++)
        {
            var edge = quad[(corner + 1) % quad.Length] - quad[corner];
            var reach = pointM - quad[corner];
            if (sign * ((edge.X * reach.Y) - (edge.Y * reach.X)) < -slackM * edge.Length()) return false;
        }

        return true;
    }

    /// <summary>A town is not laid at the cost of a tick: this is load-time work, done once.</summary>
    [Fact]
    public void TheLargestTownsGroundIsLaidOnceAndIsNotEnormous()
    {
        var mesh = Ground("Odesa");

        // One indexed draw over the whole city, and the whole of it fits in a couple of megabytes:
        // the point of laying ground from shapes rather than from a three-million-cell grid.
        Assert.InRange(mesh.Vertices.Length, 1_000, 500_000);
        Assert.True(mesh.Indices.Length > Ground(Towns.Fixture).Indices.Length,
            "the city lays no more ground than the fixture map");
    }
}
