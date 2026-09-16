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
/// The ground's triangles, asked of every map this build lays without a GPU in the room. What a picture can
/// only be judged on — a dashed line's pitch, whether the paint sits on the road — is the render and
/// agent tiers' job; what is asserted here is everything about the mesh that <em>is</em> a fact.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P5)]
public class GroundMeshTests
{
    public static TheoryData<string> Maps => Towns.EveryLaidMap();

    static readonly ConcurrentDictionary<string, GroundMesh> Laid = new();

    /// <summary>
    /// <b>One map's ground, laid once and read by every claim about it.</b> The mesh is a function of the
    /// plan and the figures and nothing here writes to it, so eleven claims over eight maps were eighty-eight
    /// triangulations of the same eight towns.
    /// </summary>
    static GroundMesh Ground(string map) => Laid.GetOrAdd(map, at => GroundMesh.Build(Towns.Of(at), SimConfig.Shipped()));

    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryLaidMapLaysGroundThatIsWellFormed(string map)
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
    /// <b>The parts tile the mesh</b> (OBS-2v): each layer's triangles start where the layer before it
    /// ended, and the last of them ends at the last triangle laid. A part is switched off by leaving its
    /// run out of the draw, so a stretch belonging to no part is ground no switch can reach and one
    /// overlapping its neighbour is a switch that takes the neighbour's ground with it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ThePartsOfTheGroundTileIt(string map)
    {
        var mesh = Ground(map);

        var laid = 0;
        for (var part = 0; part < GroundParts.Count; part++)
        {
            Assert.Equal(laid, mesh.Parts[part].FirstIndex);
            laid += mesh.Parts[part].IndexCount;
        }

        Assert.Equal(mesh.Indices.Length, laid);
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
    /// <b>The ground is laid bottom to top, and each kerb over the fill it bounds</b> (TER-7b,
    /// <c>GroundRings</c>): the walk, the kerb along its outer face, the carriageway, and last the town's own
    /// kerb. <b>A fill laid after the kerb beside it eats into that kerb</b> by as much as its own thinning
    /// reached, which is the whole reason a kerb is a stroke here rather than a band — and laid the other way
    /// round altogether, the carriageway goes under the concrete and the town is one slab of pavement.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Read off the shades and not off the surfaces</b>, the walk and both kerbs wearing the same one:
    /// what tells a kerb from the walk it bounds is the tint, which is the whole of how a line beside a road
    /// is drawn here.
    /// </para>
    /// <para>
    /// <b>The walk's own shade is the shore's too</b>, the shore being pavement drawn plain, so the first
    /// triangle wearing it is the walk's and the last is whichever of the two was laid later. That is why the
    /// walk is read by where it starts. <b>And the last tarmac is a slab rather than the carriageway</b>, to
    /// the same end: what the town's kerb is asked to be over is every piece of tarmac the town lays.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void TheGroundIsLaidBottomToTopAndEachKerbOverTheFillItBounds(string map)
    {
        var mesh = Ground(map);

        var walkFirst = int.MaxValue;
        var walkKerbFirst = int.MaxValue;
        var walkKerbLast = -1;
        var kerbFirst = int.MaxValue;
        var tarmacFirst = int.MaxValue;
        var tarmacLast = -1;
        for (var index = 0; index + 2 < mesh.Indices.Length; index += 3)
        {
            var vertex = mesh.Vertices[(int)mesh.Indices[index]];
            if (vertex.Surface == Surface.Tarmac)
            {
                tarmacFirst = Math.Min(tarmacFirst, index);
                tarmacLast = index;
            }

            if (vertex.Surface != Surface.Pavement) continue;

            if (vertex.Tint == GroundMesh.Edge)
            {
                walkKerbFirst = Math.Min(walkKerbFirst, index);
                walkKerbLast = index;
            }
            else if (vertex.Tint == GroundMesh.Kerb)
            {
                kerbFirst = Math.Min(kerbFirst, index);
            }
            else if (vertex.Tint == Vector3.One)
            {
                walkFirst = Math.Min(walkFirst, index);
            }
        }

        Assert.True(
            walkKerbLast >= 0 && walkFirst < int.MaxValue && kerbFirst < int.MaxValue && tarmacLast >= 0,
            $"{map} is missing one of the four: walk {walkFirst}, walk's kerb {walkKerbFirst}, "
            + $"tarmac {tarmacFirst}, town's kerb {kerbFirst}");

        Assert.True(walkFirst < walkKerbFirst, $"{map} lays the walk over its own kerb");
        Assert.True(walkKerbLast < tarmacFirst, $"{map} lays the carriageway under the pavement");
        Assert.True(tarmacLast < kerbFirst, $"{map} lays tarmac over the town's kerb");
    }

    /// <summary>
    /// <b>The town's kerb covers the driven ground's boundary for the whole of its length</b> (TER-3d): the
    /// ground a quarter of a kerb <em>either</em> side of that boundary wears the kerb, everywhere the
    /// boundary runs. This is the claim the line exists to make good — struck about the boundary at a
    /// constant width and laid after both fills, so neither the walk's thinning nor the carriageway's can eat
    /// into it and there is no strip of anything else between the two.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void TheTownsKerbCoversTheWholeOfTheDrivenGroundsBoundary(string map)
    {
        var config = SimConfig.Shipped();
        KerbCovers(
            map, Ground(map), Drawn(Towns.Of(map).Paving(config).Rings(config).Carriageway.Rings),
            GroundMesh.Kerb, config.Road.KerbWidthM, "the boundary");
    }

    /// <summary>
    /// <b>The walk's own kerb covers the pavement's outer face for the whole of its length</b> (TER-3c.3):
    /// the same claim about the other of the two lines the town carries beside a road, struck off the walk's
    /// own offset rather than off the carriageway's boundary.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void TheWalksOwnKerbCoversTheWholeOfItsOuterFace(string map)
    {
        var config = SimConfig.Shipped();
        KerbCovers(
            map, Ground(map), Drawn(Towns.Of(map).Paving(config).Rings(config).WalkEdge),
            GroundMesh.Edge, config.Road.KerbWidthM, "the walk's outer face");
    }

    /// <summary>
    /// <b>The line a layer's kerb is struck from</b> (<c>GroundMesh.Line</c>), which is what every claim
    /// about a kerb is made against — asked of the mesh's own method, not cut again here.
    /// </summary>
    /// <remarks>
    /// <b>And not the arcs the merge handed over.</b> A kerb is struck from this line, so a kerb asked to
    /// straddle the arcs instead is asked to straddle something no triangle in the mesh stands on.
    /// </remarks>
    static Vector2[][] Drawn(ReadOnlySpan<ArcSeg[]> rings) => GroundMesh.Line(rings);

    /// <summary>
    /// <b>Neither fill's edge shows from under the kerb along it</b> (TER-3d): the whole of the boundary the
    /// carriageway and the walk are actually cut to wears kerb, everywhere it runs.
    /// </summary>
    /// <remarks>
    /// <b>This is what lets a fill be cut coarsely at all.</b> A fill is the same line thinned by a share of
    /// what the kerb hides (<c>GroundMesh.HiddenShare</c>), so where it cuts a corner the layer beneath
    /// shows through and the kerb is laid over both — and a fill reaching out from under that stone is the
    /// one way the thinning can be seen. <b>The corners are not the question and the chords are</b>: every
    /// corner of a fill is a corner of the line by construction, and what parts from the line is the
    /// straight between two of them.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NeitherFillsEdgeShowsFromUnderTheKerbAlongIt(string map)
    {
        var config = SimConfig.Shipped();
        var rings = Towns.Of(map).Paving(config).Rings(config);
        var kerbM = config.Road.KerbWidthM;

        FillHides(map, Ground(map), GroundMesh.Filled(Drawn(rings.Carriageway.Rings), kerbM),
            GroundMesh.Kerb, "the carriageway");
        FillHides(map, Ground(map), GroundMesh.Filled(Drawn(rings.WalkEdge), kerbM),
            GroundMesh.Edge, "the walk");
    }

    /// <summary>One fill's own boundary walked, with every place along it asked whether it wears kerb.</summary>
    static void FillHides(string map, GroundMesh mesh, Vector2[][] outline, Vector3 tint, string named)
    {
        var kerb = Binned(Tinted(mesh, tint));

        var sampled = 0;
        var bare = new List<Vector2>();
        foreach (var ring in outline)
        {
            for (var at = 0; at < ring.Length; at++)
            {
                var fromM = ring[at];
                var ontoM = ring[(at + 1) % ring.Length];
                var steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(fromM, ontoM) / SamplePitchM));
                for (var step = 0; step <= steps; step++)
                {
                    var pointM = Vector2.Lerp(fromM, ontoM, (float)step / steps);
                    sampled++;
                    if (!Covered(kerb.GetValueOrDefault(Cell(pointM), []), pointM) && bare.Count < Named)
                    {
                        bare.Add(pointM);
                    }
                }
            }
        }

        Assert.True(
            bare.Count == 0,
            $"{map}: {bare.Count} of {sampled} places on {named}'s own edge wear no kerb, "
            + $"the first at {string.Join("; ", bare.Select(at => $"{at.X:F1}, {at.Y:F1}"))}");
    }

    /// <summary>
    /// <b>And no corner of either kerb stands further off the line it was struck from than half a kerb</b>
    /// (TER-3d): the line runs down the middle of the stroke, so the whole of it lies within half its own
    /// width of that line — on a bend, at a corner, and at the tightest hook the boundary has.
    /// </summary>
    /// <remarks>
    /// <b>The claim <see cref="KerbCovers"/> cannot make.</b> That one walks the line and asks what covers a
    /// place, so it sees a stroke that falls short and never one that reaches too far; a kerb bulging off its
    /// own line covers everything it is asked about and is wrong anyway.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NeitherKerbStandsFurtherOffItsLineThanHalfItsWidth(string map)
    {
        var config = SimConfig.Shipped();
        var rings = Towns.Of(map).Paving(config).Rings(config);
        var halfM = config.Road.KerbWidthM * 0.5f;

        KerbKeepsTo(map, Ground(map), Drawn(rings.Carriageway.Rings), GroundMesh.Kerb, halfM, "the boundary");
        KerbKeepsTo(map, Ground(map), Drawn(rings.WalkEdge), GroundMesh.Edge, halfM,
            "the walk's outer face", Surface.Pavement);
    }

    /// <summary>
    /// Every corner of one stroke measured against the nearest stretch of the line it was struck from,
    /// exactly and not against a sampling of it.
    /// </summary>
    /// <remarks>
    /// <b>The cells around it and not the one it is in.</b> A stretch is filed by the cells its own box
    /// reaches, and a corner half a width off it stands in a cell that box may just fail to touch.
    /// </remarks>
    static void KerbKeepsTo(
        string map, GroundMesh mesh, Vector2[][] lines, Vector3 tint, float halfM, string named,
        Surface? surface = null)
    {
        var stretches = new Dictionary<(int X, int Y), List<(Vector2 FromM, Vector2 OntoM)>>();
        foreach (var line in lines)
        {
            for (var at = 0; at < line.Length; at++)
            {
                var stretch = (FromM: line[at], OntoM: line[(at + 1) % line.Length]);
                var (fromX, fromY) = Cell(Vector2.Min(stretch.FromM, stretch.OntoM));
                var (toX, toY) = Cell(Vector2.Max(stretch.FromM, stretch.OntoM));
                for (var y = fromY; y <= toY; y++)
                {
                    for (var x = fromX; x <= toX; x++)
                    {
                        if (!stretches.TryGetValue((x, y), out var held)) stretches[(x, y)] = held = [];
                        held.Add(stretch);
                    }
                }
            }
        }

        var strayed = new List<(Vector2 AtM, float OffM)>();
        foreach (var triangle in Tinted(mesh, tint, surface))
        {
            foreach (var cornerM in triangle)
            {
                var offM = float.MaxValue;
                var (x, y) = Cell(cornerM);
                for (var downY = -1; downY <= 1; downY++)
                {
                    for (var acrossX = -1; acrossX <= 1; acrossX++)
                    {
                        foreach (var (fromM, ontoM) in stretches.GetValueOrDefault((x + acrossX, y + downY), []))
                        {
                            offM = MathF.Min(offM, OffStretchM(fromM, ontoM, cornerM));
                        }
                    }
                }

                if (offM > halfM + Rounding && strayed.Count < Named) strayed.Add((cornerM, offM));
            }
        }

        Assert.True(
            strayed.Count == 0,
            $"{map}: {strayed.Count} corners of the kerb stand further than {halfM:F2} m off {named}, "
            + $"the first at {string.Join("; ", strayed.Select(at => $"{at.AtM.X:F1}, {at.AtM.Y:F1} by {at.OffM:F3} m"))}");
    }

    /// <summary>
    /// What a corner may stand past the width and still be that width: two millimetres, which is the weld
    /// (<c>GroundMesh.OnePointM</c>) and the arithmetic that put the corner there.
    /// </summary>
    const float Rounding = 0.002f;

    /// <summary>
    /// One line walked, with the places a quarter of a kerb either side of every sample asked what they wear
    /// — <b>at the corners it turns as much as along the pieces it turns them between</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A quarter of a kerb off and not a whisker off.</b> A sample taken against the line itself is a
    /// sample on the edge of two shapes, which answers whichever way the arithmetic rounds, and one at half a
    /// kerb is on the edge of the stroke for the same reason. Taken at the middle of the half claimed, it
    /// answers the question asked. <b>Both halves</b>, because the line runs down the middle of the stroke
    /// (TER-3d) and a claim made about one side of it says nothing about where the stroke actually stands.
    /// </para>
    /// <para>
    /// <b>And off the bisector at every corner</b> (<c>GroundMesh.Stroke</c>), because a corner is where a
    /// stroke fails and nowhere along a stretch is. What is claimed at a corner is the sector the
    /// cross-section sweeps through it, and a stroke that crosses one short of the middle of the turn leaves
    /// every corner in the town bare — which a walk down the stretches alone passes either side of.
    /// </para>
    /// </remarks>
    static void KerbCovers(
        string map, GroundMesh mesh, Vector2[][] lines, Vector3 tint, float kerbM, string named)
    {
        var kerb = Binned(Tinted(mesh, tint));
        var offM = kerbM * 0.25f;

        var sampled = 0;
        var bare = new List<Vector2>();
        void Ask(Vector2 atM, Vector2 across)
        {
            foreach (var sideM in (ReadOnlySpan<float>)[-offM, offM])
            {
                var pointM = atM + (across * sideM);
                sampled++;
                if (!Covered(kerb.GetValueOrDefault(Cell(pointM), []), pointM) && bare.Count < Named)
                {
                    bare.Add(pointM);
                }
            }
        }

        foreach (var line in lines)
        {
            for (var at = 0; at < line.Length; at++)
            {
                var fromM = line[at];
                var ontoM = line[(at + 1) % line.Length];
                var stretchM = Vector2.Distance(fromM, ontoM);
                if (stretchM <= 0f) continue;

                var across = Heading.RightOf((ontoM - fromM) / stretchM);
                var steps = Math.Max(1, (int)MathF.Ceiling(stretchM / SamplePitchM));
                for (var step = 0; step < steps; step++)
                {
                    Ask(Vector2.Lerp(fromM, ontoM, (step + 0.5f) / steps), across);
                }

                // A cusp has no bisector — the cross-section turns right round — so there is no one direction
                // off it that the turn is the middle of.
                var beyondM = line[(at + 2) % line.Length];
                if (beyondM == ontoM) continue;

                var bisector = across + Heading.RightOf(Vector2.Normalize(beyondM - ontoM));
                if (bisector.LengthSquared() < Cusp) continue;

                Ask(ontoM, Vector2.Normalize(bisector));
            }
        }

        Assert.True(
            bare.Count == 0,
            $"{map}: {bare.Count} of {sampled} places {offM:F2} m off {named} wear no kerb, "
            + $"the first at {string.Join("; ", bare.Select(at => $"{at.X:F1}, {at.Y:F1}"))}");
    }

    /// <summary>How far apart a line is sampled when it is walked: a car's length, which is a mouth.</summary>
    const float SamplePitchM = 4f;

    /// <summary>How far a point stands off one stretch of a line, clamped to that stretch's own two ends.</summary>
    static float OffStretchM(Vector2 fromM, Vector2 ontoM, Vector2 pointM)
    {
        var stepM = ontoM - fromM;
        var lengthM2 = stepM.LengthSquared();
        if (lengthM2 <= 0f) return Vector2.Distance(fromM, pointM);

        var along = Math.Clamp(Vector2.Dot(pointM - fromM, stepM) / lengthM2, 0f, 1f);
        return Vector2.Distance(fromM + (stepM * along), pointM);
    }

    /// <summary>
    /// How short the sum of two outward normals is before the corner between them is a cusp and has no
    /// bisector: a hundredth, which is half a degree off turning right round.
    /// </summary>
    const float Cusp = 0.01f;

    /// <summary>How many failures are written out before a count stands for the rest.</summary>
    const int Named = 8;

    /// <summary>
    /// How wide a cell of <see cref="Binned"/> is. A kerb triangle is a fifth of a metre across and under a
    /// metre long, so at this size each falls in one cell or two and a place is answered by a handful.
    /// </summary>
    const float CellM = 4f;

    static (int X, int Y) Cell(Vector2 pointM) =>
        ((int)MathF.Floor(pointM.X / CellM), (int)MathF.Floor(pointM.Y / CellM));

    /// <summary>
    /// Triangles filed by the cells their boxes reach, so asking what covers a place is a question about a
    /// handful of them rather than about every one the town laid.
    /// </summary>
    static Dictionary<(int X, int Y), List<Vector2[]>> Binned(List<Vector2[]> triangles)
    {
        var bins = new Dictionary<(int X, int Y), List<Vector2[]>>();
        foreach (var triangle in triangles)
        {
            var leastM = Vector2.Min(Vector2.Min(triangle[0], triangle[1]), triangle[2]);
            var mostM = Vector2.Max(Vector2.Max(triangle[0], triangle[1]), triangle[2]);
            var (fromX, fromY) = Cell(leastM);
            var (toX, toY) = Cell(mostM);
            for (var y = fromY; y <= toY; y++)
            {
                for (var x = fromX; x <= toX; x++)
                {
                    if (!bins.TryGetValue((x, y), out var held)) bins[(x, y)] = held = [];
                    held.Add(triangle);
                }
            }
        }

        return bins;
    }

    /// <summary>
    /// Every triangle of the ground wearing one shade, as three corners each — and of one surface where the
    /// caller names it, <b>a shade being shared by the two things drawn in it</b>: the walk's own kerb and a
    /// deck's rim are both the surface darkened (<c>GroundMesh.Edge</c>), on the pavement and on the deck.
    /// </summary>
    static List<Vector2[]> Tinted(GroundMesh mesh, Vector3 tint, Surface? surface = null)
    {
        var vertices = mesh.Vertices;
        var triangles = new List<Vector2[]>();
        for (var index = 0; index + 2 < mesh.Indices.Length; index += 3)
        {
            var first = (int)mesh.Indices[index];
            if (vertices[first].Tint != tint) continue;
            if (surface is not null && vertices[first].Surface != surface) continue;

            triangles.Add(
            [
                vertices[first].PositionM, vertices[(int)mesh.Indices[index + 1]].PositionM,
                vertices[(int)mesh.Indices[index + 2]].PositionM,
            ]);
        }

        return triangles;
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
    /// <summary>
    /// <b>The surface the last triangle covering a point wears</b>, which is the one that shows (TER-7b) —
    /// and <c>null</c> where nothing covers it. A layer drawn over another still holds the triangles of the
    /// one beneath, so asking whether <em>any</em> triangle of a surface covers a place answers a question
    /// about the mesh rather than about the picture.
    /// </summary>
    static Surface? Topmost(GroundMesh mesh, Vector2 pointM)
    {
        var vertices = mesh.Vertices;
        Surface? showing = null;
        for (var index = 0; index + 2 < mesh.Indices.Length; index += 3)
        {
            var first = (int)mesh.Indices[index];
            if (Covers(
                    vertices[first].PositionM, vertices[(int)mesh.Indices[index + 1]].PositionM,
                    vertices[(int)mesh.Indices[index + 2]].PositionM, pointM))
            {
                showing = vertices[first].Surface;
            }
        }

        return showing;
    }

    static bool Covers(Vector2 aM, Vector2 bM, Vector2 cM, Vector2 pointM)
    {
        var left = true;
        var right = true;
        Span<Vector2> corners = [aM, bM, cM];
        for (var corner = 0; corner < 3; corner++)
        {
            var edge = corners[(corner + 1) % 3] - corners[corner];
            var reach = pointM - corners[corner];
            var turn = (edge.X * reach.Y) - (edge.Y * reach.X);
            left &= turn >= 0f;
            right &= turn <= 0f;
        }

        return left || right;
    }

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
        var mesh = Ground(Towns.City);

        // One indexed draw over the whole city, and the whole of it fits in a few tens of megabytes: the
        // point of laying ground from shapes rather than from a three-million-cell grid, which would be
        // hundreds of times this. The floor is low because a town whose whole surface is grass, water and
        // the lines a car is driven on lays very little — the ground comes back when the layers do.
        Assert.InRange(mesh.Vertices.Length, 100, 600_000);
        Assert.True(mesh.Indices.Length > Ground(Towns.Fixture).Indices.Length,
            "the city lays no more ground than the fixture map");
    }

    /// <summary>
    /// <b>A fill is cut coarser than the line its kerb is struck from</b> (<c>GroundMesh.HiddenShare</c>):
    /// the same shell read both ways lays substantially fewer corners as a fill than as a line.
    /// </summary>
    /// <remarks>
    /// <b>The saving and not a count.</b> How many corners a town comes to is the town's, and a figure
    /// asserted here would be a figure about whichever map this ran on; what the share owes is that it is
    /// still spending itself, since a thinning that quietly stopped thinning costs nothing visible and
    /// doubles the ground.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void AFillIsCutCoarserThanTheLineItsKerbIsStruckFrom(string map)
    {
        var config = SimConfig.Shipped();
        Assert.True(Ground(map).Indices.Length > 0, $"{map} lays no ground at all");

        var line = Drawn(Towns.Of(map).Paving(config).Rings(config).Carriageway.Rings);
        var corners = line.Sum(ring => ring.Length);
        var thinned = GroundMesh.Filled(line, config.Road.KerbWidthM).Sum(ring => ring.Length);

        Assert.True(thinned < corners, $"{map} thins {corners} corners of its line to {thinned}");
    }
}
