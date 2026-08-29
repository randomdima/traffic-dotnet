using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Runtime;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Debug;

/// <summary>What is drawn over the town rather than over an agent — the collision shapes and the networks, relaid only when the view has moved.</summary>
internal sealed partial class DebugOverlay
{
    /// <summary>
    /// The shapes the solver actually holds — a circle for a person and a prop, a rounded box for a car
    /// (CAR-12b), and for a building the rectangles its roof is built of (OBJ-5a) — centred on their
    /// bodies and at the size they were given. The statics are read off the plan rather than the solver:
    /// the same numbers, one step earlier.
    /// </summary>
    static void CollisionShapes(
        ref ScreenDraw draw, TownWorld world, SimConfig config, Vector2 viewCentreM, Vector2 viewSpanM,
        float pixelsPerMetre)
    {
        var lineM = MathF.Max(CollisionLineM, CollisionLineFloorPx / pixelsPerMetre);
        var plan = world.Plan;

        for (var prop = 0; prop < plan.Props.Count; prop++)
        {
            var centreM = plan.Props.CentreM[prop];
            var radiusM = plan.Props.RadiusM[prop];
            if (!OnScreen(centreM, viewCentreM, viewSpanM, radiusM)) continue;

            draw.RingM(centreM, radiusM, lineM, Theme.Collision, segments: 8);
        }

        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            var centreM = plan.Buildings.CentreM[building];
            if (!OnScreen(centreM, viewCentreM, viewSpanM, plan.Buildings.SizeM[building].Length() * 0.5f)) continue;

            var roof = BuildingRoofs.Of(plan, BuildingCatalog.Shared, world.Uses, building);
            ref readonly var variant = ref BuildingCatalog.Shared.Variants[roof.Variant];
            if (variant.PartsM.Length == 0)
            {
                draw.BoxM(centreM, roof.FootprintM, roof.HeadingRad, lineM, Theme.Collision);
                continue;
            }

            var scale = roof.FootprintM / variant.FootprintM;
            Heading.Frame(roof.HeadingRad, out var forward, out var right);
            foreach (var part in variant.PartsM)
            {
                var atM = part.AtM * scale;
                draw.BoxM(
                    centreM + (forward * atM.X) + (right * atM.Y), part.SizeM * scale, roof.HeadingRad,
                    lineM, Theme.Collision);
            }
        }

        var people = world.People;
        for (var person = 0; person < people.Count; person++)
        {
            var centreM = people.PositionM[person];
            if (!OnScreen(centreM, viewCentreM, viewSpanM, people.RadiusM[person])) continue;

            draw.RingM(centreM, people.RadiusM[person], lineM, Theme.Collision, segments: 12);
        }

        var cars = world.Cars;
        for (var car = 0; car < cars.Count; car++)
        {
            var centreM = cars.PositionM[car];
            ref readonly var build = ref cars.BuildOf(car);
            if (!OnScreen(centreM, viewCentreM, viewSpanM, build.HalfLengthM)) continue;

            // The shape the solver is given, which is this car's own (CAR-11) — fitted inside its picture
            // (CAR-12b), so it is inside the bodywork as well as inside the tyres and the mirrors.
            draw.RoundedBoxM(
                centreM, build.CollisionSizeM, cars.HeadingRad[car], build.CornerRadiusM, lineM, Theme.Collision);
        }
    }

    /// <summary>
    /// The town's own graphs, laid into the cache: the global nodes under the nodes switch, which is
    /// not switched with a body.
    /// </summary>
    void RelayIfStale(
        TownWorld world, SimConfig config, DebugSwitches switches, Vector2 viewCentreM, Vector2 viewSpanM,
        float pixelsPerMetre)
    {
        var marginM = viewSpanM * MarginFraction;
        var inside = Vector2.Abs(viewCentreM - _drawnCentreM) + viewSpanM * 0.5f;
        var stale = switches.Generation != _drawnGeneration
                    || pixelsPerMetre != _drawnPixelsPerMetre
                    || inside.X > _drawnSpanM.X * 0.5f || inside.Y > _drawnSpanM.Y * 0.5f;
        if (!stale) return;

        _drawnGeneration = switches.Generation;
        _drawnPixelsPerMetre = pixelsPerMetre;
        _drawnCentreM = viewCentreM;
        _drawnSpanM = viewSpanM + marginM * 2f;
        Relaid = true;

        var into = new ScreenDraw(_town);
        if (switches.Nodes) Nodes(ref into, world, config, _drawnCentreM, _drawnSpanM, pixelsPerMetre);
        _townQuads = into.Written;
    }

    /// <summary>
    /// The nodes switch: both networks' <em>global</em> nodes and the links between them — the places
    /// a body can go more than one way, which is what the router plans over.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A link is drawn as the ground it is travelled over, not as the straight line between its ends: a
    /// link contracts many pieces, and the straight runs through buildings, over verges and across
    /// junction boxes. What is walked is the run's own pieces, which are the lanes the follower holds.
    /// </para>
    /// <para>
    /// The driving network's junctions carry the movements as well as the node — the arcs a car may
    /// drive through the box, on the same biarc the assembler lays. Without them the layer says a car
    /// crosses a junction by teleporting between two lane ends.
    /// </para>
    /// </remarks>
    void Nodes(
        ref ScreenDraw draw, TownWorld world, SimConfig config, Vector2 viewCentreM, Vector2 viewSpanM,
        float pixelsPerMetre)
    {
        const float discM = NodeDiscM;
        var pitchM = PathMarks.MarkPitchAt(pixelsPerMetre);
        var sagM = PathMarks.SagPx / pixelsPerMetre;

        // Every piece of every run, rather than every lane of the town: the two come to the same
        // ground where a network covers it, and a piece no link is travelled over is ground this
        // layer's question is not about.
        var roads = world.Roads;
        var driving = world.Driving.Runs;
        for (var link = 0; link < driving.LinkCount; link++)
        {
            foreach (var lane in driving.PiecesOf(link))
            {
                // Between the two setbacks its own ends carry, which is the stretch of it anything drives:
                // the rest is under the movements through the box, and drawn as lane as well it is the same
                // spur past every corner the walking side had.
                Chain(
                    ref draw, roads.ArcsOf(lane), roads.JoinedAtM(lane), roads.LaneLengthM[lane] - roads.LeftAtM(lane),
                    sagM, pitchM, bothWays: false, Theme.DrivingNodes, viewCentreM, viewSpanM);
            }
        }

        Movements(ref draw, roads, config, sagM, pitchM, Theme.DrivingNodes, viewCentreM, viewSpanM);
        BayApproaches(ref draw, world.BayWays, sagM, pitchM, Theme.DrivingNodes, viewCentreM, viewSpanM);

        // Each stretch between the stations a walk actually joins and leaves its lane at, because the
        // mitres below cover the rest: drawn whole, the ground either side of every node is drawn twice
        // and a spur of it runs past the corner into the node, which is a walk nobody walks.
        var walking = world.Walking;
        var runs = walking.Runs;
        for (var link = 0; link < runs.LinkCount; link++)
        {
            foreach (var edge in runs.PiecesOf(link))
            {
                Chain(
                    ref draw, walking.LaneOf(edge), walking.WalkedFromM(edge), walking.WalkedToM(edge), sagM, pitchM,
                    OnOneLine(walking, edge), Theme.WalkingNodes, viewCentreM, viewSpanM);
            }
        }

        Mitres(ref draw, world, sagM, pitchM, Theme.WalkingNodes, viewCentreM, viewSpanM);

        Discs(ref draw, world.Driving.Graph, discM, Theme.DrivingNodes, viewCentreM, viewSpanM);
        Discs(ref draw, world.Walking.Graph, discM * 0.8f, Theme.WalkingNodes, viewCentreM, viewSpanM);
    }

    /// <summary>
    /// The corners of the walking network: from each stretch arriving at a node, the town's own mitre
    /// onto each stretch it may leave for, read off the network rather than laid again here. Turning
    /// round on the spot is left out — drawn, it is a ring at every node, over the corners worth seeing.
    /// </summary>
    /// <remarks>
    /// Dotted where a mitre meets a stretch, as a junction movement is (<see cref="Movements"/>). Where
    /// the two networks differ is what the dots then show: <b>a pavement hands over at a point per turn</b>
    /// rather than at one point per end, because a walk running straight on through a node gives up no
    /// ground while one turning off it gives up a corner's margin — so a corner carries a dot for the
    /// straight and a dot a margin back for the turn, and a lane end carries one.
    /// </remarks>
    static void Mitres(
        ref ScreenDraw draw, TownWorld world, float sagM, float pitchM, Vector4 colour, Vector2 viewCentreM,
        Vector2 viewSpanM)
    {
        var foot = world.Foot;
        var walking = world.Walking;
        for (var edge = 0; edge < foot.EdgeCount; edge++)
        {
            var turns = walking.TurnsFrom(edge);
            for (var turn = 0; turn < turns.Length; turn++)
            {
                // The turn-around, and the corner the lane carries: one is a walk nobody makes and the
                // other is already drawn, as the bend at the end of the stretch that owns it.
                if (turns[turn] == foot.Reverse(edge) || walking.TurnSlotAt(edge, turn) == walking.TailOf(edge)) continue;

                // A mitre lies on top of the mitre back the other way exactly when both the lanes it joins
                // do: it is the corner between their two lines, and where those are single lines so is it.
                Link(
                    ref draw, walking.JoinArcs(walking.TurnSlotAt(edge, turn)), sagM, pitchM,
                    OnOneLine(walking, edge) && OnOneLine(walking, turns[turn]), colour, viewCentreM, viewSpanM);
            }
        }
    }

    /// <summary>
    /// Whether the two directions of a stretch are laid on one line, which is the town's own answer and not
    /// a shape read back off the picture: the offset it was laid at <em>is</em> how far apart they are
    /// (<see cref="World.Foot.WalkingNetwork.LaneOffsetM"/>), and it is nought where the ground had no room
    /// for a lane either side.
    /// </summary>
    static bool OnOneLine(WalkingNetwork walking, int edge) => walking.LaneOffsetM(edge) * 2f < OneLineApartM;

    /// <summary>
    /// The movements through every junction: from each lane arriving, the town's own join onto each lane
    /// it may leave for, read off the graph rather than drawn again here. What the graph holds is what a
    /// car may drive, so the picture is drawn straight off it (TER-5f).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Where a movement leaves a lane and where it lands on one is dotted</b>, at the size an agent dots
    /// the same places on its own route — they are the same places, and a car's route crossing a junction
    /// should put its dots on the town's.
    /// </para>
    /// <para>
    /// Every movement sharing a lane end dots one point on top of another, which is the picture of
    /// `TER-5d` and not a waste: <b>a lane has one end</b>, and a junction that ever grew a second entry
    /// would show two dots where there is one.
    /// </para>
    /// </remarks>
    static void Movements(
        ref ScreenDraw draw, RoadGraph roads, SimConfig config, float sagM, float pitchM, Vector4 colour,
        Vector2 viewCentreM, Vector2 viewSpanM)
    {
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            var end = roads.EndOf(lane);
            if (!OnScreen(end.PositionM, viewCentreM, viewSpanM, config.CarTurningRadiusM * 2f)) continue;

            for (var turn = 0; turn < roads.TurnKindsFrom(lane).Length; turn++)
            {
                Link(
                    ref draw, roads.JoinArcs(roads.TurnSlotAt(lane, turn)), sagM, pitchM, bothWays: false, colour,
                    viewCentreM, viewSpanM);
            }
        }
    }

    /// <summary>
    /// The ways at every bay — for each lane it is worked off, the line a car is driven in on and the same
    /// line it is backed out on — read off the town rather than laid again here, and drawn exactly as a
    /// junction's movements are (<see cref="Movements"/>). They are movements of the same network: a way off
    /// a lane, onto ground that is not a lane, with a dot where it leaves the road and a dot where it
    /// arrives.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Drawn in the driving colour and not a colour of their own</b>, because that is what they are —
    /// ways of the same book, in the same table of what is driven over what. A layer that gave a car park
    /// its own colour would be saying a bay is a different kind of thing, which is the reading this whole
    /// slice exists to refuse.
    /// </para>
    /// <para>
    /// <b>The reversed way takes the shade of the same colour that says the car is going backwards down
    /// it</b> (<see cref="Theme.DrivingReverse"/>). A pair covers one piece of ground, so drawn in one
    /// colour they are a single line whose chevrons cross each other — and which way a car is pointing while
    /// it travels them is the whole of what tells them apart and the whole of what this picture is opened
    /// for. Which of a pair that is is the standing's (GEN-4j): a bay backed into is driven into in reverse
    /// and out of under power, so its pair is shaded the other way round from a bay nosed into.
    /// </para>
    /// </remarks>
    static void BayApproaches(
        ref ScreenDraw draw, BayWays ways, float sagM, float pitchM, Vector4 colour, Vector2 viewCentreM,
        Vector2 viewSpanM)
    {
        for (var way = ways.FirstWay; way < ways.TotalWayCount; way++)
        {
            Link(
                ref draw, ways.ArcsOf(way), sagM, pitchM, bothWays: false,
                ways.IsDrivenInReverse(way) ? Theme.DrivingReverse : colour, viewCentreM, viewSpanM);
        }
    }

    static void Discs(
        ref ScreenDraw draw, World.Routing.TravelGraph graph, float discM, Vector4 colour, Vector2 viewCentreM,
        Vector2 viewSpanM)
    {
        for (var node = 0; node < graph.NodeCount; node++)
        {
            var atM = graph.AnchorOf(node);
            if (!OnScreen(atM, viewCentreM, viewSpanM, discM)) continue;

            draw.DiscM(atM, discM, colour);
        }
    }

    /// <summary>
    /// One way a network gets from one of its stretches onto another — a junction movement, a pavement
    /// mitre — as the line it is with <b>a dot at each end</b>: the place it leaves the stretch behind it
    /// and the place it meets the one ahead, at the size an agent dots the same places on its own route.
    /// </summary>
    /// <remarks>
    /// The cull is the link's and not the line's: a dot drawn outside it is one quad, and there are two
    /// per movement in the town.
    /// </remarks>
    static void Link(
        ref ScreenDraw draw, ReadOnlySpan<ArcSeg> arcs, float sagM, float pitchM, bool bothWays, Vector4 colour,
        Vector2 viewCentreM, Vector2 viewSpanM)
    {
        if (arcs.Length == 0) return;

        var headM = arcs[0].StartM;
        var tailM = arcs[^1].EndM;
        if (!OnScreen((headM + tailM) * 0.5f, viewCentreM, viewSpanM, (tailM - headM).Length() * 0.5f)) return;

        PathMarks.Chained(ref draw, arcs, 0f, Spline.TotalLengthM(arcs), pitchM, bothWays, sagM, colour);
        draw.DiscM(headM, PathMarks.JoinDiscM, colour);
        draw.DiscM(tailM, PathMarks.JoinDiscM, colour);
    }

    /// <summary>
    /// A chain of arcs as the line it is, with marks down it at a pitch on the ground. A cull that
    /// admits a body is not a cull that admits its whole line, so the chain is rejected coarsely on its
    /// ends before it is sampled finely.
    /// </summary>
    static void Chain(
        ref ScreenDraw draw, ReadOnlySpan<ArcSeg> arcs, float sagM, float pitchM, bool bothWays, Vector4 colour,
        Vector2 viewCentreM, Vector2 viewSpanM)
    {
        if (arcs.Length == 0) return;

        Chain(
            ref draw, arcs, 0f, Spline.TotalLengthM(arcs), sagM, pitchM, bothWays, colour, viewCentreM, viewSpanM);
    }

    /// <summary>
    /// The same, between two stations along it — which is what a lane comes to once the corners at its two
    /// ends have taken their ground. How much of the chain is drawn does not move a single mark: they stand
    /// on the town's comb (<see cref="PathMarks.FirstMarkM"/>), which is the same comb an agent's own layer marks the
    /// same ground on.
    /// </summary>
    static void Chain(
        ref ScreenDraw draw, ReadOnlySpan<ArcSeg> arcs, float fromM, float toM, float sagM, float pitchM,
        bool bothWays, Vector4 colour, Vector2 viewCentreM, Vector2 viewSpanM)
    {
        if (arcs.Length == 0 || toM <= fromM) return;

        var headM = Spline.SampleAt(arcs, fromM).PositionM;
        var tailM = Spline.SampleAt(arcs, toM).PositionM;
        var reachM = (tailM - headM).Length() * 0.5f;
        if (!OnScreen((headM + tailM) * 0.5f, viewCentreM, viewSpanM, reachM)) return;

        PathMarks.Chained(ref draw, arcs, fromM, toM, pitchM, bothWays, sagM, colour);
    }
}
