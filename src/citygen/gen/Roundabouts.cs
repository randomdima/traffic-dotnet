using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>Which junctions of the town are opened out into a roundabout</b> (GEN-19): the ones a district's own
/// streets leave it by, where there is ground for a circle and the arms already stand round it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A roundabout is not a new kind of junction — it is a ring of ordinary ones</b>
/// (<see cref="TownLayout.RingOut"/>). What the node becomes is a circle of one-way arcs with one junction
/// on it per arm, each arm meeting the ring on its own bearing and nothing else changed: the movements, the
/// right of way, the paint, the pavement and the ground are the town's ordinary answers to a junction of
/// three arms, and nothing downstream carries a rule about roundabouts at all.
/// </para>
/// <para>
/// <b>Circulating traffic keeps the right of way without anything granting it one.</b> The two ring arcs at
/// a node are two pieces of one circle, so the movement between them is straight on where the movement in
/// off the arm is a turn, and <c>TER-5e</c> ranks the straighter one over it. That is exactly what a
/// roundabout is for, and it falls out of the ranking rather than being written anywhere.
/// </para>
/// <para>
/// <b>It runs on the layout the deletions left</b>, after the merge, the crossings, the stranded pieces and
/// the dead ends (<see cref="TownGenerator"/>), because a ring is laid against the town there actually is
/// — and before the one-way scatter, which then finds every ring node already spoken for.
/// </para>
/// <para>
/// <b>Nothing is laid and taken back</b> (GEN-8, GEN-10). Every one of the conditions below is asked before
/// the node is opened out, so a ring that is begun is a ring that stands; where one of them fails the
/// junction stays the junction it was.
/// </para>
/// </remarks>
internal static class Roundabouts
{
    /// <summary>
    /// How many roads have to meet at a node before a circle is worth laying there (GEN-19): four, because
    /// a node of three is a junction the ranking already settles standing still.
    /// </summary>
    public const int ArmsLeast = 4;

    public static void Lay(
        TownLayout layout, Districts districts, WaterRules water, SimConfig config, Vector2 extentM,
        float marginM)
    {
        var laidM = new List<Vector2>();

        // The nodes the town had before any of this: a ring's own nodes are not places for a second ring,
        // and everything appended below is one of those.
        var nodes = layout.NodeM.Count;
        for (var node = 0; node < nodes; node++)
        {
            var arms = layout.ArmsAt(node);
            if (!ADistrictLeavesHere(layout, arms)) continue;
            if (StandsNearOne(laidM, layout.NodeM[node], config.CityGen.RoundaboutApartMinM)) continue;

            var radiusM = RadiusM(layout, node, arms, config);
            if (float.IsNaN(radiusM)) continue;
            if (!RoomFor(layout, districts, water, config, node, arms, radiusM, extentM, marginM)) continue;

            layout.RingOut(node, radiusM, Curvature(radiusM, config));
            laidM.Add(layout.NodeM[node]);
        }

        // Every node a ring was opened out of is left standing with nothing at it, and this is what drops
        // them. There are no dead ends left for it to find: a ring node carries three arms and no arm of the
        // town was deleted.
        if (laidM.Count > 0) layout.PruneTheDeadEnds();
    }

    /// <summary>
    /// <b>Which way round a roundabout is driven</b>: the island on the side the traffic does not keep, so
    /// a car goes round it turning away from the kerb it drives against — anticlockwise where the traffic
    /// keeps right. Curvature is signed the way <see cref="ArcSeg"/> signs it, positive to the driver's
    /// right.
    /// </summary>
    static float Curvature(float radiusM, SimConfig config) => -config.RoadSideSign / radiusM;

    /// <summary>
    /// <b>Where a district leaves the town</b> (GEN-19): a junction of four arms or more that some district's
    /// own street meets a road carrying the town between districts at. <b>A bridgehead is never one</b> — a
    /// deck cannot move (GEN-14a), and the arm the ring would cut back is the span itself.
    /// </summary>
    /// <remarks>
    /// <b>Three arms are a junction and not a roundabout</b> (GEN-19). A ring there is a circle laid to sort
    /// out one conflict, which the ranking already sorts out standing still, and it costs every car through
    /// the node a detour to reach the arm opposite.
    /// </remarks>
    static bool ADistrictLeavesHere(TownLayout layout, List<int> arms)
    {
        if (arms.Count < ArmsLeast) return false;

        var street = false;
        var arterial = false;
        foreach (var arm in arms)
        {
            switch (layout.Edges[arm].Class)
            {
                case RoadClass.Bridge or RoadClass.Roundabout: return false;
                case RoadClass.Street: street = true; break;
                default: arterial = true; break;
            }
        }

        return street && arterial;
    }

    /// <summary>
    /// <b>How wide the circle at this node has to be</b>, or not a number where no circle fits its arms —
    /// and no wider than that, so a roundabout is the smallest circle its arms and its own speed allow.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The arms are what size it.</b> Two of them a gap apart put two junctions that gap apart on the
    /// ring, and what leaves those two is a pair of roads running side by side: they owe each other the
    /// ground one road takes (<see cref="SimConfig.RoadFootprintM"/>, GEN-17) and a pavement's width of
    /// ground on top of it, because two mouths whose paving abuts is paving with nothing to wrap round.
    /// <b>That, and not the road two separate junctions would owe each other</b> — a ring's nodes are one
    /// junction laid out as a circle (GEN-16, GEN-19).
    /// </para>
    /// <para>
    /// What is left over is the floor the roundabout's own design speed affords
    /// (<see cref="SimConfig.RoundaboutRadiusFloorM"/>), which is the answer wherever the arms stand well
    /// apart.
    /// </para>
    /// <para>
    /// <b>A gap wider than half a turn is refused rather than filled.</b> The piece of ring across it would
    /// be an arc longer than a half circle — a road that doubles back on itself, which is not a shape this
    /// layout carries (<see cref="RoadStage"/>) — and a junction whose arms all leave on one side is not one
    /// anybody would drive round anyway.
    /// </para>
    /// </remarks>
    static float RadiusM(TownLayout layout, int node, List<int> arms, SimConfig config)
    {
        var spanM = config.RoadFootprintM + config.PavementWidthM;
        var radiusM = config.RoundaboutRadiusFloorM;
        var widestRad = 0f;
        for (var arm = 0; arm < arms.Count; arm++)
        {
            var next = arms[(arm + 1) % arms.Count];
            var gapRad = Wrapped(layout.OutwardRad(next, node) - layout.OutwardRad(arms[arm], node));
            widestRad = MathF.Max(widestRad, gapRad);

            radiusM = MathF.Max(radiusM, spanM * 0.5f / MathF.Sin(gapRad * 0.5f));
        }

        return widestRad > MathF.PI ? float.NaN : radiusM;
    }

    /// <summary>
    /// <b>Whether the town can afford the circle</b>: the four things a ring takes that the junction it
    /// replaces did not, each of them a rule the finished town is held to anyway.
    /// <list type="bullet">
    /// <item><b>Its own ground is dry and on the map</b> (GEN-14, GEN-2b) — a ring node is a junction, and
    /// nothing a junction is made of stands over water or past the edge.</item>
    /// <item><b>Every arm is still a road once it is cut back</b>, with its far junction a locality clear of
    /// the ring node it now ends at (GEN-16).</item>
    /// <item><b>Every other junction in the town stands clear of the whole circle</b>, on the same
    /// terms.</item>
    /// <item><b>Every road the node does not carry stands clear of it</b> (GEN-17), measured as the ground
    /// it is drawn on rather than as the chord it was joined along.</item>
    /// </list>
    /// </summary>
    static bool RoomFor(
        TownLayout layout, Districts districts, WaterRules water, SimConfig config, int node, List<int> arms,
        float radiusM, Vector2 extentM, float marginM)
    {
        var centreM = layout.NodeM[node];
        var localityM = config.CityGen.LocalityM;
        var clearM = radiusM + localityM;

        var reachM = radiusM + marginM;
        if (centreM.X < reachM || centreM.Y < reachM
            || centreM.X > extentM.X - reachM || centreM.Y > extentM.Y - reachM)
        {
            return false;
        }

        foreach (var arm in arms)
        {
            var edge = layout.Edges[arm];
            if ((layout.NodeM[edge.To] - layout.NodeM[edge.From]).Length() < clearM) return false;
            if (water.Wet(centreM + (Heading.Unit(layout.OutwardRad(arm, node)) * radiusM))) return false;
        }

        if (water.Wet(centreM)) return false;

        for (var other = 0; other < layout.NodeM.Count; other++)
        {
            if (other == node) continue;
            if ((layout.NodeM[other] - centreM).Length() < clearM) return false;
        }

        var straysM = RoadStage.StraysM(layout, districts, config);
        for (var road = 0; road < layout.Edges.Count; road++)
        {
            if (arms.Contains(road)) continue;

            var offM = OffM(centreM, layout.NodeM[layout.Edges[road].From], layout.NodeM[layout.Edges[road].To]);
            if (offM < radiusM + config.RoadFootprintM + straysM[road]) return false;
        }

        return true;
    }

    static bool StandsNearOne(List<Vector2> laidM, Vector2 atM, float apartM)
    {
        foreach (var centreM in laidM)
        {
            if (Vector2.Distance(centreM, atM) < apartM) return true;
        }

        return false;
    }

    static float OffM(Vector2 pointM, Vector2 fromM, Vector2 toM)
    {
        var runM = toM - fromM;
        var lengthSquared = runM.LengthSquared();
        var along = lengthSquared > 0f
            ? Math.Clamp(Vector2.Dot(pointM - fromM, runM) / lengthSquared, 0f, 1f)
            : 0f;
        return (pointM - (fromM + (runM * along))).Length();
    }

    static float Wrapped(float radians) => radians - (MathF.Tau * MathF.Floor(radians / MathF.Tau));
}
