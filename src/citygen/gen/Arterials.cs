using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>The roads that belong to the town rather than to a district</b>: the hub, the spokes out of it, and
/// the orbital they cross. They are laid first, and everything else is laid in the ground they leave.
/// </summary>
/// <remarks>
/// <para>
/// <b>A wheel is planar without any intersection arithmetic.</b> Spokes leave one hub on their own bearings,
/// so no two of them meet anywhere else; the orbital carries a node at every spoke's own bearing, so a
/// spoke crosses it <em>at a junction that was placed there</em> rather than at a crossing somebody has to
/// find and split. What would otherwise be the hardest thing about laying a road network is arranged away.
/// </para>
/// <para>
/// <b>A node is a place on an arterial, and the roads are what is left between them once everything has
/// been placed.</b> The lattice hangs its own streets off these (<see cref="Lattice"/>), each of which puts
/// another node on the ring or the spoke it meets — so the chain is closed last, by
/// <see cref="Close"/>, when nothing more can be inserted.
/// </para>
/// <para>
/// <b>And an arterial is the only thing that crosses water</b> (GEN-14a). Where one meets the water it
/// carries a node on each bank, so what is left between them is the water that path actually crosses rather
/// than whatever the node spacing happened to leave either side of it — and the stretch between the two is
/// closed to everything else, because a node inserted there would be a junction on the deck or in the river.
/// </para>
/// </remarks>
internal sealed class Arterials
{
    /// <summary>How far apart the orbital carries a node of its own, before any street hangs one off it.</summary>
    const float RingNodeSpacingM = 220f;

    /// <summary>And the same along a spoke.</summary>
    const float SpokeNodeSpacingM = 200f;

    /// <summary>A line an arterial is laid along, so the water can be met on it by walking its own length.</summary>
    interface IPath
    {
        Vector2 PointAt(float alongM);
    }

    readonly struct Ray(Vector2 fromM, Vector2 unit) : IPath
    {
        public Vector2 PointAt(float alongM) => fromM + (unit * alongM);
    }

    readonly struct Orbit(Vector2 hubM, float radiusM, float fromRad) : IPath
    {
        public Vector2 PointAt(float alongM) =>
            hubM + (Heading.Unit(fromRad + (alongM / radiusM)) * radiusM);
    }

    readonly Districts _districts;
    readonly List<(float ThetaRad, int Node)> _ring = [];
    readonly List<(float RadiusM, int Node)>[] _spokes;

    /// <summary>The stretch of each spoke a bridge stands on, where nothing else may put a node.</summary>
    readonly List<(float FromM, float ToM)>[] _spanned;

    /// <summary>And the same round the orbital, as arc length from the angle its own walk started at.</summary>
    readonly List<(float FromM, float ToM)> _ringSpanned = [];

    float _ringFromRad;

    Arterials(Districts districts)
    {
        _districts = districts;
        _spokes = new List<(float, int)>[districts.Spokes];
        _spanned = new List<(float, float)>[districts.Spokes];
        for (var spoke = 0; spoke < _spokes.Length; spoke++)
        {
            _spokes[spoke] = [];
            _spanned[spoke] = [];
        }
    }

    public static Arterials Lay(
        TownLayout layout, Districts districts, TownBrief brief, WaterRules water, float shortestRoadM,
        float marginM)
    {
        var arterials = new Arterials(districts);
        var extentM = new Vector2(brief.WidthM, brief.HeightM);
        var hub = layout.AddNode(districts.HubM);
        var heads = new List<float>();

        for (var spoke = 0; spoke < districts.Spokes; spoke++)
        {
            var unit = districts.SpokeUnit(spoke);
            var reachM = ReachToTheEdgeM(districts.HubM, unit, extentM) - marginM;
            if (hub >= 0) arterials._spokes[spoke].Add((0f, hub));

            // The banks come first and everything else is spaced around them: a bridgehead is where the
            // ground says it is, and a node of the ordinary spacing is wherever there is still room.
            heads.Clear();
            Bridgeheads(
                new Ray(districts.HubM, unit), reachM, shortestRoadM, water, heads,
                arterials._spanned[spoke]);
            foreach (var atM in heads) arterials.Stand(layout, spoke, atM, shortestRoadM);

            // The orbital puts its own node where it crosses, so the spacing leaves that radius alone.
            var ringM = districts.HasRing ? districts.RingRadiusM : float.PositiveInfinity;
            for (var atM = SpokeNodeSpacingM; atM <= reachM; atM += SpokeNodeSpacingM)
            {
                if (MathF.Abs(atM - ringM) < SpokeNodeSpacingM * 0.5f) continue;

                arterials.Stand(layout, spoke, atM, SpokeNodeSpacingM * 0.5f);
            }

            if (MathF.Abs(reachM - ringM) >= SpokeNodeSpacingM * 0.5f)
            {
                arterials.Stand(layout, spoke, reachM, SpokeNodeSpacingM * 0.5f);
            }
        }

        if (districts.HasRing) arterials.LayTheRing(layout, water, shortestRoadM);
        return arterials;
    }

    void LayTheRing(TownLayout layout, WaterRules water, float shortestRoadM)
    {
        var radiusM = _districts.RingRadiusM;
        var circumferenceM = MathF.Tau * radiusM;

        // The walk starts on dry ground, so a wet stretch is always met from a bank and never from the
        // middle of itself — otherwise the one crossing the seam of the circle fell in would carry no
        // bridgeheads and the town would lose a bridge to where the angles happen to start.
        _ringFromRad = DryStartRad(water, radiusM, circumferenceM);
        if (float.IsNaN(_ringFromRad)) return;

        var heads = new List<float>();
        Bridgeheads(
            new Orbit(_districts.HubM, radiusM, _ringFromRad), circumferenceM, shortestRoadM, water, heads,
            _ringSpanned);
        foreach (var atM in heads) StandOnTheRing(layout, _ringFromRad + (atM / radiusM), shortestRoadM);

        // Every spoke's own bearing carries a ring node, so the two meet at a junction placed there rather
        // than at a crossing that has to be found — welded onto a bridgehead already standing near it.
        for (var spoke = 0; spoke < _districts.Spokes; spoke++)
        {
            var node = OnTheRing(layout, _districts.SpokeBearingRad(spoke), SpokeNodeSpacingM * 0.5f);
            if (node >= 0) _spokes[spoke].Add((radiusM, node));
        }

        var betweenRad = RingNodeSpacingM / radiusM;
        for (var thetaRad = 0f; thetaRad < MathF.Tau; thetaRad += betweenRad)
        {
            StandOnTheRing(layout, thetaRad, RingNodeSpacingM * 0.5f);
        }
    }

    /// <summary>
    /// <b>A node on each bank wherever a path meets the water, and the stretch between them closed</b>
    /// (GEN-14b). This is what makes a bridge short: the pair stands the abutment's own width back from the
    /// two banks, so what spans the water is the shortest run this path affords rather than the distance
    /// between the two nodes the spacing would otherwise have left either side of it.
    /// </summary>
    /// <remarks>
    /// <b>Where no pair will do, none is laid and the path is simply cut at the water.</b> A span longer
    /// than the deck a town builds, a bridgehead that lands in the water again, or a sea rather than a river
    /// all end the same way: the two banks carry ordinary nodes, the chord between them is refused
    /// (<see cref="WaterRules.Carries"/>), and whichever side is left unreachable is deleted with its own
    /// piece (GEN-8).
    /// </remarks>
    static void Bridgeheads<TPath>(
        TPath path, float lengthM, float shortestRoadM, WaterRules water, List<float> heads,
        List<(float FromM, float ToM)> spanned)
        where TPath : struct, IPath
    {
        var stepM = water.StepM;
        var wasWet = water.Wet(path.PointAt(0f));
        var entered = false;
        var enteredM = 0f;

        for (var alongM = stepM; alongM <= lengthM; alongM += stepM)
        {
            var wet = water.Wet(path.PointAt(alongM));
            if (wet == wasWet) continue;

            wasWet = wet;
            if (wet)
            {
                entered = true;
                enteredM = alongM;
                continue;
            }

            if (!entered) continue;

            // Never so close together that what joins them is shorter than a road: a sliver of water is
            // spanned by the shortest bridge the town builds rather than by a road no junction fits on.
            var middleM = (enteredM + alongM) * 0.5f;
            var halfM = MathF.Max(((alongM - enteredM) * 0.5f) + water.AbutmentM, shortestRoadM);
            var nearM = middleM - halfM;
            var farM = middleM + halfM;
            if (nearM < 0f || farM > lengthM) continue;

            var atNearM = path.PointAt(nearM);
            var atFarM = path.PointAt(farM);
            if (water.Wet(atNearM) || water.Wet(atFarM) || !water.Spans(atNearM, atFarM)) continue;

            heads.Add(nearM);
            heads.Add(farM);
            spanned.Add((nearM, farM));
        }
    }

    /// <summary>Where round the orbital the ground is dry, or not a number where none of it is.</summary>
    float DryStartRad(WaterRules water, float radiusM, float circumferenceM)
    {
        for (var alongM = 0f; alongM < circumferenceM; alongM += water.StepM)
        {
            var thetaRad = alongM / radiusM;
            if (!water.Wet(_districts.HubM + (Heading.Unit(thetaRad) * radiusM))) return thetaRad;
        }

        return float.NaN;
    }

    /// <summary>One node on a spoke, if the water, the bridge it would stand on and the spacing all take it.</summary>
    void Stand(TownLayout layout, int spoke, float atM, float apartM)
    {
        if (atM <= 0f || InASpan(_spanned[spoke], atM)) return;

        foreach (var (radiusM, _) in _spokes[spoke])
        {
            if (MathF.Abs(radiusM - atM) < apartM) return;
        }

        var node = layout.AddNode(_districts.HubM + (_districts.SpokeUnit(spoke) * atM));
        if (node >= 0) _spokes[spoke].Add((atM, node));
    }

    /// <summary>And one on the orbital, on the same terms.</summary>
    void StandOnTheRing(TownLayout layout, float thetaRad, float apartM)
    {
        if (OnABridge(thetaRad)) return;

        var wrappedRad = Wrapped(thetaRad);
        foreach (var (atRad, _) in _ring)
        {
            if (ApartM(atRad, wrappedRad) < apartM) return;
        }

        var node = layout.AddNode(_districts.HubM + (Heading.Unit(thetaRad) * _districts.RingRadiusM));
        if (node >= 0) _ring.Add((wrappedRad, node));
    }

    /// <summary>Whether an angle stands on the stretch of the orbital a bridge has taken.</summary>
    bool OnABridge(float thetaRad) =>
        InASpan(_ringSpanned, Wrapped(thetaRad - _ringFromRad) * _districts.RingRadiusM);

    static bool InASpan(List<(float FromM, float ToM)> spanned, float atM)
    {
        foreach (var (fromM, toM) in spanned)
        {
            if (atM > fromM && atM < toM) return true;
        }

        return false;
    }

    /// <summary>How far apart two angles stand on the orbital, the short way round.</summary>
    float ApartM(float aRad, float bRad)
    {
        var apartRad = MathF.Abs(aRad - bRad);
        return MathF.Min(apartRad, MathF.Tau - apartRad) * _districts.RingRadiusM;
    }

    /// <summary>
    /// Where a street meets the orbital, as a node on it — the one already standing there if a street from
    /// the other side has put one within welding distance, and a new one otherwise. <b>Two junctions a
    /// stride apart are a road with no length</b>, and the two districts either side of an arterial meet it
    /// at their own spacings, so the near-coincidence is the ordinary case rather than the odd one.
    /// </summary>
    public int OnTheRing(TownLayout layout, float thetaRad, float weldM)
    {
        var wrappedRad = Wrapped(thetaRad);
        foreach (var (atRad, node) in _ring)
        {
            if (ApartM(atRad, wrappedRad) < weldM) return node;
        }

        // Nothing is inserted onto a bridge: a node between two bridgeheads is a junction on the deck, and
        // one in the water is a junction in the river (GEN-14).
        if (OnABridge(thetaRad)) return -1;

        var placed = layout.AddNode(_districts.HubM + (Heading.Unit(thetaRad) * _districts.RingRadiusM));
        if (placed >= 0) _ring.Add((wrappedRad, placed));
        return placed;
    }

    /// <summary>And where one meets a spoke, on the same terms.</summary>
    public int OnASpoke(TownLayout layout, int spoke, float radiusM, float weldM)
    {
        foreach (var (atM, node) in _spokes[spoke])
        {
            if (MathF.Abs(atM - radiusM) < weldM) return node;
        }

        if (InASpan(_spanned[spoke], radiusM)) return -1;

        var placed = layout.AddNode(_districts.HubM + (_districts.SpokeUnit(spoke) * radiusM));
        if (placed >= 0) _spokes[spoke].Add((radiusM, placed));
        return placed;
    }

    /// <summary>
    /// Joins each arterial's nodes into the roads between them, once every street that wanted one has put
    /// its own node on them. <b>The orbital's pieces are arcs</b> — it is a circle, and a circle laid as
    /// chords is a polygon with a corner at every junction.
    /// </summary>
    public void Close(TownLayout layout, WaterRules water)
    {
        foreach (var spoke in _spokes)
        {
            spoke.Sort((a, b) => a.RadiusM.CompareTo(b.RadiusM));
            for (var at = 0; at + 1 < spoke.Count; at++)
            {
                Link(layout, water, spoke[at].Node, spoke[at + 1].Node, 0f);
            }
        }

        if (_ring.Count < 2) return;

        _ring.Sort((a, b) => a.ThetaRad.CompareTo(b.ThetaRad));
        var curvature = 1f / _districts.RingRadiusM;
        for (var at = 0; at < _ring.Count; at++)
        {
            Link(layout, water, _ring[at].Node, _ring[(at + 1) % _ring.Count].Node, curvature);
        }
    }

    /// <summary>
    /// One piece of an arterial, as the kind of road the ground under it makes it. <b>A piece standing over
    /// the water is a bridge and is laid straight</b> (GEN-14a) — the orbital's own arc is given up over the
    /// span, because a deck is a straight thing. What is refused here is a crossing the town may not make,
    /// and the arterial is simply cut at the bank.
    /// </summary>
    static void Link(TownLayout layout, WaterRules water, int from, int to, float curvature)
    {
        var wet = water.Wets(layout.NodeM[from], layout.NodeM[to]);
        layout.Join(from, to, wet ? RoadClass.Bridge : RoadClass.Arterial, wet ? 0f : curvature);
    }

    /// <summary>
    /// Whether a point stands in the ground an arterial and its own pavement have taken, which is where a
    /// street may not put a node.
    /// </summary>
    public bool InACorridor(Vector2 pointM, float clearanceM)
    {
        var offsetM = pointM - _districts.HubM;
        var radiusM = offsetM.Length();
        if (_districts.HasRing && MathF.Abs(radiusM - _districts.RingRadiusM) < clearanceM) return true;

        for (var spoke = 0; spoke < _districts.Spokes; spoke++)
        {
            var unit = _districts.SpokeUnit(spoke);
            var alongM = Vector2.Dot(offsetM, unit);
            if (alongM < 0f) continue;
            if (MathF.Abs(Cross(unit, offsetM)) < clearanceM) return true;
        }

        return false;
    }

    /// <summary>
    /// Whether a straight between two points crosses the orbital. <b>The one test the convex districts do
    /// not make unnecessary</b>: the ground outside a circle is not convex, so two nodes of one outer sector
    /// can stand either side of the ring's own arc.
    /// </summary>
    public bool CrossesTheRing(Vector2 fromM, Vector2 toM)
    {
        if (!_districts.HasRing) return false;

        var inside = (fromM - _districts.HubM).Length() < _districts.RingRadiusM;
        return inside != (toM - _districts.HubM).Length() < _districts.RingRadiusM;
    }

    /// <summary>Which spoke a point stands nearest, and how far out along it — how a street finds where to meet one.</summary>
    public int NearestSpoke(Vector2 pointM, out float radiusM)
    {
        var offsetM = pointM - _districts.HubM;
        var nearest = 0;
        var nearestM = float.PositiveInfinity;
        for (var spoke = 0; spoke < _districts.Spokes; spoke++)
        {
            var awayM = MathF.Abs(Cross(_districts.SpokeUnit(spoke), offsetM));
            if (Vector2.Dot(offsetM, _districts.SpokeUnit(spoke)) < 0f || awayM >= nearestM) continue;

            nearestM = awayM;
            nearest = spoke;
        }

        radiusM = Vector2.Dot(offsetM, _districts.SpokeUnit(nearest));
        return nearest;
    }

    public float ThetaOf(Vector2 pointM)
    {
        var offsetM = pointM - _districts.HubM;
        return MathF.Atan2(offsetM.Y, offsetM.X);
    }

    static float Wrapped(float thetaRad) => thetaRad - (MathF.Tau * MathF.Floor(thetaRad / MathF.Tau));

    static float Cross(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);

    /// <summary>How far a ray from the hub runs before it leaves the town, less the margin nothing is laid inside.</summary>
    static float ReachToTheEdgeM(Vector2 fromM, Vector2 unit, Vector2 extentM)
    {
        var reachM = float.PositiveInfinity;
        if (MathF.Abs(unit.X) > 1e-4f)
        {
            reachM = MathF.Min(reachM, ((unit.X > 0f ? extentM.X : 0f) - fromM.X) / unit.X);
        }

        if (MathF.Abs(unit.Y) > 1e-4f)
        {
            reachM = MathF.Min(reachM, ((unit.Y > 0f ? extentM.Y : 0f) - fromM.Y) / unit.Y);
        }

        return reachM;
    }
}
