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
    /// The geometry the town holds still, laid into the cache: the lanes and their connectors under the
    /// nodes switch, the tarmac's own outline under the perimeter, and the ground's own triangles under the
    /// wireframe. None of them is switched with a body and none moves once the town is laid, which is what
    /// they are cached for.
    /// </summary>
    void RelayIfStale(
        TownWorld world, GroundMesh? mesh, SimConfig config, DebugSwitches switches, Vector2 viewCentreM,
        Vector2 viewSpanM, float pixelsPerMetre)
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

        // <b>The ground first and the ruling over it</b>, both of them under every line: neither wash is
        // the thing being looked at, and what either is read against is whatever line lands on top of it.
        if (switches.Ribbons) Ribbons(ref into, world, config, _drawnCentreM, _drawnSpanM, pixelsPerMetre);

        // A cell wash over a lane says which cell the lane is in; a lane over a cell wash says the same
        // thing and leaves the lane the thing being looked at, which is the one of the two that moves.
        if (switches.Grid) Grid(ref into, world, config, _drawnCentreM, _drawnSpanM, pixelsPerMetre);

        // The solver's furniture on the same terms (OBS-2x): indexed once when the last static body was
        // added, so it belongs in the cache beside the geometry it is an index of. Its moving half cannot.
        if (switches.SolverGrid) SolverStatics(ref into, world, _drawnCentreM, _drawnSpanM, pixelsPerMetre);

        if (switches.Nodes) Nodes(ref into, world, config, _drawnCentreM, _drawnSpanM, pixelsPerMetre);

        // Over the graphs where both are on, which is the reading it exists for: what the layer says is
        // which of the lines under it the town's outline actually runs along.
        if (switches.Perimeter) Perimeter(ref into, world, config, _drawnCentreM, _drawnSpanM, pixelsPerMetre);

        // Over the town's own layers where both are on, which is what the probe is turned for: the reading
        // is the one line against the lines the picture was laid from (OBS-2w).
        if (switches.Shell.Drawn)
        {
            ProbedShell(
                ref into, world, config, switches.Shell, _drawnCentreM, _drawnSpanM, pixelsPerMetre);
        }

        // <b>Last, so a mesh dense enough to fill the buffer takes no quads off the layer beside it.</b> A
        // city's triangulation is more quads than the cache holds at any framing that admits it, and laid
        // first it would leave the nodes switch on and drawing nothing. The cost is that the hairlines
        // stand over the chevrons rather than under them, which at this weight is nothing to read.
        if (switches.Wireframe && mesh is not null)
        {
            Wireframe(ref into, mesh, switches.Ground.Shown, _drawnCentreM, _drawnSpanM, pixelsPerMetre);
        }

        _townQuads = into.Written;
    }

    /// <summary>
    /// The nodes switch: both networks as what they are — every lane, and every connector between two of
    /// them, which is the whole of what the router plans over.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>There is no disc for a junction, because the town holds none.</b> What the layer used to draw
    /// there was the anchor of a contracted travel node, and a picture with a dot in the middle of every
    /// box says the router plans between intersections when it plans between ways
    /// (<see cref="World.Routing.TravelGraph"/>). What is left is the lanes and the connectors, which is
    /// what a body actually chooses between.
    /// </para>
    /// <para>
    /// A movement is drawn as the ground it is travelled over, not as the straight line between its ends:
    /// what is driven is the connector's own biarc, the one the assembler lays, and the straight would run
    /// through the box. Without them the layer says a car crosses a junction by teleporting between two
    /// lane ends.
    /// </para>
    /// </remarks>
    void Nodes(
        ref ScreenDraw draw, TownWorld world, SimConfig config, Vector2 viewCentreM, Vector2 viewSpanM,
        float pixelsPerMetre)
    {
        var pitch = PathMarks.MarkPitchAt(config.Grid, pixelsPerMetre);
        var sagM = PathMarks.SagPx / pixelsPerMetre;

        // <b>One set of claims over both networks</b> (<see cref="MarkClaims"/>): a pavement runs a metre
        // off the carriageway it serves, so marks kept clear of each other only within a network still pile
        // a walk's chevrons onto a lane's.
        _marks.Clear(
            config.Grid, viewCentreM, viewSpanM, pitch is not null ? PathMarks.MarkApartM : float.PositiveInfinity);

        // <b>Every lane of the town, whole</b> (OBS-2d), which is the same thing as every lane between its
        // two connection points: a lane ends where its movements hand over (TER-5d), so the whole of its line
        // is ground it holds and no part of it is drawn under a join as well.
        var roads = world.Roads;
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            Chain(
                ref draw, roads.ArcsOf(lane), sagM, pitch, roads.LaneOverOneLine[lane], Theme.DrivingNodes,
                viewCentreM, viewSpanM, _marks);
        }

        Movements(ref draw, roads, config, sagM, pitch, Theme.DrivingNodes, viewCentreM, viewSpanM, _marks);
        BayApproaches(ref draw, world, sagM, pitch, Theme.DrivingNodes, viewCentreM, viewSpanM, _marks);

        // And on the walking side, every lane of the town's pavement as the graph holds it (WLK-1), which is
        // the same reading as the driving side above: the line a body is actually held on, walked its own way.
        PavementLanes(ref draw, world.Foot, sagM, pitch, viewCentreM, viewSpanM, _marks);
        Unwalked(
            ref draw, world.PavementLanes, viewCentreM, viewSpanM, sagM,
            PathMarks.BarbPitchAt(config.Grid, pixelsPerMetre));

        KerbEnds(ref draw, world.Plan.Paving(config).RoadEnds(config), viewCentreM, viewSpanM);
        CrossingPoints(ref draw, world.CrossingWays, viewCentreM, viewSpanM);
    }

    /// <summary>
    /// <b>The points a crossing's junctions hand over at</b> (WLK-15): a disc at every end of every stretch
    /// of every zebra — the two on the boundary where its paint stops, and the two on each lane of the walk
    /// beside the road, which are the places that lane was parted at. <b>Six to a junction</b>, one per
    /// connected lane, which is the reading that says whether the place joins what it is supposed to.
    /// </summary>
    /// <remarks>
    /// <b>Drawn exactly as a junction's movements are on the driving side</b> (<see cref="Link"/>): the
    /// network's own colour and the size an agent dots the same places on its own route, because they are
    /// the same kind of thing — the place a way leaves the stretch behind it and the place it meets the one
    /// ahead. <b>A colour of its own would say a crossing is a different kind of way</b>, which is the
    /// reading this layer exists to refuse; what a walk crosses at is the white pair a figure out from the
    /// kerb's own end (<see cref="Theme.FootNode"/>), and these are where the lines that carry it begin.
    /// </remarks>
    static void CrossingPoints(
        ref ScreenDraw draw, CrossingWays crossings, Vector2 viewCentreM, Vector2 viewSpanM)
    {
        foreach (var way in crossings.Ways)
        {
            if (OnScreen(way.FromM, viewCentreM, viewSpanM, PathMarks.JoinDiscM))
            {
                draw.DiscM(way.FromM, PathMarks.JoinDiscM, Theme.WalkingNodes);
            }

            if (OnScreen(way.OntoM, viewCentreM, viewSpanM, PathMarks.JoinDiscM))
            {
                draw.DiscM(way.OntoM, PathMarks.JoinDiscM, Theme.WalkingNodes);
            }
        }
    }

    /// <summary>
    /// <b>Where the town's outline stops following a road</b> (<see cref="CityGen.KerbEnds"/>): a disc at
    /// every place the boundary hands over from one road's kerb to a mouth, to a movement's edge or to the
    /// next road, which is where a walk beside that road has to end.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Under the walking lanes and not under the boundary</b>: the point is on the boundary but what it
    /// is <em>for</em> is the pavement, so it is read against the lanes it will cut rather than against the
    /// line it was found on. Read off the town and never worked out here, like every other shape in this
    /// slice.
    /// </para>
    /// <para>
    /// <b>A road's two ends at one box are drawn in two colours</b> (<see cref="Theme.KerbEndNearer"/>):
    /// red for the kerb of it that runs further out along the road and green for the one that stops nearer
    /// the middle. They stand either side of the carriageway, so the reading is taken across it — which is
    /// why it is a colour and not a mark that has to be found twice. <b>Green last, so a pair that lands on
    /// one place still says there are two.</b>
    /// </para>
    /// <para>
    /// <b>And over both of them, the places the walk crosses</b> (<see cref="Theme.FootNode"/>): two to a
    /// place, either side of the carriageway and square across it, struck off the red one at the figure the
    /// page turns (<see cref="Core.Config.RoadFigures.FootNodeClearM"/>) — or one midway down a street too
    /// short to be crossed twice (WLK-10a). Drawn last because they are what the layer is opened for and the
    /// ends are the working behind them.
    /// </para>
    /// </remarks>
    static void KerbEnds(
        ref ScreenDraw draw, CityGen.KerbEnds ends, Vector2 viewCentreM, Vector2 viewSpanM)
    {
        foreach (var end in ends.Further)
        {
            if (!OnScreen(end.AtM, viewCentreM, viewSpanM, PathMarks.EndDiscM)) continue;

            draw.DiscM(end.AtM, PathMarks.EndDiscM, Theme.KerbEnd);
        }

        foreach (var end in ends.Nearer)
        {
            if (!OnScreen(end.AtM, viewCentreM, viewSpanM, PathMarks.EndDiscM)) continue;

            draw.DiscM(end.AtM, PathMarks.EndDiscM, Theme.KerbEndNearer);
        }

        foreach (var nodes in ends.CrossedM)
        {
            if (OnScreen(nodes.NearM, viewCentreM, viewSpanM, PathMarks.EndDiscM))
            {
                draw.DiscM(nodes.NearM, PathMarks.EndDiscM, Theme.FootNode);
            }

            if (OnScreen(nodes.FarM, viewCentreM, viewSpanM, PathMarks.EndDiscM))
            {
                draw.DiscM(nodes.FarM, PathMarks.EndDiscM, Theme.FootNode);
            }
        }
    }

    /// <summary>
    /// <b>Every lane of the town's pavement</b> (WLK-1, WLK-8, OBS-2d): the fine graph's own lines, as the
    /// chains they are and walked the way they are walked.
    /// </summary>
    /// <remarks>
    /// <b>Chevrons and not ticks</b> (<see cref="PathMarks.Marks"/>): a lane is walked one way as a car's
    /// lane is driven one way, and the graph holds it laid that way — so the mark that reads a direction off
    /// the line is the one that tells the truth about it. <b>Read off the graph and not off the shape it was
    /// cut from</b>, because what a reader of this layer is asking is what a body may be held on.
    /// </remarks>
    static void PavementLanes(
        ref ScreenDraw draw, FootGraph foot, float sagM, GridLevel? pitch, Vector2 viewCentreM, Vector2 viewSpanM,
        MarkClaims claims)
    {
        for (var lane = 0; lane < foot.EdgeCount; lane++)
        {
            Chain(
                ref draw, foot.ArcsOf(lane), sagM, pitch, bothWays: false, Theme.WalkingNodes, viewCentreM,
                viewSpanM, claims);
        }
    }

    /// <summary>
    /// <b>And what the move could not close</b> (<see cref="World.Foot.PavementLanes.LooseOf"/>): a run with
    /// two ends where a course should be, which is nobody's lane and is drawn in the fault colour that every
    /// other open run is.
    /// </summary>
    static void Unwalked(
        ref ScreenDraw draw, World.Foot.PavementLanes pavement, Vector2 viewCentreM, Vector2 viewSpanM,
        float sagM, GridLevel? pitch)
    {
        for (var lane = 0; lane < pavement.Count; lane++)
        {
            Boundaries(
                ref draw, pavement.LooseOf(lane), Theme.PerimeterLoose, true, viewCentreM, viewSpanM, sagM,
                pitch);
        }
    }

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
        ref ScreenDraw draw, RoadGraph roads, SimConfig config, float sagM, GridLevel? pitch, Vector4 colour,
        Vector2 viewCentreM, Vector2 viewSpanM, MarkClaims claims)
    {
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            var end = roads.EndOf(lane);
            if (!OnScreen(end.PositionM, viewCentreM, viewSpanM, config.CarTurningRadiusM * 2f)) continue;

            foreach (var connector in roads.ConnectorsFrom(lane))
            {
                Link(
                    ref draw, roads.ConnectorArcs(connector), sagM, pitch, bothWays: false, colour,
                    viewCentreM, viewSpanM, claims);
            }
        }
    }

    /// <summary>
    /// <b>Every manoeuvre a car has in hand at a bay</b> (GEN-4f) — each piece of the shape it laid for itself,
    /// drawn exactly as a junction's movements are (<see cref="Movements"/>), with a dot where it leaves one
    /// line and a dot where it arrives. There is nothing to draw at a bay nobody is getting into or out of: the
    /// town lays no way to one.
    /// </summary>
    /// <remarks>
    /// <b>A piece driven in reverse takes the shade of the same colour that says the car is going backwards down
    /// it</b> (<see cref="Theme.DrivingReverse"/>): a car backing in pulls on past the bay and comes back over
    /// the same street, and which way it is pointing while it travels each is the whole of what tells the two
    /// apart.
    /// </remarks>
    static void BayApproaches(
        ref ScreenDraw draw, TownWorld world, float sagM, GridLevel? pitch, Vector4 colour, Vector2 viewCentreM,
        Vector2 viewSpanM, MarkClaims claims)
    {
        var manoeuvres = world.Manoeuvres;
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (!manoeuvres.Any(car)) continue;

            var shape = manoeuvres.Shape[car];
            for (var piece = manoeuvres.Piece[car]; piece < shape.Pieces; piece++)
            {
                Link(
                    ref draw, manoeuvres.PieceOf(car, piece), sagM, pitch, bothWays: false,
                    shape.IsReverse(piece) ? Theme.DrivingReverse : colour, viewCentreM, viewSpanM, claims);
            }
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
        ref ScreenDraw draw, ReadOnlySpan<ArcSeg> arcs, float sagM, GridLevel? pitch, bool bothWays, Vector4 colour,
        Vector2 viewCentreM, Vector2 viewSpanM, MarkClaims claims)
    {
        if (arcs.Length == 0) return;

        if (!OnScreen(arcs, viewCentreM, viewSpanM)) return;

        PathMarks.Chained(ref draw, arcs, 0f, Spline.TotalLengthM(arcs), pitch, bothWays, sagM, colour, claims);
        draw.DiscM(arcs[0].StartM, PathMarks.JoinDiscM, colour);
        draw.DiscM(arcs[^1].EndM, PathMarks.JoinDiscM, colour);
    }

    /// <summary>
    /// A chain of arcs as the line it is, with marks down it at a pitch on the ground. A cull that
    /// admits a body is not a cull that admits its whole line, so the chain is rejected on the box it fits
    /// in before it is sampled finely.
    /// </summary>
    static void Chain(
        ref ScreenDraw draw, ReadOnlySpan<ArcSeg> arcs, float sagM, GridLevel? pitch, bool bothWays, Vector4 colour,
        Vector2 viewCentreM, Vector2 viewSpanM, MarkClaims claims)
    {
        if (arcs.Length == 0) return;

        Chain(
            ref draw, arcs, 0f, Spline.TotalLengthM(arcs), sagM, pitch, bothWays, colour, viewCentreM, viewSpanM,
            claims);
    }

    /// <summary>
    /// <b>Whether any part of a chain could be on screen</b>, asked of the box the chain fits in
    /// (<see cref="ChainIndex.Box"/>) and never of the circle over its own two ends.
    /// </summary>
    /// <remarks>
    /// <b>A chain is not inside the circle over its ends, and a town's pavement is full of the case.</b> A
    /// walking lane that runs down one side of the wedge between two roads and back up the other stands its
    /// two ends beside each other with a hundred and fifty metres of line between them, so that circle
    /// rejects it at every framing that shows the wedge — and a frame taken there drew the concrete with no
    /// line on it at all, which is a picture claiming the town has a hole it has not got. <b>A cull may cost
    /// a quad and may never cost a line.</b>
    /// </remarks>
    static bool OnScreen(ReadOnlySpan<ArcSeg> arcs, Vector2 viewCentreM, Vector2 viewSpanM)
    {
        var leastM = new Vector2(float.MaxValue);
        var mostM = new Vector2(float.MinValue);
        foreach (ref readonly var arc in arcs) ChainIndex.Box(arc, ref leastM, ref mostM);

        var halfM = viewSpanM * 0.5f;
        return leastM.X <= viewCentreM.X + halfM.X && mostM.X >= viewCentreM.X - halfM.X
               && leastM.Y <= viewCentreM.Y + halfM.Y && mostM.Y >= viewCentreM.Y - halfM.Y;
    }

    /// <summary>
    /// The same, between two stations along it — which is what a lane comes to once the corners at its two
    /// ends have taken their ground. How much of the chain is drawn does not move a single mark: they stand
    /// where it crosses the town's grid (<see cref="MarkGrid"/>), which is the same grid an agent's own
    /// layer marks the same ground on.
    /// </summary>
    static void Chain(
        ref ScreenDraw draw, ReadOnlySpan<ArcSeg> arcs, float fromM, float toM, float sagM, GridLevel? pitch,
        bool bothWays, Vector4 colour, Vector2 viewCentreM, Vector2 viewSpanM, MarkClaims claims)
    {
        if (arcs.Length == 0 || toM <= fromM) return;

        // The whole chain's box and not the span's, which is a superset of it: what the span leaves out is
        // a corner's margin at either end, and boxing it apart would be a second reading of one line.
        if (!OnScreen(arcs, viewCentreM, viewSpanM)) return;

        PathMarks.Chained(ref draw, arcs, fromM, toM, pitch, bothWays, sagM, colour, claims);
    }
}
