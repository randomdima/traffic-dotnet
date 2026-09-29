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
/// derived from the plan while nothing derived is stored. One function, two callers — the generator laying
/// a town and <see cref="Paving"/> deriving its lanes off the plan it produced — and the same answer,
/// because a seed and a link are all a point ever depended on.
/// </para>
/// <para>
/// <b>A link is its two junctions' own centres</b> and never an index. The layout's edge numbering does not
/// survive to the plan — the component keeping, the pruning, the joining and the roundabout opening all sit
/// between them, and <c>TownLayout.Rebuilt</c> renumbers the nodes as well — whereas a centre is the same
/// float on both sides. The two ends of one link are drawn independently, so a road has two bearings to
/// satisfy and they do not agree.
/// </para>
/// <para>
/// <b>Two kinds of link take their bearing rather than drawing one</b> (GEN-14a, GEN-19): a bridge is one
/// straight span and a ring piece is one arc of one circle, and both are settled before anything here runs.
/// Jittering a bridgehead would be jittering off a deck that cannot move.
/// </para>
/// <para>
/// <b>And one kind is read off the road rather than drawn at all</b> (GEN-52). A junction cut into a road
/// that already stands inverts the inversion: the line was there first, so both arms of a cut road are the
/// line's own ends and the lead is the arc that joins the node to each of them
/// (<see cref="ReadOff"/>). It is the one place the causality runs the other way, and it runs that way
/// because the whole point of a cut is that the road does not move.
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
    /// <param name="Curvature">
    /// How the arm's own lead bends, which is nought everywhere but on a roundabout's ring — where the lead
    /// is a piece of the circle GEN-19 sized rather than a straight off the tangent to it.
    /// </param>
    /// <param name="StandM">
    /// The middle of the line the arm's points stand on — a standoff out along the arm's own lead. The
    /// points themselves stand either side of it, half a lane off.
    /// </param>
    /// <param name="StandUnit">
    /// And the bearing there, which is the arm's own where the lead is straight and the circle's tangent
    /// where it is not.
    /// </param>
    /// <param name="OnTheLine">
    /// <b>Whether this arm's points stand on its own line rather than half a lane either side of it</b>
    /// (<see cref="Point"/>). Two roads are like that and no others: a roundabout's ring, which is the one
    /// road the town does not move onto the half its traffic drives (<c>RoadStage.OntoTheDrivenHalf</c>,
    /// GEN-19), and a bay's own way, whose two ways share one line (GEN-53).
    /// </param>
    internal readonly record struct Arm(
        int Junction, Vector2 NodeM, Vector2 OutwardUnit, float Curvature, Vector2 StandM, Vector2 StandUnit,
        bool OnTheLine = false)
    {
        /// <summary>The lead itself, which is what a road laid to this arm begins with and a movement ends on.</summary>
        public ArcSeg Lead(float standoffM) =>
            new(NodeM, MathF.Atan2(OutwardUnit.Y, OutwardUnit.X), standoffM, Curvature);
    }

    /// <summary>
    /// <b>The arm one road leaves one of its two junctions on.</b> The bearing is the chord to the other
    /// junction turned by a drawn angle inside <see cref="CityGenFigures.ConnectionJitterDeg"/>, except on
    /// the two kinds of link whose shape is already settled and on a road laid straight
    /// (<see cref="CityPlan.RoadArrays.LaidStraight"/>), which takes the chord.
    /// </summary>
    public static Arm ArmOf(GroundPieces ground, SimConfig config, int road, bool atFrom)
    {
        var roads = ground.Roads;
        var junction = atFrom ? roads.FromJunction[road] : roads.ToJunction[road];
        var other = atFrom ? roads.ToJunction[road] : roads.FromJunction[road];

        // <b>A cut road's arms are read off its own line</b> (GEN-52): its line stood before the junction at
        // one of its ends did, so there is nothing left to draw here and the bearing is what the line
        // already carries.
        if (roads.WasCut(road)) return ReadOff(ground, junction, road, atFrom);

        // <b>A road leaves its junction for the first place it passes</b> (GEN-51). Where it passes
        // nowhere that is its other junction, and where it does, aiming the arm at the far end would point
        // the carriageway somewhere it never goes — and leave the road a corner it cannot turn off its own
        // arm.
        var through = roads.ThroughOf(road);
        var towardM = through.Length > 0
            ? (atFrom ? through[0] : through[^1])
            : ground.Junctions.CentreM[other];

        var ring = Ringed(ground, road, atFrom, out var curvature);
        return ArmOf(
            ground.Seed, config, junction, ground.Junctions.CentreM[junction], towardM,
            ring || Array.IndexOf(ground.Bridges.Road, road) >= 0, curvature, roads.IsLaidStraight(road))
               with { OnTheLine = ring || roads.DrivenOverOneLine(road) };
    }

    /// <summary>
    /// <b>The arm of a cut road, read off the line it already carries</b> (GEN-52): the stand point is where
    /// that line ends, the bearing there is the line's own, and the lead is <b>the one arc that joins the
    /// node to it</b> (<see cref="Spline.ArcThrough"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One reading for both ends, and no case for either.</b> The end a junction was cut at stands a
    /// standoff along the road's own bend, so the arc back to the node is that bend; the end the road always
    /// had stands a standoff along a straight lead, so the arc back to the node is that straight. Asked as
    /// "which arc joins these two poses", the two come out of one line of arithmetic.
    /// </para>
    /// <para>
    /// <b>It is exact rather than near</b>, which is what the cut is held to (GEN-52): the node is placed a
    /// standoff of the road's <em>own</em> arc back from where the line was parted, so the lead this reads
    /// is the ground the road was already laid on and not a curve fitted through it.
    /// </para>
    /// </remarks>
    static Arm ReadOff(GroundPieces ground, int junction, int road, bool atFrom)
    {
        var arcs = ground.Roads.SegmentsOf(road);
        var nodeM = ground.Junctions.CentreM[junction];
        var standM = atFrom ? arcs[0].StartM : arcs[^1].EndM;
        var standUnit = atFrom
            ? arcs[0].StartUnit
            : -Heading.Unit(arcs[^1].HeadingAtRad(arcs[^1].LengthM));

        // The lead walked the other way: from the stand point back down the road to the node it stands off.
        var back = Spline.ArcThrough(standM, MathF.Atan2(-standUnit.Y, -standUnit.X), nodeM);
        var outward = -Heading.Unit(back.HeadingAtRad(back.LengthM));

        return new Arm(
            junction, nodeM, outward, -back.Curvature, standM, standUnit,
            ground.Roads.DrivenOverOneLine(road));
    }

    /// <summary>
    /// <b>The same arm off the figures a link is</b>, for the stage that draws them before there is a road to
    /// read them from (<c>RoadLines</c>): the node it stands at, the place its road leaves for, and the bend
    /// the lead carries where the shape was settled elsewhere.
    /// </summary>
    /// <param name="settled">
    /// Whether the bearing is the link's own to keep rather than one to draw (GEN-14a, GEN-19): a bridge
    /// leaves square along its span and a ring piece along its circle's tangent.
    /// </param>
    /// <param name="curvature">
    /// How the lead itself bends — the ring's own curvature at this end, and nought everywhere else. A
    /// standoff laid straight off a thirty-metre circle stands nearly two metres inside it, which is a break
    /// in the one shape GEN-19 sizes.
    /// </param>
    /// <param name="straight">
    /// Whether the road is laid straight (GEN-47), which leaves on the chord to the place it runs for and is
    /// not jittered off it.
    /// </param>
    public static Arm ArmOf(
        ulong seed, SimConfig config, int junction, Vector2 nodeM, Vector2 towardM, bool settled,
        float curvature, bool straight = false)
    {
        var outward = settled ? Tangent(nodeM, towardM, curvature)
            : straight ? Chord(nodeM, towardM)
            : Jittered(Chord(nodeM, towardM), config, seed, nodeM, towardM);

        var arm = new Arm(junction, nodeM, outward, curvature, nodeM, outward);
        var lead = arm.Lead(config.CityGen.ConnectionStandoffM);

        return arm with { StandM = lead.EndM, StandUnit = Heading.Unit(lead.HeadingAtRad(lead.LengthM)) };
    }

    /// <summary>
    /// <b>The bearing a line of this curvature leaves <paramref name="nodeM"/> on to reach
    /// <paramref name="towardM"/></b>: the chord turned back by half the sweep the arc makes over it, which
    /// at nought curvature is the chord itself.
    /// </summary>
    static Vector2 Tangent(Vector2 nodeM, Vector2 towardM, float curvature)
    {
        var chord = Chord(nodeM, towardM);
        if (curvature == 0f) return chord;

        var halfRad = MathF.Asin(
            MathF.Min(1f, Vector2.Distance(nodeM, towardM) * 0.5f * MathF.Abs(curvature))) * MathF.Sign(curvature);

        return Heading.Unit(MathF.Atan2(chord.Y, chord.X) - halfRad);
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
            into[written++] = Point(config, arm, arm.StandUnit, LaneEnd.Exit);
        }

        if (flow != (leavesHere ? RoadFlow.WithTheRoad : RoadFlow.AgainstTheRoad))
        {
            into[written++] = Point(config, arm, -arm.StandUnit, LaneEnd.Enter);
        }

        return written;
    }

    /// <inheritdoc cref="At(GroundPieces, SimConfig, in Arm, int, Span{ConnectionPoint})"/>
    public static int At(GroundPieces ground, SimConfig config, int road, bool atFrom, Span<ConnectionPoint> into) =>
        At(ground, config, ArmOf(ground, config, road, atFrom), road, into);

    /// <summary>
    /// One point of an arm: the stand line's middle, moved half a lane onto the side the traffic driving
    /// through it keeps.
    /// </summary>
    /// <remarks>
    /// <b>A ring arm carries no offset at all</b> (GEN-19). The circle a roundabout was sized as <em>is</em>
    /// the line its traffic is driven on — the ring is one way round and a lane wide, and moved half a lane
    /// off the circle its carriageway leaves the nodes its own arms end at. It is the one arm whose lane is
    /// the arm's own line rather than a share of a carriageway, and it is the same exception
    /// <c>RoadStage.OntoTheDrivenHalf</c> makes.
    /// </remarks>
    static ConnectionPoint Point(SimConfig config, in Arm arm, Vector2 drivenUnit, LaneEnd end)
    {
        var acrossM = arm.OnTheLine ? 0f : config.LaneOffsetM * config.RoadSideSign;
        return new ConnectionPoint(
            arm.StandM + (Heading.RightOf(drivenUnit) * acrossM), drivenUnit, end);
    }

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
        var boundRad = BoundRad(config, Vector2.Distance(nodeM, towardM));
        var turnedRad = draw.NextFloat(-boundRad, boundRad);

        var (sin, cos) = MathF.SinCos(turnedRad);
        return new Vector2((chord.X * cos) - (chord.Y * sin), (chord.X * sin) + (chord.Y * cos));
    }

    /// <summary>
    /// <b>How much of the ground between two arms a corner between them may take</b>, either side of the
    /// vertex it rounds — which is what says how long a lead has to be for a road to turn off its arm at
    /// all (<c>RoadStage.Chain</c>) and therefore how far an arm may be jittered on a link of any length.
    /// </summary>
    public const float TangentShareOfSegment = 0.45f;

    /// <summary>
    /// <b>How far off its chord a link this long may be jittered</b>: the authored bound, tapered by what
    /// the link has room to turn through.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A road drawn onto an arm turned by <c>θ</c> has to hold that bearing for a lead, then turn off it
    /// over a corner of radius <c>R</c>, and the corner may take only a share of the shorter of the two legs
    /// it stands between. <b>Both ends want one and the road wants what is left</b>, so the ground divides
    /// three ways — and the turn at a lead is up to <c>2θ</c>, because the far end was jittered too.
    /// That gives <c>θ ≤ atan(share·(L − 2·standoff) ∕ 3R)</c>.
    /// </para>
    /// <para>
    /// <b>Against the loosest class's floor and not the road's own</b>, because the road's class is not
    /// something the plan carries (§5) and the derivation has to draw the same angle the generator drew. An
    /// arterial's floor is the widest of them, so a link held inside it is one every class can turn.
    /// </para>
    /// </remarks>
    static float BoundRad(SimConfig config, float lengthM)
    {
        var boundRad = float.DegreesToRadians(config.CityGen.ConnectionJitterDeg);
        var betweenM = lengthM - (2f * config.CityGen.ConnectionStandoffM);
        if (betweenM <= 0f) return 0f;

        var floorM = config.CarCorneringRadiusM(
            config.CityGen.ArterialDesignSpeedMps, config.Terrain.PavedCoefficient);

        return MathF.Min(boundRad, MathF.Atan(TangentShareOfSegment * betweenM / (3f * floorM)));
    }

    /// <summary>
    /// The link's own key, as the bits of the four coordinates that name it. <b>Ordered</b>: the arm at one
    /// end and the arm at the other are two draws, and swapping the pair is how they are told apart.
    /// </summary>
    /// <remarks>
    /// <b>Everything a link is drawn with is keyed on this and never on a walk</b> (GEN-11): the arm's jitter
    /// and the road's own wander (<c>RoadLines</c>), so a link offered twice is drawn the same both times and a
    /// road deleted moves nothing that stayed.
    /// </remarks>
    public static ulong Keyed(Vector2 nodeM, Vector2 towardM)
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
    /// <b>Whether this road circulates on a roundabout, and what its lead bends at</b> (GEN-19): a ring piece
    /// is one arc of the circle its roundabout was sized as, and leaves along that circle's tangent. A bridge
    /// is the other road whose bearing is settled before its arms are drawn (GEN-14a), and it leaves square
    /// along its own straight chord, so it needs no bend of its own and is asked about at the call site.
    /// </summary>
    /// <remarks>
    /// <b>The ring's bend is the piece's own and is never fitted to its nodes.</b> A ring piece is laid as one
    /// arc of the circle (GEN-19, <c>RoadLines</c>), so the curvature the lead wants is the curvature that arc
    /// already carries — where fitting a circle through three of the ring's nodes is the same figure worked
    /// out a second way, off three points that only approximate it.
    /// </remarks>
    static bool Ringed(GroundPieces ground, int road, bool atFrom, out float curvature)
    {
        curvature = 0f;

        var rings = ground.Roundabouts;
        for (var ring = 0; ring < rings.Count; ring++)
        {
            if (rings.RoadsOf(ring).IndexOf(road) < 0) continue;

            // The arc as this arm leaves the node: the piece's own bend at its near end, and the same circle
            // turned the other way where the arm is the far end of it.
            var arcs = ground.Roads.SegmentsOf(road);
            if (arcs.Length > 0) curvature = atFrom ? arcs[0].Curvature : -arcs[^1].Curvature;

            return true;
        }

        return false;
    }
}
