using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>The line every road of the layout is laid as, drawn when the road is offered</b> (GEN-47): from the
/// stand point of one arm to the stand point of the other, holding each arm's own bearing for its lead and
/// wandering between them by as much as its district allows.
/// </summary>
/// <remarks>
/// <para>
/// <b>A road is laid once and it is laid correctly or not at all</b> (GEN-10). The line is what says whether
/// a link is a road: one that cannot meet both of its drawn bearings inside its class's floor, one that would
/// run off the world or over water, and one that would share ground with a road already laid (GEN-49) are all
/// refused as the link is offered — so nothing is laid and taken back, no round of refusals repairs what an
/// earlier round broke, and the layout that stands is a layout of roads that can be drawn.
/// </para>
/// <para>
/// <b>Precedence is the order the links are offered in</b> (GEN-16, GEN-49). Every node the town will have is
/// placed before any road is laid and the arterials are laid before the lattice, so a street offered against
/// ground an arterial already holds is the one refused, and no pass has to weigh a pair after the fact and
/// choose. <b>The bound holds from the first road</b>, there being no pair of junctions left that are about
/// to become one: two nodes inside a locality of each other were one node when the second was asked for
/// (<see cref="TownLayout.AddNode"/>).
/// </para>
/// <para>
/// <b>A road's shape is a function of the link and of the seed</b> (GEN-11) — its two node centres, the places
/// it passes and its class — so offering it again gives the same line, a deletion moves nothing that stayed,
/// and where the same link is offered twice over the town's laying it is the same road both times. The wander
/// is keyed on the link the way an arm's jitter is (<see cref="ConnectionPoints"/>) rather than drawn from a
/// stream walked road by road, which would make every road's shape depend on how many roads were laid before
/// it.
/// </para>
/// <para>
/// <b>The ground two roads may not share is walked and never solved.</b> Two curves' closest approach has no
/// closed form, and what is being asked is whether their bands overlap at all — so the candidate is walked at
/// the footprint it keeps and asked about the roads whose own cells it lands in.
/// </para>
/// </remarks>
internal sealed class RoadLines(
    ulong seed, SimConfig config, Districts districts, Vector2 extentM, WaterRules water)
{
    /// <summary>
    /// How much of a segment a rounded corner may eat, either side of the vertex it rounds. <b>It is the same
    /// share the jitter bound is derived against</b> (<see cref="ConnectionPoints.TangentShareOfSegment"/>): a
    /// corner allowed more ground here than the bound assumed is a road that turns off its arm where the arm
    /// was drawn not to let it.
    /// </summary>
    const float TangentShareOfSegment = ConnectionPoints.TangentShareOfSegment;

    /// <summary>The stream the wander is drawn from, keyed on the link so that a road's middle is the link's own.</summary>
    const ulong WanderStream = 0x7761_6E64_6572_0000;

    /// <summary>The lines kept, one a road, in the layout's own numbering.</summary>
    readonly List<ArcSeg[]> _laid = [];

    /// <summary>And which nodes each of them was laid between, so a pair that meets at one is left alone.</summary>
    readonly List<(int From, int To)> _between = [];

    /// <summary>
    /// Which roads have a piece in each cell of a lattice the footprint wide, so that what a candidate has to
    /// be asked about is the handful of roads near it (GEN-49).
    /// </summary>
    readonly Dictionary<(int X, int Y), List<int>> _inCell = [];

    readonly HashSet<int> _near = [];

    public IReadOnlyList<ArcSeg[]> Laid => _laid;

    /// <summary>
    /// <b>The line this link would be laid as, and whether the town can have it</b> — the whole of what
    /// refuses a road (GEN-47, GEN-49, GEN-14).
    /// </summary>
    /// <param name="without">
    /// The roads this line would stand in the place of, which are the ones it may share ground with: a run
    /// joined into one road is laid over its own pieces (GEN-51), and a ring's arms are laid over where they
    /// reached before the node was opened out (GEN-19).
    /// </param>
    public bool CanLay(
        in LayoutEdge edge, IReadOnlyList<Vector2> nodeM, ReadOnlySpan<int> without, out ArcSeg[] chain)
    {
        chain = Chain(edge, nodeM);
        return Clear(chain, edge, without);
    }

    /// <summary>
    /// <b>Whether a line the caller already has is one the town can have</b> — the half of
    /// <see cref="CanLay"/> that is not the drawing. <b>A cut carries its own line</b> (GEN-52,
    /// <see cref="CutJunctions"/>): the pieces of a road parted at a node are the ground that road was
    /// already laid on, so drawing them again would move the carriageway the cut exists not to move.
    /// </summary>
    public bool Clear(ArcSeg[] chain, in LayoutEdge edge, ReadOnlySpan<int> without) =>
        chain.Length > 0 && !OffTheGround(chain, edge.Class) && !SharesGround(chain, edge, without);

    /// <inheritdoc cref="CanLay(in LayoutEdge, IReadOnlyList{Vector2}, ReadOnlySpan{int}, out ArcSeg[])"/>
    public bool CanLay(in LayoutEdge edge, IReadOnlyList<Vector2> nodeM, out ArcSeg[] chain) =>
        CanLay(edge, nodeM, default, out chain);

    /// <summary>
    /// <b>Whether two lines being laid together keep off each other</b> (GEN-49) — the same question
    /// <see cref="CanLay"/> asks of the roads already standing, for the pair neither of which is standing yet:
    /// a ring's arms all move at once and stop meeting at the node they used to share.
    /// </summary>
    public bool Apart(ArcSeg[] chain, in LayoutEdge edge, ArcSeg[] otherChain, in LayoutEdge other)
    {
        if (other.From == edge.From || other.From == edge.To
            || other.To == edge.From || other.To == edge.To)
        {
            return true;
        }

        return !Crosses(chain, otherChain, config.RoadFootprintM);
    }

    /// <summary>One road's line kept, and indexed for the roads offered after it.</summary>
    public void Keep(in LayoutEdge edge, ArcSeg[] chain)
    {
        var road = _laid.Count;
        _laid.Add(chain);
        _between.Add((edge.From, edge.To));
        foreach (var cell in Cells(chain))
        {
            if (!_inCell.TryGetValue(cell, out var here)) _inCell[cell] = here = [];

            if (here.Count == 0 || here[^1] != road) here.Add(road);
        }
    }

    /// <summary>
    /// The lines of a layout that has been renumbered, in its new order — every road carried over as the line
    /// it was laid as, because a deletion moves nothing.
    /// </summary>
    public void Reset(IReadOnlyList<LayoutEdge> edges, IReadOnlyList<ArcSeg[]> kept)
    {
        _laid.Clear();
        _between.Clear();
        _inCell.Clear();
        for (var road = 0; road < kept.Count; road++) Keep(edges[road], kept[road]);
    }

    /// <summary>
    /// <b>One road's own curve</b>, laid to arrive on the bearings its two ends were drawn with (TER-5d). It
    /// runs from the stand point of one arm to the stand point of the other and wanders between them by as
    /// much as its district allows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The bearing is held by two points and not by a special case</b>: a lead laid along the arm puts the
    /// polyline's first leg on the arm's own line, so the chain <see cref="Biarcs"/> lays off it begins on that
    /// bearing and ends arriving on the other. A road that bends tighter than its class affords gives a via
    /// point up and is laid again straighter; one that has none left to give is no road.
    /// </para>
    /// <para>
    /// <b>A joined road passes where it was joined and wanders nowhere</b> (GEN-51): the places between its
    /// ends are the junctions it was one road through, so its middle is already chosen and drawing a second one
    /// would move the carriageway off the ground both its pieces were laid on. It turns those corners at a
    /// junction's own floor and not at its class's (GEN-47), because every one of them was a junction and what
    /// a car held there was the movement across it.
    /// </para>
    /// </remarks>
    ArcSeg[] Chain(in LayoutEdge edge, IReadOnlyList<Vector2> nodeM)
    {
        var from = ArmAt(edge, nodeM, atFrom: true);
        var to = ArmAt(edge, nodeM, atFrom: false);

        var betweenM = Vector2.Distance(from.StandM, to.StandM);
        if (betweenM <= 0f) return [];

        // <b>A ring piece is one arc of one circle</b> (GEN-19) and is not laid to anything: both its arms
        // were held to the circle's own tangents, so the piece between their stand points is the circle.
        if (MathF.Abs(from.Curvature) > 0f)
        {
            return [Arc(from.StandM, (to.StandM - from.StandM) / betweenM, betweenM, from.Curvature)];
        }

        var floorM = edge.ThroughM.Length > 0
            ? config.JunctionCorneringRadiusM
            : FloorRadiusM(config, edge.Class);

        var draw = new Rng(seed, WanderStream ^ ConnectionPoints.Keyed(nodeM[edge.From], nodeM[edge.To]));
        var wanderNodes = edge.ThroughM.Length > 0
            ? edge.ThroughM.Length
            : WanderNodes(edge, from.NodeM, to.NodeM, ref draw);

        Span<Vector2> pointsM = stackalloc Vector2[wanderNodes + 2];
        pointsM[0] = from.StandM;
        pointsM[^1] = to.StandM;
        if (edge.ThroughM.Length > 0)
        {
            edge.ThroughM.AsSpan().CopyTo(pointsM[1..^1]);
        }
        else
        {
            Wander(pointsM, edge, wanderNodes, ref draw);
        }

        // <b>Nothing bends tighter than its own floor.</b> A road that asks for one is straightened before it
        // is given up on — the wander is what it wanted and the bearings are what it owes, so the wander is
        // what gives way. <b>A joined road has nothing to give way</b> (GEN-51): its middle is the ground it
        // was two roads over, so it is laid there or it is not laid.
        for (var wandering = wanderNodes; wandering >= edge.ThroughM.Length; wandering--)
        {
            var chain = Biarcs(pointsM[..(wandering + 1)], to.StandM, from.StandUnit, -to.StandUnit);
            if (chain.Length > 0 && TightestRadiusM(chain) >= floorM) return chain;
        }

        return [];
    }

    /// <summary>
    /// <b>The arm one end of this link leaves on</b> (GEN-46). A bridge and a ring piece take the bearing
    /// their own shape settles; everything else takes the chord to the first place the road passes, jittered.
    /// </summary>
    ConnectionPoints.Arm ArmAt(in LayoutEdge edge, IReadOnlyList<Vector2> nodeM, bool atFrom)
    {
        var junction = atFrom ? edge.From : edge.To;

        // <b>A road leaves its junction for the first place it passes</b> (GEN-51), where it passes anywhere:
        // aiming the arm at the far end would point the carriageway somewhere it never goes.
        var towardM = edge.ThroughM.Length > 0
            ? (atFrom ? edge.ThroughM[0] : edge.ThroughM[^1])
            : nodeM[atFrom ? edge.To : edge.From];

        var settled = edge.Class is RoadClass.Bridge or RoadClass.Roundabout;

        // The lead bends with the ring and with nothing else, and the far end of a piece leaves it the other
        // way round (GEN-19).
        var curvature = atFrom ? edge.Curvature : -edge.Curvature;

        return ConnectionPoints.ArmOf(
                   seed, config, junction, nodeM[junction], towardM, settled, settled ? curvature : 0f)
               with { OnTheLine = edge.Class == RoadClass.Roundabout };
    }

    /// <summary>
    /// <b>Whether any of a road runs off the world it was laid on, or over water it is no bridge over</b>
    /// (GEN-14). The ground a road joins is asked about when the nodes are placed, and <b>what is asked here is
    /// the line</b>, which is free to bow off its own chord and does so hardest where it turns a corner it was
    /// joined through (GEN-51).
    /// </summary>
    /// <remarks>
    /// <b>Walked at a metre, which is the step the ground is judged at.</b> There is no closed form for where a
    /// chain of arcs reaches furthest or first gets its feet wet, and no need for one: a bow a metre's walk
    /// cannot find is a bow nothing stands in.
    /// </remarks>
    bool OffTheGround(ReadOnlySpan<ArcSeg> chain, RoadClass roadClass)
    {
        var lengthM = Spline.TotalLengthM(chain);
        for (var alongM = 0f; ; alongM += 1f)
        {
            var atM = Spline.SampleAt(chain, MathF.Min(alongM, lengthM)).PositionM;
            if (atM.X < 0f || atM.Y < 0f || atM.X > extentM.X || atM.Y > extentM.Y) return true;
            if (roadClass != RoadClass.Bridge && water.Wet(atM)) return true;
            if (alongM >= lengthM) return false;
        }
    }

    /// <summary>
    /// <b>Whether this line would share ground with a road already laid</b> (GEN-49): a road's whole width and
    /// the walk either side of it (<see cref="SimConfig.RoadFootprintM"/>), measured between the lines the two
    /// were laid as and never between the chords they were joined on.
    /// </summary>
    /// <remarks>
    /// <b>Two roads that share a node are left alone.</b> They touch there because that is what a junction is,
    /// and how square they have to stand to each other is <see cref="TownLayout"/>'s (GEN-13).
    /// </remarks>
    bool SharesGround(ArcSeg[] chain, in LayoutEdge edge, ReadOnlySpan<int> without)
    {
        var apartM = config.RoadFootprintM;

        _near.Clear();
        foreach (var cell in Cells(chain))
        {
            for (var atX = cell.X - 1; atX <= cell.X + 1; atX++)
            {
                for (var atY = cell.Y - 1; atY <= cell.Y + 1; atY++)
                {
                    if (_inCell.TryGetValue((atX, atY), out var here)) _near.UnionWith(here);
                }
            }
        }

        foreach (var other in _near)
        {
            if (without.Contains(other)) continue;

            var (from, to) = _between[other];
            if (from == edge.From || from == edge.To || to == edge.From || to == edge.To) continue;

            if (Crosses(chain, _laid[other], apartM)) return true;
        }

        return false;
    }

    /// <summary>
    /// <b>Whether two lines are nearer than the ground they take, or cut each other at all.</b>
    /// </summary>
    /// <remarks>
    /// <b>Crossed, and near from either end.</b> A walk at the footprint can step over the one place two lines
    /// come close, and it steps over a different place on each of them — so both are walked, and a pair that
    /// actually cuts is caught whatever the stations landed on.
    /// </remarks>
    static bool Crosses(ArcSeg[] one, ArcSeg[] other, float apartM)
    {
        Span<SplineCrossing> crossings = stackalloc SplineCrossing[MostCrossings];
        return Spline.CrossingsM(one, other, 0f, 0f, crossings) > 0
               || PassesInside(one, other, apartM)
               || PassesInside(other, one, apartM);
    }

    /// <summary>How many places two roads may cross before the pair is plainly one road laid over another.</summary>
    const int MostCrossings = 4;

    /// <summary>
    /// <b>Whether two roads pass closer than the ground they take</b>. Walked at the tolerance a road is laid
    /// to rather than solved: two curves' closest approach has no closed form, and what is being asked is
    /// whether their bands overlap at all.
    /// </summary>
    static bool PassesInside(ReadOnlySpan<ArcSeg> one, ReadOnlySpan<ArcSeg> other, float apartM)
    {
        var lengthM = Spline.TotalLengthM(one);
        var otherLengthM = Spline.TotalLengthM(other);
        var stations = Math.Max(1, (int)MathF.Ceiling(lengthM / apartM));

        for (var station = 0; station <= stations; station++)
        {
            var atM = Spline.SampleAt(one, lengthM * station / stations).PositionM;
            var onM = Spline.ProjectM(other, atM, otherLengthM * 0.5f, otherLengthM);
            if (Vector2.Distance(Spline.SampleAt(other, onM).PositionM, atM) < apartM) return true;
        }

        return false;
    }

    /// <summary>The cells one line's own walk passes through, each once and in order.</summary>
    IEnumerable<(int X, int Y)> Cells(ArcSeg[] chain)
    {
        if (chain.Length == 0) yield break;


        var cellM = config.RoadFootprintM;
        var lengthM = Spline.TotalLengthM(chain);
        var last = (X: int.MinValue, Y: int.MinValue);
        for (var alongM = 0f; ; alongM += cellM * 0.5f)
        {
            var atM = Spline.SampleAt(chain, MathF.Min(alongM, lengthM)).PositionM;
            var cell = ((int)MathF.Floor(atM.X / cellM), (int)MathF.Floor(atM.Y / cellM));
            if (cell != last) yield return last = cell;

            if (alongM >= lengthM) yield break;
        }
    }

    /// <summary>
    /// <b>The chain of biarcs through a polyline, leaving and arriving on two given bearings.</b> Every
    /// interior point is passed on the line through its neighbours, so the pieces either side of it are
    /// tangent there and the whole chain is one line a follower can read.
    /// </summary>
    static ArcSeg[] Biarcs(ReadOnlySpan<Vector2> throughM, Vector2 lastM, Vector2 leaves, Vector2 arrives)
    {
        var chain = new List<ArcSeg>(throughM.Length * 2);
        Span<ArcSeg> drawn = stackalloc ArcSeg[2];

        var at = leaves;
        for (var piece = 0; piece < throughM.Length; piece++)
        {
            var ontoM = piece + 1 < throughM.Length ? throughM[piece + 1] : lastM;
            var onward = piece + 2 < throughM.Length
                ? Through(throughM[piece], throughM[piece + 1], throughM[piece + 2])
                : piece + 2 == throughM.Length ? Through(throughM[piece], throughM[piece + 1], lastM)
                : arrives;

            var laid = Spline.BiarcInto(throughM[piece], Facing(at), ontoM, Facing(onward), drawn);
            if (laid == 0) return [];

            // <b>Two straights in a line are one straight</b>, which is what a bridge's span is and what a
            // road drawn between two arms that agree comes to. Nothing else is rubbed out: a merge over two
            // arcs that merely read alike moves the line by the tolerance it allowed, and a road is read as
            // one line to a far finer angle than that (<see cref="RoadStage.CreaseRad"/>).
            if (laid == 2 && drawn[0].Curvature == 0f && drawn[1].Curvature == 0f)
            {
                chain.Add(new ArcSeg(
                    drawn[0].StartM, drawn[0].HeadingRad, drawn[0].LengthM + drawn[1].LengthM, 0f));
            }
            else
            {
                for (var arc = 0; arc < laid; arc++) chain.Add(drawn[arc]);
            }

            at = onward;
        }

        return [.. chain];
    }

    /// <summary>The bearing a line passes an interior point on: the direction its two neighbours leave it in.</summary>
    static Vector2 Through(Vector2 beforeM, Vector2 atM, Vector2 afterM)
    {
        var run = afterM - beforeM;
        return run.LengthSquared() > 0f ? Vector2.Normalize(run) : Vector2.UnitX;
    }

    /// <summary>The tightest circle anywhere in a chain, which is the whole question about what a car can hold on it.</summary>
    static float TightestRadiusM(ReadOnlySpan<ArcSeg> arcs)
    {
        var bend = 0f;
        foreach (var arc in arcs) bend = MathF.Max(bend, MathF.Abs(arc.Curvature));

        return bend <= 1e-6f ? float.PositiveInfinity : 1f / bend;
    }

    /// <summary>The one arc a piece of a ring is: the chord it stands on decides how far round it goes.</summary>
    static ArcSeg Arc(Vector2 fromM, Vector2 unit, float chordM, float curvature)
    {
        var radiusM = 1f / MathF.Abs(curvature);
        var half = MathF.Asin(MathF.Min(1f, chordM * 0.5f / radiusM));

        // An arc's chord runs at its own start bearing plus half its sweep (<see cref="ArcSeg.PointAtM"/>),
        // so the bearing it has to start on is the chord's less that half — the tangent at the node.
        var sweep = 2f * half * MathF.Sign(curvature);
        return new ArcSeg(fromM, Facing(unit) - (sweep * 0.5f), MathF.Abs(sweep) * radiusM, curvature);
    }

    /// <summary>
    /// The points a wandering street is drawn through: <b>the two ends of its leads, and its virtual nodes
    /// standing off the line between them</b>. The offset is clamped so that the corner it makes cannot be
    /// tighter than the class's floor — a street asked to wander further than its own length can turn through
    /// is straightened rather than bent past what a car could take.
    /// </summary>
    void Wander(Span<Vector2> pointsM, in LayoutEdge edge, int nodes, ref Rng draw)
    {
        if (nodes == 0) return;

        var fromM = pointsM[0];
        var toM = pointsM[^1];
        var acrossM = toM - fromM;
        var lengthM = acrossM.Length();
        if (lengthM <= 0f) return;

        var unit = acrossM / lengthM;
        var side = Heading.RightOf(unit);
        var segmentM = lengthM / (nodes + 1);
        var floorM = FloorRadiusM(config, edge.Class);

        // <b>Bounded by the block and by the bend together</b>: a street may not stray so far off its own line
        // that it could meet the street a block over, and it may not ask for a corner its class cannot turn —
        // which over a segment of its own length is the sagitta that radius affords.
        var wanderM = MathF.Min(
            WanderM(districts, edge, (fromM + toM) * 0.5f, config),
            TangentShareOfSegment * segmentM * segmentM / MathF.Max(floorM, 1f));

        for (var node = 0; node < nodes; node++)
        {
            var alongM = segmentM * (node + 1);
            pointsM[node + 1] = fromM + (unit * alongM) + (side * draw.NextFloat(-wanderM, wanderM));
        }
    }

    int WanderNodes(in LayoutEdge edge, Vector2 fromM, Vector2 toM, ref Rng draw)
    {
        // A spoke is straight because the layout reads it as a ray: everything that asks whether a point
        // stands clear of an arterial asks it of a line through the hub.
        if (edge.Class != RoadClass.Street) return 0;

        // <b>A strict district wanders through at most one virtual node and a loose one through the town's own
        // bound</b> (<see cref="CityGenFigures.WanderNodesMost"/>), which is what makes a strict district's
        // streets read as near-chords and a loose one's as curves.
        var most = config.CityGen.WanderNodesMost;
        var districtAt = districts.At((fromM + toM) * 0.5f);
        var strict = districtAt < 0 || districts[districtAt].Strict;
        return strict ? draw.NextInt(2) : 1 + draw.NextInt(most);
    }

    /// <summary>
    /// How tightly a road of this class may bend: the radius its own design speed affords on tarmac.
    /// <b>Derived and never authored</b> — a bend quoted in metres would be a figure nobody could check
    /// against the car that has to take it.
    /// </summary>
    public static float FloorRadiusM(SimConfig config, RoadClass roadClass) =>
        config.CarCorneringRadiusM(
            roadClass switch
            {
                RoadClass.Street => config.CityGen.StreetDesignSpeedMps,
                RoadClass.Roundabout => config.CityGen.RoundaboutDesignSpeedMps,
                _ => config.CityGen.ArterialDesignSpeedMps,
            },
            config.Terrain.PavedCoefficient);

    /// <summary>
    /// <b>How far off its own chord a road is allowed to wander</b>: the block spacing of the district it runs
    /// through, at the share of a block that class of road is allowed (GEN-47). A grid's share is the tighter
    /// one, because a grid is straight.
    /// </summary>
    public static float WanderM(Districts districts, in LayoutEdge edge, Vector2 middleM, SimConfig config)
    {
        var districtAt = districts.At(middleM);
        var spacingM = districtAt < 0
            ? config.CityGen.BlockSpacingAlongMinM
            : districts[districtAt].BlockTightestM;
        var strict = districtAt >= 0 && districts[districtAt].Strict;
        return spacingM * (edge.Class == RoadClass.Arterial || strict
            ? config.CityGen.GridWanderInBlocks
            : config.CityGen.StreetWanderInBlocks);
    }

    static float Facing(Vector2 unit) => MathF.Atan2(unit.Y, unit.X);
}
