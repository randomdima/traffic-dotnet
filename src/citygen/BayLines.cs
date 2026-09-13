using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The lines a car is driven into and out of a bay on</b> (GEN-4f), laid with the town beside the lines
/// it is driven through a junction on. A car park is the union of the movements that reach into it and has
/// no shape of its own — the same thing TER-5 says of a junction — so these are what its ground is drawn
/// and answered from, and there is no rectangle anywhere.
/// </summary>
/// <remarks>
/// <para>
/// <b>Laid here because the ground is laid here.</b> What the tarmac is is the plan's
/// (<see cref="GroundShapes"/>) and what draws it reads the plan, so a movement the ground is made of has
/// to be a movement the plan carries. Laid a tier up and handed back down, the picture would draw shapes
/// the answer had never heard of.
/// </para>
/// <para>
/// <b>At the nominal car's figures</b> (CAR-11a): <see cref="SimConfig.CarParkingTemplateRadiusM"/> and
/// the rest are the town's own, so a bay's way is the same for whoever turns up. A car that cannot hold
/// the shape lays its own from where it is standing (<c>ManeuverDesk.LayTheExitLine</c>) — this is the
/// recommendation and not the instruction.
/// </para>
/// <para>
/// <b>Two rows over one piece of ground</b>: a way in and the same line walked back out, because a way's
/// metres run in the direction it is driven. The far lane keeps only the one the car is under power for —
/// a car may nose into a bay across the carriageway and drive out of one across it, and reverses over
/// neither.
/// </para>
/// </remarks>
internal sealed class BayLines
{
    /// <summary>A straight, a swing, the turn, the run in, and the run on past the pose.</summary>
    public const int MostArcsAWayTakes = BayTemplate.MostArcs + 1;

    readonly int[] _firstWayOfBay;
    readonly int[] _arcOffsets;

    BayLines(
        int[] firstWayOfBay, int[] bay, int[] lane, float[] atLaneM, float[] lengthM, float[] drivenM,
        bool[] isEntry, bool[] isNoseIn, int[] arcOffsets, ArcSeg[] arcs, Vector2[] atTheBayM)
    {
        _firstWayOfBay = firstWayOfBay;
        _arcOffsets = arcOffsets;
        Bay = bay;
        Lane = lane;
        AtLaneM = atLaneM;
        LengthM = lengthM;
        DrivenM = drivenM;
        IsEntry = isEntry;
        IsNoseIn = isNoseIn;
        Arcs = arcs;
        AtTheBayM = atTheBayM;
    }

    public int Count => Bay.Length;

    /// <summary>The bay each way serves.</summary>
    public int[] Bay { get; }

    /// <summary>The lane it leaves, for a way in, or arrives on, for a way out.</summary>
    public int[] Lane { get; }

    /// <summary>And how far along that lane it does so.</summary>
    public float[] AtLaneM { get; }

    /// <summary>The way's own metres end to end, which for a way in run past the pose to the end of the space.</summary>
    public float[] LengthM { get; }

    /// <summary>And how much of it is driven.</summary>
    public float[] DrivenM { get; }

    public bool[] IsEntry { get; }

    public bool[] IsNoseIn { get; }

    /// <summary>Where the axle comes to rest in the bay this way serves.</summary>
    public Vector2[] AtTheBayM { get; }

    public ArcSeg[] Arcs { get; }

    /// <summary>The most arcs any one way took, for a caller sizing a buffer to walk them by.</summary>
    public int MostArcs { get; private init; }

    public ReadOnlySpan<ArcSeg> ArcsOf(int way) =>
        Arcs.AsSpan(_arcOffsets[way], _arcOffsets[way + 1] - _arcOffsets[way]);

    /// <summary>The ways one bay is worked off, as the run of way numbers they are.</summary>
    public ReadOnlySpan<int> WaysOf(int bay) =>
        Bay.AsSpan(_firstWayOfBay[bay], _firstWayOfBay[bay + 1] - _firstWayOfBay[bay]);

    /// <summary>
    /// <b>The ways that carry ground, each piece of it once</b>. A way in and the way out beside it are one
    /// line walked both directions — a way's metres run the way it is driven — so the ground under them is
    /// one band, and laid twice it is two coincident outlines for the walk to trip over. The way in is the
    /// one kept, being the longer of the two: it runs on past the pose to the end of the space.
    /// </summary>
    public int[] GroundWays { get; private init; } = [];

    /// <summary>Where each bay's run of ways begins, and one past the last — the shape a run is read by.</summary>
    public int[] FirstWayOfBay => _firstWayOfBay;

    /// <summary>And where each way's own arcs begin in <see cref="Arcs"/>.</summary>
    public int[] ArcOffsets => _arcOffsets;

    public int FirstWayOf(int bay) => _firstWayOfBay[bay];

    public int WayCountOf(int bay) => _firstWayOfBay[bay + 1] - _firstWayOfBay[bay];

    public static BayLines Lay(GroundPieces pieces, LaneLines lanes, SimConfig config)
    {
        var lots = pieces.ParkingLots;
        var firstWayOfBay = new int[lots.SpaceCount + 1];
        if (lots.SpaceCount == 0 || lanes.LaneCount == 0)
        {
            return new BayLines(firstWayOfBay, [], [], [], [], [], [], [], [0], [], []) { MostArcs = 0 };
        }

        var radiusM = config.CarParkingTemplateRadiusM;
        var settlesM = config.CarParkingStraightensUpM;
        var centreAheadM = config.CarCentreAheadOfAxleM;

        var builder = new ChainIndex.Builder();
        for (var lane = 0; lane < lanes.LaneCount; lane++)
        {
            builder.Add(lane, lanes.ArcsOf(lane), lanes.LaneLengthM[lane]);
        }

        var nearest = builder.Seal(config.NearestChainCellM);

        var bay = new List<int>();
        var lane2 = new List<int>();
        var atLaneM = new List<float>();
        var lengthM = new List<float>();
        var drivenM = new List<float>();
        var isEntry = new List<bool>();
        var isNoseIn = new List<bool>();
        var arcOffsets = new List<int> { 0 };
        var arcs = new List<ArcSeg>();
        var atTheBayM = new List<Vector2>();
        var drawn = new ArcSeg[BayTemplate.MostArcs];
        var shifted = new ArcSeg[BayTemplate.MostArcs];
        var backwards = new ArcSeg[BayTemplate.MostArcs];
        var most = 0;
        var groundWays = new List<int>();

        for (var space = 0; space < lots.SpaceCount; space++)
        {
            firstWayOfBay[space] = bay.Count;

            var headingRad = lots.SpaceHeadingRad[space];
            var centreM = lots.SpacePositionM[space];

            var nearLane = nearest.Nearest(
                BayTemplate.RearAxleOfBayM(centreAheadM, centreM, headingRad, true), out _);
            if (nearLane < 0) continue;

            var farLane = lanes.LaneReverse[nearLane];

            // <b>The near lane is what a standing is settled off</b> (GEN-4j), because it is the only lane
            // a car may reverse to or from, and a standing without both its ways is a car that parks and
            // never leaves.
            var standsNoseIn = Settle(space, headingRad, centreM, nearLane, noseIn: true, forwardsOnly: false);
            var standsBackedIn = Settle(space, headingRad, centreM, nearLane, noseIn: false, forwardsOnly: false);

            if (farLane < 0) continue;

            // The oncoming lane, asked the same question and kept only where the answer is driven forwards:
            // a car may nose into a bay across the carriageway and drive out of one across it, and reverses
            // over neither.
            if (standsNoseIn)
            {
                Settle(space, headingRad, centreM, farLane, noseIn: true, forwardsOnly: true);
            }

            if (standsBackedIn)
            {
                Settle(space, headingRad, centreM, farLane, noseIn: false, forwardsOnly: true);
            }
        }

        firstWayOfBay[lots.SpaceCount] = bay.Count;

        return new BayLines(
            firstWayOfBay, [.. bay], [.. lane2], [.. atLaneM], [.. lengthM], [.. drivenM], [.. isEntry],
            [.. isNoseIn], [.. arcOffsets], [.. arcs], [.. atTheBayM])
        {
            MostArcs = most, GroundWays = [.. groundWays],
        };

        // One candidate lane and one standing, asked the one question the template answers: is there a
        // shape between that lane and the pose a car standing that way round holds.
        bool Settle(int space, float headingRad, Vector2 centreM, int candidate, bool noseIn, bool forwardsOnly)
        {
            var axleM = BayTemplate.RearAxleOfBayM(centreAheadM, centreM, headingRad, noseIn);
            var line = lanes.ArcsOf(candidate);
            var laneLengthM = lanes.LaneLengthM[candidate];
            var abeamM = Spline.ProjectM(line, axleM, laneLengthM * 0.5f, laneLengthM);

            // Nosing in, the car comes up the lane and turns off it short of the bay. Backing in, it has
            // driven past the bay first, so the shape is staged beyond it and the axle travels back down
            // the lane before it turns — the same template, asked with the lane the other way round.
            var stagedInM = noseIn ? -config.ParkingStagedInM : config.ParkingStagedInM;
            var stagedM = abeamM + stagedInM;
            if (stagedM < 0f || stagedM > laneLengthM) return false;

            var laid = LayFrom(line, stagedM, drawn, out var runsOnM);
            if (!laid.Any) return false;

            // <b>The way begins where the car stops driving down the lane</b>, and the metres before that
            // are the lane's own. Staged from a fixed place, the way opens with a straight lying on the
            // line it left — a stretch the route would have driven anyway, held twice, and driven back up
            // on the way out for no reason but that it was written down. The shape from the nearer pose is
            // kept only where it lays: a lane that bends over the run-in moves the pose across as well as
            // along, and there the way staged where it was asked for is the one the town has.
            var turnsInM = noseIn ? stagedM + runsOnM : stagedM - runsOnM;
            if (turnsInM >= 0f && turnsInM <= laneLengthM)
            {
                var closer = LayFrom(line, turnsInM, shifted, out _);
                if (closer.Any)
                {
                    laid = closer;
                    stagedM = turnsInM;
                    shifted.CopyTo(drawn.AsSpan());
                }
            }

            // <b>How far the way runs on past the pose</b> (GEN-4f): to the far end of the space, measured
            // along the bay's own bearing, which is the direction the axle is travelling at the end of the
            // shape whichever way round the car stands. It is the ground a body in the space can be standing
            // on and nothing drives it — a nose-in car's own bonnet is inside it, and so is anybody walking
            // in front of that car.
            var pastThePoseM = MathF.Max(
                0f, (config.ParkingSpaceLengthM * 0.5f) - Vector2.Dot(axleM - centreM, Heading.Unit(headingRad)));

            Spline.ReverseInto(drawn.AsSpan(0, laid.ArcCount), backwards);
            var laidTheWayIn = !forwardsOnly || noseIn;
            if (laidTheWayIn)
            {
                Add(space, candidate, stagedM, axleM, laid, drawn, entry: true, noseIn, headingRad, pastThePoseM);
                groundWays.Add(bay.Count - 1);
            }

            if (!forwardsOnly || !noseIn)
            {
                Add(space, candidate, stagedM, axleM, laid, backwards, entry: false, noseIn, headingRad, 0f);
                if (!laidTheWayIn) groundWays.Add(bay.Count - 1);
            }

            return true;

            // The template from a place on the lane, in the direction the axle travels from there.
            BayLine LayFrom(ReadOnlySpan<ArcSeg> onLane, float alongM, ArcSeg[] into, out float runsOnM)
            {
                var from = Spline.SampleAt(onLane, alongM);
                var travelRad = noseIn ? from.HeadingRad : from.HeadingRad + MathF.PI;
                return BayTemplate.TryLay(
                    radiusM, settlesM, from.PositionM, travelRad, axleM, headingRad, into, out runsOnM);
            }
        }

        void Add(
            int space, int onLane, float onLaneM, Vector2 axleM, in BayLine laid, ArcSeg[] drawnAs, bool entry,
            bool noseIn, float bayHeadingRad, float pastThePoseM)
        {
            bay.Add(space);
            lane2.Add(onLane);
            atLaneM.Add(onLaneM);
            atTheBayM.Add(axleM);
            lengthM.Add(laid.LengthM + pastThePoseM);
            drivenM.Add(laid.LengthM);
            isEntry.Add(entry);
            isNoseIn.Add(noseIn);

            for (var arc = 0; arc < laid.ArcCount; arc++) arcs.Add(drawnAs[arc]);

            // <b>The run on to the end of the space, as the straight it is</b>: the shape ends square on the
            // bay's own bearing (<see cref="BayTemplate.TryLay"/>), so the ground past the pose carries
            // straight on down the same line. <b>A way out has none</b> — it begins at the pose, and ground
            // behind a car that is leaving is not ground its line covers; the way in is what carries the
            // space, and it is the one a driver aiming at the bay reads.
            //
            // <b>It is a piece of its own although it turns at nothing</b>, and a way into a bay is not
            // joined into the pieces it really turns at the way a movement is: where a chain is cut decides
            // which piece the nearest point on it is read off (<see cref="Spline.ProjectM"/>), that reading
            // is worth a millimetre at a town's coordinates, and which band of a car park's bundle is the
            // outermost at a place is settled inside two (<see cref="LaneShell"/>).
            if (pastThePoseM > 0f) arcs.Add(new ArcSeg(axleM, bayHeadingRad, pastThePoseM, 0f));

            arcOffsets.Add(arcs.Count);
            most = Math.Max(most, arcs.Count - arcOffsets[^2]);
        }
    }
}
