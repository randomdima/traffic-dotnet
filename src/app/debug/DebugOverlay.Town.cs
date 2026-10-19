using System.Numerics;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Debug;

/// <summary>What is drawn over the town rather than over an agent — how it was laid, relaid only when the view has moved.</summary>
internal sealed partial class DebugOverlay
{
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
        // <b>Laid at the zoom's rung of a ladder and not at the zoom</b> (ViewFigures.DebugRelayZoomFactor): every notch of
        // the wheel is a new scale, and laying the town's graphs again for each is the cost this cache is for.
        var rung = config.View.DebugRelayZoomFactor;
        pixelsPerMetre = MathF.Pow(rung, MathF.Floor(MathF.Log(pixelsPerMetre) / MathF.Log(rung)));

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
        var above = new ScreenDraw(_townAbove);

        // <b>The ground first and the ruling over it</b>, both of them under every line: neither wash is
        // the thing being looked at, and what either is read against is whatever line lands on top of it.
        if (switches.Ribbons) Ribbons(ref into, ref above, world, config, _drawnCentreM, _drawnSpanM, pixelsPerMetre);

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
        _townAboveQuads = above.Written;
    }

    /// <summary>
    /// The nodes switch's readings of how the town was laid, cached with the rest of its still geometry: what the pavement
    /// could not close, where each kerb ends, and the points a crossing hands over at. <b>Every way itself</b> — each lane,
    /// junction join, pavement, mitre and track, whatever kind — <b>is the engine layers'</b> (<see cref="EngineLayers"/>),
    /// drawn off the one numbering every way is in.
    /// </summary>
    void Nodes(
        ref ScreenDraw draw, TownWorld world, SimConfig config, Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre)
    {
        Unwalked(
            ref draw, world.PavementLanes, viewCentreM, viewSpanM, PathMarks.SagPx / pixelsPerMetre,
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
    /// <b>Every manoeuvre a car has in hand at a bay</b> (GEN-4f) — each piece of the shape it laid for itself, in the car's
    /// own colour, with a dot where it leaves one line and a dot where it arrives. It is geometry the car laid for itself and
    /// on no way of the town, so a car's own layer and not the engine's draws it.
    /// </summary>
    /// <remarks>
    /// <b>A piece driven in reverse is drawn in the colour that says a car is going backwards down it</b>
    /// (<see cref="Theme.DrivingReverse"/>): a car backing in pulls on past the bay and comes back over the same street, and
    /// which way it is pointing while it travels each is the whole of what tells the two apart.
    /// </remarks>
    static void BayApproaches(
        ref ScreenDraw draw, TownWorld world, float sagM, GridLevel? pitch, Vector2 viewCentreM, Vector2 viewSpanM)
    {
        var manoeuvres = world.Manoeuvres;
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (!manoeuvres.Any(car)) continue;

            var shape = manoeuvres.Shape[car];
            for (var piece = manoeuvres.Piece[car]; piece < shape.Pieces; piece++)
            {
                var arcs = manoeuvres.PieceOf(car, piece);
                var (leastM, mostM) = ChainBoxes.Of(arcs);
                if (!ChainBoxes.Meets(leastM, mostM, viewCentreM, viewSpanM) || arcs.Length == 0) continue;

                var colour = shape.IsReverse(piece) ? Theme.DrivingReverse : Theme.AgentLine(car);
                PathMarks.Chained(ref draw, arcs, 0f, Spline.TotalLengthM(arcs), pitch, false, sagM, colour, MarkClaims.None);
                draw.DiscM(arcs[0].StartM, PathMarks.JoinDiscM, colour);
                draw.DiscM(arcs[^1].EndM, PathMarks.JoinDiscM, colour);
            }
        }
    }
}
