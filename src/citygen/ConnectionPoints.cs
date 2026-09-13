using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen;

/// <summary>Which way a car crosses a connection point: into the junction behind it, or out of it.</summary>
internal enum LaneEnd : byte
{
    /// <summary>A car arrives at the junction here, and what it does next is a movement.</summary>
    Enter,

    /// <summary>A car leaves the junction here, onto the road the point stands on.</summary>
    Exit,
}

/// <summary>
/// <b>One end of one lane, drawn</b>: where a car crosses out of a junction's ground onto a road, or back
/// off a road into one, and the direction it is going while it does.
/// </summary>
internal readonly record struct ConnectionPoint(Vector2 AtM, Vector2 DrivenUnit, LaneEnd End);

/// <summary>
/// <b>Where every lane in the town begins and ends</b> (TER-5d), drawn from the world seed and the link and
/// stored nowhere.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the inversion.</b> A lane used to be what was left of a road's own curve once the junction
/// discs had bitten it, so the bearing a car entered a box on was whatever the chord happened to leave.
/// Here the points come first: each arm of each node is given a bearing and a standoff, the movements
/// through the box are laid between the points that produces, and <em>then</em> the road is splined to
/// arrive on the bearings its two ends were drawn with.
/// </para>
/// <para>
/// <b>They live nowhere and are drawn again wherever they are wanted</b>, which is what lets the lanes stay
/// derived from the plan while nothing derived is written to disk. One function, two callers — the
/// generator laying a town and <see cref="Paving"/> deriving its lanes off a town read back — and the same
/// answer, because a seed and a link are all a point ever depended on.
/// </para>
/// <para>
/// <b>A link is its two junctions' own centres</b> and never an index. The layout's edge numbering does not
/// survive to the plan — the unpick, the component keeping, the pruning and the roundabout opening all sit
/// between them, and <c>TownLayout.Rebuilt</c> renumbers the nodes as well — whereas a centre is written to
/// the <c>.town</c> file as a raw <c>F32</c> and reads back bit for bit. The two ends of one link are drawn
/// independently, so a road has two bearings to satisfy and they do not agree.
/// </para>
/// <para>
/// <b>Two kinds of link take their bearing rather than drawing one</b> (GEN-14a, GEN-19): a bridge is one
/// straight span and a ring piece is one arc of one circle, and both are settled before anything here runs.
/// Jittering a bridgehead would be jittering off a deck that cannot move.
/// </para>
/// </remarks>
internal static class ConnectionPoints
{
    /// <summary>The most points one arm of one node ever carries: one lane each way (GEN-15).</summary>
    public const int MostPerArm = 2;

    /// <summary>
    /// The stream this draw takes, so that retuning it moves no earlier stage's town (GEN-11). <b>It is
    /// mixed with the link rather than walked</b>: the derivation asks for one link's points without
    /// walking every link laid before it.
    /// </summary>
    const ulong Stream = 0x636F_6E6E_6563_7400;

    /// <summary>
    /// <b>One arm of one node</b>: the bearing its road leaves on and the line its lanes end on.
    /// </summary>
    /// <param name="Junction">The node the arm stands off.</param>
    /// <param name="NodeM">Its centre, which is what the draw is keyed on.</param>
    /// <param name="OutwardUnit">The bearing the road leaves the node on, jittered off the chord.</param>
    /// <param name="StandM">
    /// The middle of the line the arm's points stand on — a standoff out along the bearing. The points
    /// themselves stand either side of it, half a lane off.
    /// </param>
    internal readonly record struct Arm(int Junction, Vector2 NodeM, Vector2 OutwardUnit, Vector2 StandM);

    /// <summary>
    /// <b>The arm one road leaves one of its two junctions on.</b> The bearing is the chord to the other
    /// junction turned by a drawn angle inside <see cref="CityGenFigures.ConnectionJitterDeg"/>, except on
    /// the two kinds of link whose shape is already settled.
    /// </summary>
    public static Arm ArmOf(GroundPieces ground, SimConfig config, int road, bool atFrom)
    {
        var roads = ground.Roads;
        var junction = atFrom ? roads.FromJunction[road] : roads.ToJunction[road];
        var other = atFrom ? roads.ToJunction[road] : roads.FromJunction[road];

        var nodeM = ground.Junctions.CentreM[junction];
        var towardM = ground.Junctions.CentreM[other];

        var outward = Held(ground, road, junction, other, nodeM, towardM, out var held)
            ? held
            : Jittered(Chord(nodeM, towardM), config, ground.Seed, nodeM, towardM);

        return new Arm(junction, nodeM, outward, nodeM + (outward * config.CityGen.ConnectionStandoffM));
    }

    /// <summary>
    /// <b>Every point one arm carries</b>, written into <paramref name="into"/> and counted back: one for
    /// each way the road is driven (TER-4d), standing half a lane off the arm's own line on the side its
    /// traffic keeps.
    /// </summary>
    /// <remarks>
    /// <b>One rule covers the one-way street as well as the two-way one.</b> A street driven one way is a
    /// lane wide and sits on the half of the corridor its traffic drives rather than down the middle of it
    /// (<c>RoadStage.OntoTheDrivenHalf</c>, TER-4d) — which is the same half a lane, off the same side, as
    /// the lane a two-way street carries in that direction. So the offset is taken off the direction the car
    /// is driving and never off the road's own, and the shift falls out rather than being a case.
    /// </remarks>
    public static int At(GroundPieces ground, SimConfig config, in Arm arm, int road, Span<ConnectionPoint> into)
    {
        var flow = ground.Roads.Flow[road];
        var leavesHere = arm.Junction == ground.Roads.FromJunction[road];

        var written = 0;

        // A car leaves the node on this arm where the road is driven away from it, and arrives on it where
        // the road is driven towards it. A two-way road is both.
        if (flow != (leavesHere ? RoadFlow.AgainstTheRoad : RoadFlow.WithTheRoad))
        {
            into[written++] = Point(config, arm, arm.OutwardUnit, LaneEnd.Exit);
        }

        if (flow != (leavesHere ? RoadFlow.WithTheRoad : RoadFlow.AgainstTheRoad))
        {
            into[written++] = Point(config, arm, -arm.OutwardUnit, LaneEnd.Enter);
        }

        return written;
    }

    /// <inheritdoc cref="At(GroundPieces, SimConfig, in Arm, int, Span{ConnectionPoint})"/>
    public static int At(GroundPieces ground, SimConfig config, int road, bool atFrom, Span<ConnectionPoint> into) =>
        At(ground, config, ArmOf(ground, config, road, atFrom), road, into);

    static ConnectionPoint Point(SimConfig config, in Arm arm, Vector2 drivenUnit, LaneEnd end) =>
        new(
            arm.StandM + (Heading.RightOf(drivenUnit) * config.LaneOffsetM * config.RoadSideSign),
            drivenUnit,
            end);

    static Vector2 Chord(Vector2 nodeM, Vector2 towardM)
    {
        var chord = towardM - nodeM;

        // Two nodes on one another is not a link a bearing can be read off, and the layout does not lay one:
        // answering with a fixed axis keeps the draw total rather than hiding the fault behind a NaN.
        return chord.LengthSquared() > 0f ? Vector2.Normalize(chord) : Vector2.UnitX;
    }

    /// <summary>
    /// The chord turned by an angle drawn inside the bound. <b>Keyed on the two centres and on which of
    /// them the arm stands at</b>, so the two ends of one link draw independently and a town read back off
    /// disk draws what the generator drew.
    /// </summary>
    static Vector2 Jittered(Vector2 chord, SimConfig config, ulong seed, Vector2 nodeM, Vector2 towardM)
    {
        var draw = new Rng(seed, Stream ^ Keyed(nodeM, towardM));
        var boundRad = float.DegreesToRadians(config.CityGen.ConnectionJitterDeg);
        var turnedRad = draw.NextFloat(-boundRad, boundRad);

        var (sin, cos) = MathF.SinCos(turnedRad);
        return new Vector2((chord.X * cos) - (chord.Y * sin), (chord.X * sin) + (chord.Y * cos));
    }

    /// <summary>
    /// The link's own key, as the bits of the four coordinates that name it. <b>Ordered</b>: the arm at one
    /// end and the arm at the other are two draws, and swapping the pair is how they are told apart.
    /// </summary>
    static ulong Keyed(Vector2 nodeM, Vector2 towardM)
    {
        var key = Mixed(BitConverter.SingleToUInt32Bits(nodeM.X));
        key = Mixed(key ^ BitConverter.SingleToUInt32Bits(nodeM.Y));
        key = Mixed(key ^ BitConverter.SingleToUInt32Bits(towardM.X));
        return Mixed(key ^ BitConverter.SingleToUInt32Bits(towardM.Y));
    }

    /// <summary>SplitMix64's finaliser, which is what turns four coordinates into a stream nothing neighbours.</summary>
    static ulong Mixed(ulong of)
    {
        var mixed = of + 0x9E37_79B9_7F4A_7C15ul;
        mixed = (mixed ^ (mixed >> 30)) * 0xBF58_476D_1CE4_E5B9ul;
        mixed = (mixed ^ (mixed >> 27)) * 0x94D0_49BB_1331_11EBul;
        return mixed ^ (mixed >> 31);
    }

    /// <summary>
    /// <b>Whether this link's bearing is already settled, and what it is</b> (§6.10). A bridge is one
    /// straight span and leaves square along its own chord; a ring piece is one arc of the circle its
    /// roundabout was sized as, and leaves along that circle's tangent.
    /// </summary>
    static bool Held(
        GroundPieces ground, int road, int junction, int other, Vector2 nodeM, Vector2 towardM,
        out Vector2 outward)
    {
        if (Array.IndexOf(ground.Bridges.Road, road) >= 0)
        {
            outward = Chord(nodeM, towardM);
            return true;
        }

        if (RingCentreM(ground, road) is { } centreM)
        {
            // Square to the radius, and round the circle the way this arm is driven: the tangent at a point
            // of a circle is the radius turned a quarter, and which quarter is which end of the piece.
            var tangent = Heading.RightOf(Vector2.Normalize(nodeM - centreM));
            var along = towardM - nodeM;
            outward = Vector2.Dot(tangent, along) >= 0f ? tangent : -tangent;
            return true;
        }

        outward = Vector2.Zero;
        return false;
    }

    /// <summary>
    /// The middle of the circle a roundabout's ring was laid on, or nothing where the road is on no ring.
    /// <b>Read off the ring's own nodes</b>: the plan carries which roads circulate and lets their arcs say
    /// where, so the centre is the one point every one of those nodes stands the same distance from.
    /// </summary>
    static Vector2? RingCentreM(GroundPieces ground, int road)
    {
        var rings = ground.Roundabouts;
        for (var ring = 0; ring < rings.Count; ring++)
        {
            var roads = rings.RoadsOf(ring);
            if (roads.IndexOf(road) < 0) continue;

            // Three of the ring's own nodes and the one circle through them. <b>Three that are actually
            // different</b>: the pieces are membership rather than a walk round the circle, so the first
            // two ends of the first piece and whatever the next piece adds is a triangle, and taking the
            // last piece's far end could be the first piece's near one back again.
            Span<int> corners = [CityPlan.NoRecord, CityPlan.NoRecord, CityPlan.NoRecord];
            var found = 0;
            foreach (var on in roads)
            {
                foreach (var node in (int[])[ground.Roads.FromJunction[on], ground.Roads.ToJunction[on]])
                {
                    if (corners.IndexOf(node) >= 0) continue;

                    corners[found++] = node;
                    if (found == 3) break;
                }

                if (found == 3) break;
            }

            // A ring of fewer than three nodes is not a circle the layout lays (GEN-19).
            if (found < 3) return null;

            return Through(
                ground.Junctions.CentreM[corners[0]],
                ground.Junctions.CentreM[corners[1]],
                ground.Junctions.CentreM[corners[2]]);
        }

        return null;
    }

    /// <summary>The centre of the circle through three points, or the middle of them where they are in a line.</summary>
    static Vector2 Through(Vector2 one, Vector2 two, Vector2 three)
    {
        var twice = 2f * ((one.X * (two.Y - three.Y)) + (two.X * (three.Y - one.Y)) + (three.X * (one.Y - two.Y)));
        if (MathF.Abs(twice) < 1e-6f) return (one + two + three) / 3f;

        var oneSq = one.LengthSquared();
        var twoSq = two.LengthSquared();
        var threeSq = three.LengthSquared();

        return new Vector2(
            ((oneSq * (two.Y - three.Y)) + (twoSq * (three.Y - one.Y)) + (threeSq * (one.Y - two.Y))) / twice,
            ((oneSq * (three.X - two.X)) + (twoSq * (one.X - three.X)) + (threeSq * (two.X - one.X))) / twice);
    }
}
