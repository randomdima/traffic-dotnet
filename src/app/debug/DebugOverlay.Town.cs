using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Runtime;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Debug;

/// <summary>What is drawn over the town rather than over an agent — the collision shapes and the networks, relaid only when the view has moved.</summary>
internal sealed partial class DebugOverlay
{
    /// <summary>
    /// The shapes the solver actually holds — a circle for a person and a prop, a box for a car and a
    /// building — centred on their bodies and at the size it was given. The statics are read off the
    /// plan rather than the solver: the same numbers, one step earlier.
    /// </summary>
    static void CollisionShapes(
        ref ScreenDraw draw, TownWorld world, SimConfig config, Vector2 viewCentreM, Vector2 viewSpanM)
    {
        const float lineM = CollisionLineM;
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
            var sizeM = plan.Buildings.SizeM[building];
            if (!OnScreen(centreM, viewCentreM, viewSpanM, sizeM.Length() * 0.5f)) continue;

            draw.BoxM(centreM, sizeM, plan.Buildings.HeadingRad[building], lineM, Theme.Collision);
        }

        var people = world.People;
        for (var person = 0; person < people.Count; person++)
        {
            var centreM = people.PositionM[person];
            if (!OnScreen(centreM, viewCentreM, viewSpanM, people.RadiusM[person])) continue;

            draw.RingM(centreM, people.RadiusM[person], lineM, Theme.Collision, segments: 12);
        }

        var cars = world.Cars;
        var carSizeM = new Vector2(config.Car.LengthM, config.Car.WidthM);
        for (var car = 0; car < cars.Count; car++)
        {
            var centreM = cars.PositionM[car];
            if (!OnScreen(centreM, viewCentreM, viewSpanM, config.Car.LengthM * 0.5f)) continue;

            draw.BoxM(centreM, carSizeM, cars.HeadingRad[car], lineM, Theme.Collision);
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
        var pitchM = MarkPitchAt(pixelsPerMetre);
        var sagM = SagPx / pixelsPerMetre;

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
                    sagM, pitchM, Theme.DrivingNodes, viewCentreM, viewSpanM);
            }
        }

        Movements(ref draw, roads, config, sagM, pitchM, Theme.DrivingNodes, viewCentreM, viewSpanM);

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
                    Theme.WalkingNodes, viewCentreM, viewSpanM);
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

                Link(
                    ref draw, walking.JoinArcs(walking.TurnSlotAt(edge, turn)), sagM, pitchM, colour, viewCentreM,
                    viewSpanM);
            }
        }
    }

    /// <summary>
    /// The movements through every junction: from each lane arriving, the town's own join onto each lane
    /// it may leave for, read off the graph rather than drawn again here. The turn-around is left out —
    /// a semicircle no car could hold would be the one movement in the picture that is not drivable.
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

            var kinds = roads.TurnKindsFrom(lane);
            for (var turn = 0; turn < kinds.Length; turn++)
            {
                if (kinds[turn] == LaneTurn.TurnAround) continue;

                Link(
                    ref draw, roads.JoinArcs(roads.TurnSlotAt(lane, turn)), sagM, pitchM, colour, viewCentreM,
                    viewSpanM);
            }
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
        ref ScreenDraw draw, ReadOnlySpan<ArcSeg> arcs, float sagM, float pitchM, Vector4 colour, Vector2 viewCentreM,
        Vector2 viewSpanM)
    {
        if (arcs.Length == 0) return;

        var headM = arcs[0].StartM;
        var tailM = arcs[^1].EndM;
        if (!OnScreen((headM + tailM) * 0.5f, viewCentreM, viewSpanM, (tailM - headM).Length() * 0.5f)) return;

        Chained(ref draw, arcs, 0f, Spline.TotalLengthM(arcs), anchorM: 0f, pitchM, sagM, colour);
        draw.DiscM(headM, JoinDiscM, colour);
        draw.DiscM(tailM, JoinDiscM, colour);
    }

    /// <summary>
    /// A chain of arcs as the line it is, with marks down it at a pitch on the ground. A cull that
    /// admits a body is not a cull that admits its whole line, so the chain is rejected coarsely on its
    /// ends before it is sampled finely.
    /// </summary>
    static void Chain(
        ref ScreenDraw draw, ReadOnlySpan<ArcSeg> arcs, float sagM, float pitchM, Vector4 colour, Vector2 viewCentreM,
        Vector2 viewSpanM)
    {
        if (arcs.Length == 0) return;

        Chain(ref draw, arcs, 0f, Spline.TotalLengthM(arcs), sagM, pitchM, colour, viewCentreM, viewSpanM);
    }

    /// <summary>
    /// The same, between two stations along it — which is what a lane comes to once the corners at its two
    /// ends have taken their ground. <b>The marks are counted from the chain's own zero and not from the
    /// stretch drawn</b>, which is the same count an agent's own layer makes along the same ground: a mark
    /// stands where the metres say, whoever is drawing and however much of the chain they are drawing.
    /// </summary>
    static void Chain(
        ref ScreenDraw draw, ReadOnlySpan<ArcSeg> arcs, float fromM, float toM, float sagM, float pitchM,
        Vector4 colour, Vector2 viewCentreM, Vector2 viewSpanM)
    {
        if (arcs.Length == 0 || toM <= fromM) return;

        var headM = Spline.SampleAt(arcs, fromM).PositionM;
        var tailM = Spline.SampleAt(arcs, toM).PositionM;
        var reachM = (tailM - headM).Length() * 0.5f;
        if (!OnScreen((headM + tailM) * 0.5f, viewCentreM, viewSpanM, reachM)) return;

        Chained(ref draw, arcs, fromM, toM, anchorM: 0f, pitchM, sagM, colour);
    }
}
