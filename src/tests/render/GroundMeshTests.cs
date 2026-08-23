using System.Numerics;
using TrafficSimulation.App.Render;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
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

    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryShippedMapLaysGroundThatIsWellFormed(string map)
    {
        var mesh = GroundMesh.Build(Towns.Of(map), SimConfig.Shipped());

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
        var mesh = GroundMesh.Build(Towns.Of(map), config);

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
        var mesh = GroundMesh.Build(plan, SimConfig.Shipped());

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
        var mesh = GroundMesh.Build(plan, SimConfig.Shipped());

        var owed = 0;
        for (var outline = 0; outline < plan.Water.Count; outline++) owed += plan.Water.OutlineOf(outline).Length - 2;

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
        var mesh = GroundMesh.Build(plan, SimConfig.Shipped());

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
    [Theory]
    [MemberData(nameof(Maps))]
    public void NoDashIsLaidInAJunctionOrOnACrossing(string map)
    {
        var plan = Towns.Of(map);

        foreach (var markM in Marks(GroundMesh.Build(plan, SimConfig.Shipped())))
        {
            if (OnACrossing(plan, markM) || IsOneOfThePlansBars(plan, markM)) continue;

            for (var junction = 0; junction < plan.Junctions.Count; junction++)
            {
                var reachM = plan.Junctions.RadiusM[junction];
                Assert.True(Vector2.DistanceSquared(markM, plan.Junctions.CentreM[junction]) > reachM * reachM,
                    $"{map}: a dash at {markM} is laid inside junction {junction}");
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
            if (down <= plan.Crosswalks.DepthM[crossing] * 0.5f && across <= plan.Crosswalks.SpanM[crossing] * 0.5f) return true;
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
        var marks = Marks(GroundMesh.Build(plan, config));

        for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
        {
            var centreM = plan.Crosswalks.CentreM[crossing];
            var along = Vector2.Normalize(plan.Crosswalks.Axis[crossing]);
            var spanM = plan.Crosswalks.SpanM[crossing];

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
    /// Every bay is outlined, and <b>the line two bays share is painted once</b>: paint is the tarmac
    /// drawn brighter through a multiplying tint, so a stroke laid twice reads brighter than its
    /// neighbours and a lot drawn bay by bay would show it down every interior line.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryBayIsOutlinedAndASharedStrokeIsPaintedOnce(string map)
    {
        var plan = Towns.Of(map);
        var config = SimConfig.Shipped();
        var lots = plan.ParkingLots;
        if (lots.SpaceCount == 0) return;

        // Half-metre cells: the nearest two strokes that are genuinely different are a bay apart, so a
        // window of one cell either way finds the one stroke an edge is meant to carry and nothing else.
        var painted = new Dictionary<(int X, int Y), int>();
        foreach (var markM in Marks(GroundMesh.Build(plan, config)))
        {
            var at = ((int)MathF.Round(markM.X * 2f), (int)MathF.Round(markM.Y * 2f));
            painted[at] = painted.GetValueOrDefault(at) + 1;
        }

        for (var space = 0; space < lots.SpaceCount; space++)
        {
            var centreM = lots.SpacePositionM[space];
            var headingRad = lots.SpaceHeadingRad[space];
            var along = new Vector2(MathF.Cos(headingRad), MathF.Sin(headingRad));
            var across = new Vector2(-along.Y, along.X);

            foreach (var side in (ReadOnlySpan<float>)[-1f, 1f])
            {
                foreach (var edgeM in (ReadOnlySpan<Vector2>)
                    [
                        centreM + across * (config.ParkingSpaceWidthM * 0.5f * side),
                        centreM + along * (config.ParkingSpaceLengthM * 0.5f * side),
                    ])
                {
                    var strokes = 0;
                    for (var x = -1; x <= 1; x++)
                    {
                        for (var y = -1; y <= 1; y++)
                        {
                            strokes += painted.GetValueOrDefault(
                                ((int)MathF.Round(edgeM.X * 2f) + x, (int)MathF.Round(edgeM.Y * 2f) + y));
                        }
                    }

                    Assert.Equal(1, strokes);
                }
            }
        }
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

    /// <summary>A town is not laid at the cost of a tick: this is load-time work, done once.</summary>
    [Fact]
    public void TheLargestTownsGroundIsLaidOnceAndIsNotEnormous()
    {
        var mesh = GroundMesh.Build(Towns.Of("Odesa"), SimConfig.Shipped());

        // One indexed draw over the whole city, and the whole of it fits in a couple of megabytes:
        // the point of laying ground from shapes rather than from a three-million-cell grid.
        Assert.InRange(mesh.Vertices.Length, 1_000, 500_000);
        Assert.True(mesh.Indices.Length > GroundMesh.Build(Towns.Of(Towns.Fixture), SimConfig.Shipped()).Indices.Length,
            "the city lays no more ground than the fixture map");
    }
}
