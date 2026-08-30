using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Terrain;

namespace TrafficSimulation.App.Render;

/// <summary>Which of the five surfaces a triangle wears, or <see cref="Paint"/> for a flat colour.</summary>
internal enum Surface : uint
{
    Grass = 0,
    Tarmac = 1,
    Pavement = 2,
    Deck = 3,
    Water = 4,

    /// <summary>Not a surface: the tint alone, for anything that is not ground.</summary>
    Paint = 255,
}

/// <summary>
/// One corner of the ground. The texture coordinate is computed here, at load, from the world
/// position alone — which is what anchors every surface's texture to the world origin rather than to
/// the shape being painted, and what makes the triangulation invisible (TER-7's drawn half).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct GroundVertex(Vector2 PositionM, Vector2 Uv, Vector3 Tint, Surface Surface);

/// <summary>
/// The town's standing ground, triangulated once at load from the plan's <b>shapes</b> — road ribbons
/// bent along their own splines, junction discs, corner fillets, decks, slabs, lots and water
/// outlines. Never from the cell grid: no arrangement of metre squares is a kerb running at 40°.
/// </summary>
/// <remarks>
/// <para>
/// The order the triangles are laid in is the order they are painted in: grass over the whole world;
/// the pavement, which is every road ribbon, junction disc and lot read out a walk's width bigger and
/// drawn twice for its edge line; the water; the decks; the paved slabs; the lots; the carriageway;
/// the junction discs; the corner fillets. There is no depth buffer and nothing to sort — one indexed
/// draw in one pass, and the pavement band falls out as the two strips either side of what is drawn
/// over it, without anything having to know where a kerb is.
/// </para>
/// <para>
/// What is <b>not</b> here is anything that is not ground: buildings, props, agents and their sprites
/// are the second pipeline.
/// </para>
/// </remarks>
internal sealed partial class GroundMesh
{
    /// <summary>How far a drawn chord is allowed to bow off the arc it stands for.</summary>
    const float ChordSagM = 0.02f;

    /// <summary>White: a surface drawn as itself.</summary>
    static readonly Vector3 Plain = Vector3.One;

    readonly List<GroundVertex> _vertices = [];
    readonly List<uint> _indices = [];

    GroundMesh()
    {
    }

    public ReadOnlySpan<GroundVertex> Vertices => CollectionsMarshal.AsSpan(_vertices);

    public ReadOnlySpan<uint> Indices => CollectionsMarshal.AsSpan(_indices);

    /// <summary>
    /// Where the marks start, as an index into <see cref="Vertices"/>: everything from here on is a
    /// dash, a bar, a zebra's stripe or a bay stroke, four corners at a time.
    /// </summary>
    /// <remarks>
    /// <b>The kerb line is paint too</b>, and it is a rim on a ribbon, a disc and a fillet rather than
    /// a quad — so the tint alone no longer tells a mark from the ground it is on, and anything asking
    /// what was <em>painted</em> asks this instead.
    /// </remarks>
    public int FirstMarkVertex { get; private set; }

    /// <summary>
    /// No ground at all: one degenerate triangle, which is what the start menu is drawn over.
    /// </summary>
    /// <remarks>
    /// <b>GEN-1b — nothing is built until a map is picked</b>, and a menu still has to be drawn by
    /// something. A renderer whose ground pass covers no pixels is the honest shape of that: the
    /// menu's own quads are the third pipeline, exactly as they are over a town, and the picture says
    /// plainly that no town exists.
    /// </remarks>
    public static GroundMesh Nothing()
    {
        var mesh = new GroundMesh();
        for (var corner = 0; corner < 3; corner++)
        {
            mesh._vertices.Add(new GroundVertex(Vector2.Zero, Vector2.Zero, Vector3.Zero, Surface.Grass));
            mesh._indices.Add((uint)corner);
        }

        return mesh;
    }

    public static GroundMesh Build(CityPlan plan, SimConfig config)
    {
        var mesh = new GroundMesh();
        var periods = Periods(config);
        var walkM = plan.PavementWidthM > 0f ? plan.PavementWidthM : config.Road.PavementWidthM;
        var edgeM = config.Road.EdgeLineWidthM;

        // An edge is the surface darkened and paint is the surface brightened. Two measurements, not one
        // relation: the inverse of the edge shade is 1.72 and lays a dash two and a half times too dark.
        // Nothing else in the town is drawn in a colour of its own.
        var edge = Shade(0.58f, 0.58f, 0.62f);
        var paint = Shade(2.6f, 2.6f, 2.5f);
        var kerbM = config.Road.PaintLineWidthM;
        var cornerM = config.PavementCornerRadiusM;
        var corners = PavementCorners.Solve(plan, config);

        mesh.Rect(Vector2.Zero, plan.WorldSizeM, Surface.Grass, Plain, periods);

        // The pavement, twice: once at full size in the edge shade, then a line's width smaller in
        // the surface shade over the top. Since every fill follows every rim, what survives is a rim
        // on the union's own outer boundary and nowhere two pieces meet.
        foreach (var inset in (ReadOnlySpan<float>)[0f, edgeM])
        {
            var tint = inset == 0f ? edge : Plain;
            for (var road = 0; road < plan.Roads.Count; road++)
            {
                mesh.Ribbon(plan.Roads.SegmentsOf(road), plan.Roads.WidthM[road] * 0.5f + walkM - inset,
                    Surface.Pavement, tint, periods);
            }

            for (var junction = 0; junction < plan.Junctions.Count; junction++)
            {
                mesh.Disc(plan.Junctions.CentreM[junction], plan.Junctions.RadiusM[junction] + walkM - inset,
                    Surface.Pavement, tint, periods);
            }

            // TER-3c.3: a lot turns a right angle of its own, so its wrap turns on half the walk —
            // which stands the corner 4.83 m deep against the straight's 4 m. Rounded on the full
            // width the band would be 4 m everywhere and read pinched; square takes a bite of verge.
            for (var lot = 0; lot < plan.ParkingLots.Count; lot++)
            {
                mesh.RoundedRect(plan.ParkingLots.CentreM[lot], plan.ParkingLots.Axis[lot],
                    plan.ParkingLots.HalfExtentM[lot] + new Vector2(walkM - inset), cornerM - inset,
                    Surface.Pavement, tint, periods);
            }

            // TER-3c.4: where two of the pieces above run into one another they leave a re-entrant spike
            // of verge, and it is turned on an arc like any other corner. The fillet is the same piece a
            // kerb fillet is — apex, arc, two tangent points — and it insets the same way: the arc is the
            // union's own boundary here and draws in, the two straight sides are the neighbours' seen
            // from inside and draw out to meet where those have drawn back to.
            foreach (var corner in corners)
            {
                mesh.Fillet(corner.CornerM, corner.ArcCentreM, corner.RadiusM, corner.TangentAM, corner.TangentBM,
                    inset, Surface.Pavement, tint, periods);
            }
        }

        for (var outline = 0; outline < plan.Water.Count; outline++)
        {
            mesh.Polygon(plan.Water.OutlineOf(outline), Surface.Water, Plain, periods);
        }

        // A deck is drawn like the section TER-3b.1 draws: the deck itself out to its own half-width,
        // then the town's pavement carried across it at the width it has on land, which leaves the
        // margin — the ground a parapet stands on — as the strip of deck outside the walk. Both carry
        // an edge line, and each is laid the way the pavement's is on land: the piece at full size in
        // the edge shade, then a line's width smaller in its own.
        for (var bridge = 0; bridge < plan.Bridges.Count; bridge++)
        {
            var road = plan.Bridges.Road[bridge];
            if (road < 0) continue;

            var span = plan.Roads.SegmentsOf(road);
            var deckPavementM = plan.Bridges.PavementWidthM[bridge] > 0f ? plan.Bridges.PavementWidthM[bridge] : walkM;
            var deckHalfM = plan.Bridges.DeckWidthM[bridge] * 0.5f;
            var walkHalfM = (plan.Roads.WidthM[road] * 0.5f) + deckPavementM;
            mesh.Ribbon(span, deckHalfM, Surface.Deck, edge, periods);
            mesh.Ribbon(span, deckHalfM - edgeM, Surface.Deck, Plain, periods);
            mesh.Ribbon(span, walkHalfM, Surface.Pavement, edge, periods);
            mesh.Ribbon(span, walkHalfM - edgeM, Surface.Pavement, Plain, periods);
        }

        for (var slab = 0; slab < plan.PavedAreas.Count; slab++)
        {
            mesh.Rect(plan.PavedAreas.MinM[slab], plan.PavedAreas.SizeM[slab], Surface.Tarmac, Plain, periods);
        }

        for (var lot = 0; lot < plan.ParkingLots.Count; lot++)
        {
            mesh.OrientedRect(plan.ParkingLots.CentreM[lot], plan.ParkingLots.Axis[lot],
                plan.ParkingLots.HalfExtentM[lot], Surface.Tarmac, Plain, periods);
        }

        // The carriageway, twice, for its kerb line — the same trick the pavement's edge line is drawn
        // by and for the same reason: a stroke laid on the carriageway's own offset curve, broken
        // exactly where a road runs into a junction rather than walked or probed for.
        foreach (var inset in (ReadOnlySpan<float>)[0f, kerbM])
        {
            var tint = inset == 0f ? paint : Plain;
            for (var road = 0; road < plan.Roads.Count; road++)
            {
                mesh.Ribbon(plan.Roads.SegmentsOf(road), (plan.Roads.WidthM[road] * 0.5f) - inset,
                    Surface.Tarmac, tint, periods);
            }

            for (var junction = 0; junction < plan.Junctions.Count; junction++)
            {
                mesh.Disc(plan.Junctions.CentreM[junction], plan.Junctions.RadiusM[junction] - inset,
                    Surface.Tarmac, tint, periods);
            }

            for (var corner = 0; corner < plan.JunctionCorners.Count; corner++)
            {
                mesh.Fillet(plan.JunctionCorners.CornerM[corner], plan.JunctionCorners.ArcCentreM[corner],
                    plan.JunctionCorners.RadiusM[corner], plan.JunctionCorners.TangentAM[corner],
                    plan.JunctionCorners.TangentBM[corner], inset, Surface.Tarmac, tint, periods);
            }
        }

        // The kerb line stops where the kerb does. A car park hangs off the kerb it is laid along, so over
        // its frontage the ground on the far side of that line is the lot's own tarmac and not a walk —
        // and a line painted there is one every car entering the lot drives across. The pavement's edge
        // line is untouched: the lot's wrap is part of the union that one is a rim on, so it already
        // rounds the outside of the lot rather than running between the lot and the street.
        // It is broken over the lot's mouth and not over its whole shadow, and it stops a line's width
        // short of either end of that: the kerb line runs to the far face of the lot's outermost bay
        // stroke, so the corner the two turn is painted exactly once. It is the same end-to-end rule the
        // bay's own three strokes are laid by, with the kerb line as the fourth.
        var frontages = RoadFrontages.Lay(plan, config);
        foreach (var front in frontages.All)
        {
            if (!front.FrontsTheKerb) continue;

            mesh.EdgeStrip(plan.Roads.SegmentsOf(front.Road), front.MouthFromM + kerbM, front.MouthToM - kerbM,
                front.Side * plan.Roads.WidthM[front.Road] * 0.5f, kerbM, Surface.Tarmac, Plain, periods);
        }

        mesh.FirstMarkVertex = mesh._vertices.Count;
        mesh.LaneDashes(plan, config, paint, periods);

        // A zebra spans the whole carriageway kerb to kerb, where a stop bar covers the approaching lane
        // only — so the two are laid off different fields of the plan and neither is the other's default.
        for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
        {
            mesh.Zebra(plan.Crosswalks.CentreM[crossing], plan.Crosswalks.Axis[crossing],
                plan.Crosswalks.DepthM[crossing], plan.Crosswalks.SpanM[crossing], config, paint, periods);
        }

        // The bars that were painted, in the arm's own frame: the plan carries where each one landed,
        // so nothing here re-derives a coordinate somebody else owns.
        for (var bar = 0; bar < plan.StopLines.Count; bar++)
        {
            var approach = plan.StopLines.Approach[bar];
            mesh.OrientedRect(plan.StopLines.CentreM[bar], new Vector2(-approach.Y, approach.X),
                new Vector2(plan.StopLines.SpanM[bar] * 0.5f, plan.StopLines.ThicknessM[bar] * 0.5f),
                Surface.Tarmac, paint, periods);
        }

        mesh.BayStrokes(plan, config, paint, periods, frontages);

        return mesh;
    }

    /// <summary>The period each surface's texture repeats over, in metres, from the figures config carries.</summary>
    public static float[] Periods(SimConfig config) =>
    [
        config.View.GroundPeriodGrassM,
        config.View.GroundPeriodTarmacM,
        config.View.GroundPeriodPavementM,
        config.View.GroundPeriodDeckM,
        config.View.GroundPeriodWaterM,
    ];

    /// <summary>
    /// A multiplier and not a colour, which is why it is three floats and not four bytes: paint is
    /// the surface <em>brighter</em>, so the factor is above one and an eight-bit tint could only
    /// have clamped it back to the ground it was meant to stand out from.
    /// </summary>
    static Vector3 Shade(float r, float g, float b) => new(r, g, b);

}
