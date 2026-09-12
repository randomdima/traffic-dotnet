using System.Diagnostics;
using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Statics;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>The outer shell of one town's driven ground, laid and then read back</b>
/// (<see cref="LaneShell"/>): how its stretches were paired, which runs closed, and which lines it is the
/// outside of came back in nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>The reading the layer cannot give.</b> What the shell hands over is the rings that shut and nothing
/// else, so a lane the perimeter layer does not mark is either a lane that was never the outside or one
/// whose whole run was thrown away for a fault somewhere else on it (OBS-2p). Only this tells the two
/// apart, and the last table is what says which.
/// </para>
/// <para>
/// <b>It gates nothing.</b> Every figure is a fact about one town's geometry rather than a claim about the
/// engine, and nothing here is asserted anywhere.
/// </para>
/// </remarks>
internal static class ShellProbe
{
    /// <summary>How many rows of a table are printed before the rest are counted instead. A screen of them.</summary>
    const int Listed = 25;

    /// <summary>
    /// How far round a hand-over has to turn before the ring is doubling back rather than turning a corner.
    /// A right angle and a half: no junction of a town turns one that sharp, so past it the outside has
    /// gone somewhere and come back.
    /// </summary>
    const float SpikeRad = MathF.PI * 0.75f;

    /// <summary>
    /// How far round a place the stretches are listed when one is asked about (<c>--at</c>). A junction's
    /// own ground with its corners over: near enough that everything listed is part of the same question.
    /// </summary>
    const float AboutM = 12f;

    /// <summary>How finely a line is walked when asking how near a place it comes. A stride: near enough for
    /// a table that is read to the tenth of a metre.</summary>
    const float WalkedM = 1f;

    /// <summary>
    /// How short a step off a line and back onto it is a jog in that line rather than a corner the ring
    /// really turns. A metre: nothing a car is driven round is shorter, so under it the ring has left the
    /// line for a length of nothing and the two are one line drawn crooked.
    /// </summary>
    const float JogM = 1f;

    /// <summary>
    /// How far inside the driven ground a straight's middle may stand before it is not the boundary at all.
    /// A metre: the boundary runs along the edge, a join that shuts a corner dips inside it by the sag of a
    /// fillet, and past a metre the straight is crossing the ground rather than skirting it.
    /// </summary>
    const float AcrossM = 1f;

    public static void Run(string map, SimConfig config, Vector2? atM = null)
    {
        var plan = Maps.Plan(map, config, BuildingCatalog.Shared.OrdinaryFootprintsM());
        var paving = plan.Paving(config);

        var started = Stopwatch.GetTimestamp();
        var shell = paving.Perimeter(config);
        var elapsed = Stopwatch.GetElapsedTime(started);
        var reading = shell.Reading;

        Console.WriteLine($"shell probe — {plan.Name}, seed {plan.Seed}, laid in {elapsed.TotalMilliseconds:F0} ms");
        Console.WriteLine();
        Console.WriteLine("the walk");
        Console.WriteLine(
            $"  driven lines        {reading.Lines,7}  {paving.Lanes.LaneCount} lanes, "
            + $"{paving.Lanes.ConnectorCount} through a box, {paving.Bays.GroundWays.Length} into a bay");
        Console.WriteLine($"  stretches           {reading.Stretches,7}  metres of a line whose own edge is the edge of the ground");
        Console.WriteLine($"  met at a crossing   {reading.MetAtACrossing,7}  handed on where two band edges cross");
        Console.WriteLine($"  joined across a cut {reading.JoinedAcrossACut,7}  handed on over ground nothing is driven along");
        Console.WriteLine($"  left over           {reading.LooseEnds.Count,7}  carrying the outside on to nothing");
        Console.WriteLine($"  met at a point      {reading.MetAtAPoint,7}  carried to where the two lines cross, and drawn with nothing between");
        Console.WriteLine($"  left a straight     {reading.LeftAStraight,7}  no crossing to carry to");
        Console.WriteLine($"  carried away        {reading.CarriedAway,7}  cut back at both ends until nothing of its own was left");
        Console.WriteLine();

        Met(reading);
        Handovers(reading, paving);
        Corners(reading, paving);
        Cuts(reading, paving);
        Across(reading, paving);
        Dips(reading, paving);
        Runs(reading);
        Loose(reading, paving);
        Lost(reading, paving);
        Extruded(shell, paving, config);
        Built(plan, paving, config);
        if (atM is { } place)
        {
            Ground(paving, config, place);
            About(reading, paving, place);
        }
    }

    /// <summary>
    /// <b>What the ground says at one place, and what the boundary says about it</b> — the two halves of
    /// one answer (<see cref="GroundRings.OffTheKerbM"/>, <c>GroundShapes.At</c>), printed together so that
    /// a place the picture and the answer disagree about can be looked at rather than guessed at.
    /// </summary>
    static void Ground(Paving paving, SimConfig config, Vector2 placeM)
    {
        var rings = GroundRings.Of(paving, config);
        var shapes = new GroundShapes(paving, config);

        Console.WriteLine();
        Console.WriteLine($"ground at {placeM.X:F2},{placeM.Y:F2}");

        foreach (var offM in (ReadOnlySpan<float>)[0f, 1f, 2f, -1f, -2f])
        {
            foreach (var alongM in (ReadOnlySpan<Vector2>)[Vector2.UnitX, Vector2.UnitY])
            {
                var atM = placeM + (alongM * offM);
                Console.WriteLine(
                    $"  {atM.X,9:F2},{atM.Y,-9:F2} {shapes.At(atM),-13} "
                    + $"{rings.OffTheKerbM(atM),7:F3} m off the kerb");
            }
        }
    }

    /// <summary>How finely an extruded ring is walked when asking whether it kept its distance. A stride.</summary>
    const float ExtrudedStationM = 1f;

    /// <summary>
    /// <b>Whether the distances read off the shell are the distances they say they are</b> — the one reading
    /// everything laid off the ring rests on (<see cref="LaneShell.Extruded"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The kerb is asked of the driven ground, and the rest are asked of the kerb.</b> A ring extruded by
    /// nought is meant to stand on the edge of the ground the town is driven along, so what it is weighed
    /// against is the other reading of that ground (<see cref="Kerbs.OffTheDrivenM"/>) — the two
    /// constructions disagreeing is the fact worth knowing. Every further distance is weighed against the
    /// ring it was struck from rather than against the bands, because that is the relation a band's width
    /// actually is.
    /// </para>
    /// <para>
    /// <b>A reading and not a claim.</b> What it costs to be wrong is a pavement on the road, and the
    /// numbers say how near that the town stands.
    /// </para>
    /// </remarks>
    static void Extruded(LaneShell shell, Paving paving, SimConfig config)
    {
        var started = Stopwatch.GetTimestamp();
        var rings = GroundRings.Of(paving, config);
        var kerb = rings.At(0f).ToArray();
        var walk = rings.At(paving.WalkM).ToArray();
        var elapsed = Stopwatch.GetElapsedTime(started);

        Console.WriteLine();
        Console.WriteLine(
            $"extruded — {Rings(kerb)} of {shell.Chains.Length} rings carry a kerb and {Rings(walk)} a "
            + $"pavement's outer edge, indexed and struck in {elapsed.TotalMilliseconds:F0} ms");

        var offM = new List<float>();
        foreach (var ring in kerb) Walked(ring, offM, pointM => paving.Kerbs.OffTheDrivenM(pointM));

        Say("  kerb off the driven  ", offM, "m out, the two readings of one edge");

        offM.Clear();
        foreach (var ring in kerb) Walked(ring, offM, rings.OffTheKerbM);

        Say("  kerb off itself      ", offM, "m out, which is what the index answers a point on it");

        offM.Clear();
        var atM = new List<Vector2>();
        var onM = new List<float>();
        foreach (var ring in walk)
        {
            Walked(ring, offM, pointM => rings.OffTheKerbM(pointM) - paving.WalkM, atM, onM);
        }

        Say("  pavement off the kerb", offM, $"m over the {paving.WalkM:F2} m it was struck at");
        Worst(offM, atM, onM);

        var halfM = paving.WalkM * 0.5f;
        offM.Clear();
        atM.Clear();
        onM.Clear();
        foreach (var ring in rings.At(halfM))
        {
            Walked(ring, offM, pointM => rings.OffTheKerbM(pointM) - halfM, atM, onM);
        }

        Say("  lane off the kerb    ", offM, $"m over the {halfM:F2} m it was struck at");
        Worst(offM, atM, onM);
        Wet(kerb, paving, config);
        Nodes(paving, config);
        Inward(rings, config);
        Kinks(rings, config);
    }

    /// <summary>
    /// <b>Where a named line is not smooth, and what the corner is made of</b> — every joint of the laid
    /// chain, the angle it turns, and the length of line either side of it.
    /// </summary>
    /// <remarks>
    /// <b>The arms are what name the fault.</b> A joint with a long arm either side is the line really
    /// turning a corner — a kerb goes round a junction and a trimmed fold meets its other branch — and
    /// belongs there. A sharp turn with a <em>short</em> arm is one of two things and the length says which:
    /// a step of about a band's difference is the offset jumping where the ring changes which driven line it
    /// is running down, and a long straight arriving at nothing is a gap the closure gave up on. Neither is
    /// a smoothing problem, and smoothing either would move the line off the distance it was struck at.
    /// </remarks>
    static void Kinks(GroundRings rings, SimConfig config)
    {
        // The shell's own chains first, because a kink the boundary already has is not the extrusion's to
        // answer for: the ring runs down the driven lines and a stretch handed over to another can turn
        // whatever the two lines make between them.
        Kinked("shell", rings.Shell.Chains);

        foreach (var line in (ReadOnlySpan<GroundLine>)[GroundLine.Kerb, GroundLine.Roadside])
        {
            Kinked(line.ToString().ToLowerInvariant(), rings.At(line));
        }

        static void Kinked(string what, ReadOnlySpan<ArcSeg[]> lines)
        {
            var joints = 0;
            var sharp = new List<(float TurnRad, float ArmM, float StepM, Vector2 AtM)>();
            foreach (var ring in lines)
            {
                for (var at = 0; at < ring.Length; at++)
                {
                    var arriving = ring[at];
                    var leaving = ring[(at + 1) % ring.Length];
                    var turnRad = Spline.WrapRad(leaving.HeadingRad - arriving.HeadingAtRad(arriving.LengthM));
                    joints++;
                    if (MathF.Abs(turnRad) < KinkRad) continue;

                    var armM = MathF.Min(arriving.LengthM, leaving.LengthM);
                    sharp.Add((turnRad, armM, MathF.Min(arriving.LengthM, leaving.LengthM), leaving.StartM));
                }
            }

            var shortArmed = sharp.Count(kink => kink.ArmM <= KinkArmM);
            var halfTurned = sharp.Count(kink => MathF.Abs(kink.TurnRad) >= HalfTurnRad);
            Console.WriteLine(
                $"  {what,-9} kinks    {sharp.Count} of {joints} joints turn over "
                + $"{KinkRad * 180f / MathF.PI:F0}°, {shortArmed} with an arm under {KinkArmM:F2} m, "
                + $"{halfTurned} turning a half circle");

            foreach (var kink in sharp.OrderByDescending(kink => MathF.Abs(kink.TurnRad)).Take(Listed / 5))
            {
                Console.WriteLine(
                    $"      {kink.TurnRad * 180f / MathF.PI,6:F0}° arm {kink.ArmM,6:F2} m "
                    + $"at {kink.AtM.X:F1},{kink.AtM.Y:F1}");
            }
        }
    }

    /// <summary>
    /// How far round is a half circle for this reading — a degree short of one. A turn this far round is a
    /// line doubling back on itself, and the offset of such a corner is a cap and not a point.
    /// </summary>
    const float HalfTurnRad = MathF.PI - (MathF.PI / 180f);

    /// <summary>
    /// How far round a joint of a laid line has to turn to be worth reading. A sixth of a right angle: a
    /// line walked at quarter-metre stations and laid back to a two-centimetre sag turns less than that
    /// between pieces wherever it is doing what it was asked to.
    /// </summary>
    const float KinkRad = MathF.PI / 12f;

    /// <summary>
    /// How short an arm makes a corner a fault rather than a corner. Two stations of the walk the line was
    /// struck from: a kerb rounding a junction has metres either side of it.
    /// </summary>
    const float KinkArmM = 0.5f;

    /// <summary>
    /// <b>Whether every named line's normal points inside the perimeter</b> (<see cref="GroundLine"/>): a
    /// line is walked with the driven ground on the walker's right, so the right of travel is the inward
    /// side on the ring round the town and on the ring round every block it encloses.
    /// </summary>
    /// <remarks>
    /// <b>It is the one thing a reader of these lines cannot check for itself.</b> Anything laid along a
    /// named line takes its own inward side off the line's own direction — which way to face a kerb, which
    /// way to turn a barb, which side of a stretch the town is — and a ring whose walk came out the other
    /// way round hands every one of those back inverted while still looking like a perfectly good closed
    /// line. Read here by stepping off the line both ways and asking the kerb which step went in.
    /// </remarks>
    static void Inward(GroundRings rings, SimConfig config)
    {
        foreach (var line in (ReadOnlySpan<GroundLine>)[GroundLine.Kerb, GroundLine.Roadside])
        {
            var wantedM = GroundRings.OutM(line, config);
            var outward = 0;
            var asked = 0;
            var stations = 0;
            var atM = new List<Vector2>();
            foreach (var ring in rings.At(line))
            {
                foreach (var arc in ring)
                {
                    var steps = Math.Max(1, (int)MathF.Ceiling(arc.LengthM / ExtrudedStationM));
                    for (var step = 0; step < steps; step++)
                    {
                        var alongM = arc.LengthM * step / steps;
                        var onM = arc.PointAtM(alongM);
                        stations++;

                        // <b>Only where the line is standing where it says it is.</b> The question is which
                        // way round the walk went, and the kerb answers with the *nearest* kerb — so at a
                        // station the closure left short, or one whose own kerb is further off than some
                        // other piece of boundary, the step that goes inward is the step that goes away from
                        // whatever is answering. Those stations are already counted as off their figure; a
                        // second reading of them says nothing about the winding.
                        if (MathF.Abs(rings.OffTheKerbM(onM) - wantedM) > OffTheFigureM) continue;

                        asked++;
                        var inward = Heading.RightOf(Heading.Unit(arc.HeadingAtRad(alongM)));
                        if (rings.OffTheKerbM(onM + (inward * InwardStepM))
                            < rings.OffTheKerbM(onM - (inward * InwardStepM))) continue;

                        outward++;
                        if (atM.Count < Listed / 5) atM.Add(onM);
                    }
                }
            }

            Console.WriteLine(
                $"  {line.ToString().ToLowerInvariant(),-9} normals  {outward} of {asked} point out of the "
                + $"perimeter rather than into it, at {wantedM:F2} m off the kerb "
                + $"({stations - asked} of {stations} not asked, standing off their own figure)");
            foreach (var placeM in atM) Console.WriteLine($"      at {placeM.X:F1},{placeM.Y:F1}");
        }
    }

    /// <summary>
    /// How far off a line the step is taken when asking which way is inward. A tenth of a metre: far enough
    /// that the two steps straddle the line by more than the millimetre two computations of one distance
    /// disagree by, and near enough that nothing else is nearer than the line itself.
    /// </summary>
    const float InwardStepM = 0.1f;

    /// <summary>
    /// <b>Whether anything the town stands along its streets is standing in the road</b> — a building or a
    /// car park's own rectangle, walked and asked of the finished ground.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the one question the generator cannot ask itself.</b> Every stage reads the ground as it
    /// stands, which is right, but one of them adds driven ground <em>and</em> clears things against it: a
    /// car park is the ways driven into it (GEN-4b) and those move the edge of the driven ground, so a
    /// building cleared before the lots went down was cleared against a boundary the finished town does not
    /// have (<c>SlotStage</c>). Only a reading taken after everything is laid can say whether that left
    /// anything in the road.
    /// </para>
    /// <para>
    /// <b>Asked of the ground and not of the boundary</b>, because a car park's own tarmac is driven ground
    /// a building may legitimately back onto (GEN-4b) — what is wrong is a wall standing where a car goes,
    /// which is what <c>GroundShapes.At</c> answers and no distance does.
    /// </para>
    /// </remarks>
    static void Built(CityPlan plan, Paving paving, SimConfig config)
    {
        var shapes = new GroundShapes(paving, config);
        var buildings = plan.Buildings;
        var inTheRoad = new List<(Vector2 AtM, CityGen.Ground Ground)>();
        for (var building = 0; building < buildings.CentreM.Length; building++)
        {
            var halfM = buildings.SizeM[building] * 0.5f;
            var along = Heading.Unit(buildings.HeadingRad[building]);
            var across = Heading.RightOf(along);
            for (var downM = -halfM.X; downM <= halfM.X; downM += WetStrideM)
            {
                for (var acrossM = -halfM.Y; acrossM <= halfM.Y; acrossM += WetStrideM)
                {
                    var atM = buildings.CentreM[building] + (along * downM) + (across * acrossM);
                    var ground = shapes.At(atM);
                    if (ground is not (CityGen.Ground.Road or CityGen.Ground.Intersection
                        or CityGen.Ground.Crosswalk or CityGen.Ground.Parking)) continue;

                    inTheRoad.Add((AtM: atM, Ground: ground));
                    downM = halfM.X + 1f;
                    break;
                }
            }
        }

        Console.WriteLine(
            $"  buildings in the road {inTheRoad.Count} of {buildings.CentreM.Length} cover ground a car is "
            + "driven over");
        for (var at = 0; at < Math.Min(Listed / 5, inTheRoad.Count); at++)
        {
            Console.WriteLine($"      at {inTheRoad[at].AtM.X:F1},{inTheRoad[at].AtM.Y:F1} — {inTheRoad[at].Ground}");
        }
    }

    /// <summary>
    /// <b>Whether every node of the plan stands on ground a car is driven over</b> — a junction is the
    /// movements that cross in it (TER-5) and has no shape of its own, so a node whose arms are cut back
    /// further than anything reaches is a place in the plan with no tarmac under it
    /// (<c>docs/index.md#known-gaps</c>).
    /// </summary>
    /// <remarks>
    /// <b>It is asked of the node's own point and of nothing else.</b> What the boundary encloses is the
    /// ground the arms and the movements lay between them, so a node the boundary encloses is a node with
    /// tarmac under it whether or not any one movement covers it — which is the whole of why a junction
    /// needs no shape. A node the boundary does not enclose is the fault, and what it then reads is
    /// whatever the town lays beside a kerb: pavement where the lines off the kerb are laid, and the grass
    /// they were laid over where they are held back, which is the same fault wearing the stage's clothes.
    /// </remarks>
    static void Nodes(Paving paving, SimConfig config)
    {
        var shapes = new GroundShapes(paving, config);
        var rings = GroundRings.Of(paving, config);
        var junctions = paving.Of.Junctions;
        var dry = new List<(Vector2 AtM, CityGen.Ground Ground, float OffM)>();
        for (var junction = 0; junction < junctions.Count; junction++)
        {
            var atM = junctions.CentreM[junction];
            var ground = shapes.At(atM);
            if (ground is CityGen.Ground.Road or CityGen.Ground.Intersection or CityGen.Ground.Crosswalk
                or CityGen.Ground.Parking) continue;

            dry.Add((atM, ground, rings.OffTheKerbM(atM)));
        }

        Console.WriteLine(
            $"  nodes off the tarmac {dry.Count} of {junctions.Count} stand on ground no car is driven over");
        for (var at = 0; at < Math.Min(Listed / 5, dry.Count); at++)
        {
            Console.WriteLine(
                $"      at {dry[at].AtM.X:F1},{dry[at].AtM.Y:F1} — {dry[at].Ground}, "
                + $"{dry[at].OffM:F2} m off the kerb");
        }
    }

    /// <summary>
    /// <b>Where the boundary stands over water that no deck carries it across</b> — the one place the
    /// picture and the answer agree about something the town should not have
    /// (<c>docs/index.md#known-gaps</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The ground a junction shares beats the water, and it has to.</b> A carriageway on a bridge is
    /// inside the boundary and over a river, so a point the boundary encloses reads as driven ground
    /// whatever is under it (<c>GroundShapes.At</c>, TER-5) — asked the other way round, every deck in the
    /// town would come back wet. What that costs is that a boundary standing anywhere else over water reads
    /// as driven ground too, and the picture fills it to match.
    /// </para>
    /// <para>
    /// <b>So the reading is asked underneath the answer</b> (<c>GroundShapes.OverWater</c>): the kerb walked
    /// station by station, counting the metres of it standing over water with no deck beneath them. Nought
    /// is the town having no such place; anything else is the boundary out over a river, and the coordinates
    /// say where to look.
    /// </para>
    /// </remarks>
    static void Wet(ArcSeg[][] kerb, Paving paving, SimConfig config)
    {
        var shapes = new GroundShapes(paving, config);
        var wetM = new List<Vector2>();
        var stations = 0;
        foreach (var ring in kerb)
        {
            if (ring.Length == 0) continue;

            var lengthM = Spline.TotalLengthM(ring);
            var steps = Math.Max(1, (int)MathF.Ceiling(lengthM / ExtrudedStationM));
            for (var step = 0; step < steps; step++)
            {
                var pointM = Spline.SampleAt(ring, lengthM * step / steps).PositionM;
                stations++;
                if (shapes.OverWater(pointM) && !shapes.OnADeck(pointM)) wetM.Add(pointM);
            }
        }

        Console.WriteLine(
            $"  kerb over water      {wetM.Count} of {stations} stations stand over water with no deck under them");
        for (var at = 0; at < Math.Min(Listed / 5, wetM.Count); at++)
        {
            Console.WriteLine($"      at {wetM[at].X:F1},{wetM[at].Y:F1}");
        }

        // And the same question asked of the water rather than of the boundary, because a body of water
        // small enough to stand wholly inside the driven ground is one the boundary never crosses the edge
        // of. Stepped at a stride, which is finer than anything the boundary does.
        var drownedM = new List<Vector2>();
        var sampled = 0;
        var worldM = paving.Of.WorldSizeM;
        for (var yM = 0f; yM < worldM.Y; yM += WetStrideM)
        {
            for (var xM = 0f; xM < worldM.X; xM += WetStrideM)
            {
                var pointM = new Vector2(xM, yM);
                if (!shapes.OverWater(pointM)) continue;

                sampled++;
                if (shapes.OnADeck(pointM)) continue;
                if (shapes.At(pointM) is CityGen.Ground.Water or CityGen.Ground.Sidewalk
                    or CityGen.Ground.Grass) continue;

                drownedM.Add(pointM);
            }
        }

        Console.WriteLine(
            $"  water driven over    {drownedM.Count} of {sampled} places over water answer as ground a car is "
            + "driven on, with no deck under them");
        for (var at = 0; at < Math.Min(Listed / 5, drownedM.Count); at++)
        {
            Console.WriteLine(
                $"      at {drownedM[at].X:F1},{drownedM[at].Y:F1} — {shapes.At(drownedM[at])}");
        }
    }

    /// <summary>How finely the town is stepped when asking what stands over its water. A stride.</summary>
    const float WetStrideM = 1f;

    /// <summary>
    /// <b>Where the distances came out worst, furthest first</b> — the reading that says whether a worst
    /// figure is one place worth looking at or a fault running the length of a town.
    /// </summary>
    static void Worst(List<float> offM, List<Vector2> atM, List<float>? onM = null)
    {
        var order = new int[offM.Count];
        for (var at = 0; at < order.Length; at++) order[at] = at;

        Array.Sort(order, (one, other) => MathF.Abs(offM[other]).CompareTo(MathF.Abs(offM[one])));
        var listed = Math.Min(Listed / 5, order.Length);
        for (var at = 0; at < listed; at++)
        {
            if (MathF.Abs(offM[order[at]]) < 0.1f) break;

            Console.WriteLine(
                $"      {offM[order[at]],7:F2} m out at {atM[order[at]].X:F1},{atM[order[at]].Y:F1}"
                + (onM is null ? "" : $" on a {onM[order[at]]:F2} m piece"));
        }

        if (onM is null) return;

        // How much of the reading is the middle of a long piece rather than a station of the walk, which is
        // the difference between the closure giving up and the rule keeping what it should not have.
        var offAStraight = 0;
        var deep = 0;
        var inside = 0;
        var insideOnAStation = 0;
        for (var at = 0; at < offM.Count; at++)
        {
            if (MathF.Abs(offM[at]) < OffTheFigureM) continue;

            deep++;
            if (onM[at] > ExtrudedStationM * 2f) offAStraight++;
            if (offM[at] > 0f) continue;

            inside++;
            if (onM[at] <= ExtrudedStationM * 2f) insideOnAStation++;
        }

        Console.WriteLine(
            $"      {deep} stations stand over {OffTheFigureM:F2} m off the figure, {offAStraight} of them "
            + "on a piece longer than two strides");

        // <b>And how many of them are nearer the kerb than the figure says</b>, which is the half that
        // costs something: a line struck short of its own distance is a pavement on the road, where one
        // struck past it is a pavement on the grass. It is what ranks one keep rule against another
        // (<c>Extrusion.Of</c>) — the two fail in opposite directions, so a count that does not say which
        // direction cannot choose between them.
        Console.WriteLine(
            $"      {inside} of those stand nearer the kerb than the figure, {insideOnAStation} of them on a "
            + "station of the walk");
    }

    /// <summary>
    /// How far off its own figure a station has to read before it is a fault rather than the rounding two
    /// computations of one distance disagree by. A tenth of a metre.
    /// </summary>
    const float OffTheFigureM = 0.1f;

    static int Rings(ArcSeg[][] rings)
    {
        var count = 0;
        foreach (var ring in rings)
        {
            if (ring.Length > 0) count++;
        }

        return count;
    }

    /// <summary>One ring walked at stations, each answered by the reading given.</summary>
    /// <param name="onM">
    /// How long the piece of the ring each station was sampled on is. <b>It is what tells a station that is
    /// wrong from a straight that is wrong</b>: the extrusion keeps its distance <em>at</em> its stations by
    /// construction, so a reading metres off the figure is either a station the rule should have dropped or
    /// the middle of a long straight the fold closure gave up and drew (<c>Extrusion.Closed</c>) — and the
    /// two want opposite fixes. Every corner of a laid ring is a station; everything between two of them is
    /// a piece.
    /// </param>
    static void Walked(
        ReadOnlySpan<ArcSeg> ring, List<float> into, Func<Vector2, float> reading, List<Vector2>? atM = null,
        List<float>? onM = null)
    {
        if (ring.Length == 0) return;

        foreach (var arc in ring)
        {
            var stations = Math.Max(1, (int)MathF.Ceiling(arc.LengthM / ExtrudedStationM));
            for (var station = 0; station < stations; station++)
            {
                var pointM = arc.PointAtM(arc.LengthM * station / stations);
                into.Add(reading(pointM));
                atM?.Add(pointM);
                onM?.Add(arc.LengthM);
            }
        }
    }

    static void Say(string what, List<float> offM, string against)
    {
        if (offM.Count == 0)
        {
            Console.WriteLine($"{what}    nothing walked");
            return;
        }

        var sorted = offM.ToArray();
        Array.Sort(sorted);
        var worst = 0f;
        foreach (var off in sorted) worst = MathF.Max(worst, MathF.Abs(off));

        Console.WriteLine(
            $"{what}  median {sorted[sorted.Length / 2]:F3} {against}, "
            + $"worst {worst:F3} m over {offM.Count} stations");
    }

    /// <summary>
    /// <b>Every stretch standing about one place, and what each carries the outside on to</b> — the reading a
    /// corner that came out wrong is diagnosed from, since a fault there is one stretch handed to the wrong
    /// neighbour and the table says which neighbours it had.
    /// </summary>
    /// <remarks>
    /// <b>The two edge points are what the pairing weighed and the metres are what is drawn.</b> A hand-over
    /// is offered on how near one stretch's leaving edge stands to the next one's arriving edge, and drawn as
    /// the straight between the two <em>lines</em> — so a pair whose edges met exactly can still leave a
    /// band's width of straight, and a pair whose edges are a street apart was never on offer at all.
    /// </remarks>
    static void About(ShellReading reading, Paving paving, Vector2 placeM)
    {
        var near = reading.Stood
            .Where(stood =>
                Vector2.Distance(stood.ArrivesAtM, placeM) <= AboutM
                || Vector2.Distance(stood.LeavesAtM, placeM) <= AboutM)
            .OrderBy(stood => MathF.Min(
                Vector2.Distance(stood.ArrivesAtM, placeM), Vector2.Distance(stood.LeavesAtM, placeM)))
            .ToArray();

        Console.WriteLine();
        Console.WriteLine(
            $"about {placeM.X:F1},{placeM.Y:F1} — {near.Length} stretches with an end within {AboutM:F0} m");
        if (near.Length == 0) return;

        Console.WriteLine(
            $"{"at",6}{"line",12}{"side",7}{"from m",9}{"to m",9}{"onto",7}{"gap m",8}  why        cut    "
            + "arrives at        leaves at");

        for (var at = 0; at < near.Length && at < Listed; at++)
        {
            var stood = near[at];
            Console.WriteLine(
                $"{stood.At,6}{$"{stood.Line} {Kind(paving, stood.Line)}",12}"
                + $"{(stood.OnTheLeft ? "left" : "right"),7}{stood.FromM,9:F2}{stood.ToM,9:F2}"
                + $"{stood.Follows,7}{stood.GapM,8:F2}  {stood.Corner,-9}  {(stood.AcrossACut ? "cut" : "met"),-6}"
                + $"{stood.ArrivesAtM.X,8:F1},{stood.ArrivesAtM.Y:F1}{stood.LeavesAtM.X,10:F1},{stood.LeavesAtM.Y:F1}"
                + (stood.Dropped ? "   dropped" : string.Empty));
        }

        if (near.Length > Listed) Console.WriteLine($"{"",25}… and {near.Length - Listed} further off");

        Passing(reading, paving, placeM);
    }

    /// <summary>
    /// <b>Every driven line running through the place, and how much of it the shell found to be the
    /// outside.</b> A line with no metres here is one whose own band edge is covered the whole way, and a
    /// corner drawn as a chord of one is a corner the shell had a line for and did not take.
    /// </summary>
    static void Passing(ShellReading reading, Paving paving, Vector2 placeM)
    {
        var keptM = new Dictionary<int, float>();
        foreach (var stood in reading.Stood)
        {
            if (stood.Dropped) continue;

            keptM.TryGetValue(stood.Line, out var wasM);
            keptM[stood.Line] = wasM + (stood.ToM - stood.FromM);
        }

        Console.WriteLine();
        Console.WriteLine(
            $"{"line",12}{"width m",9}{"length m",10}{"outside m",11}  runs from        to               "
            + "nearest point of it");
        var listed = 0;
        var passing = 0;
        for (var line = 0; line < paving.DrivenCount; line++)
        {
            var arcs = paving.ArcsOfDriven(line);
            if (arcs.Length == 0) continue;

            var nearestM = float.PositiveInfinity;
            var atM = 0f;
            var alongM = 0f;
            foreach (var arc in arcs)
            {
                for (var stepM = 0f; stepM <= arc.LengthM; stepM += WalkedM)
                {
                    var offM = Vector2.Distance(arc.PointAtM(stepM), placeM);
                    if (offM >= nearestM) continue;

                    nearestM = offM;
                    atM = alongM + stepM;
                }

                alongM += arc.LengthM;
            }

            if (nearestM > AboutM) continue;

            passing++;
            if (listed++ >= Listed) continue;

            var on = Spline.SampleAt(arcs, MathF.Min(atM, alongM));
            Console.WriteLine(
                $"{$"{line} {Kind(paving, line)}",12}{paving.DrivenWidthM(line),9:F2}{alongM,10:F2}"
                + $"{(keptM.TryGetValue(line, out var outsideM) ? outsideM : 0f),11:F2}  "
                + $"{arcs[0].StartM.X:F1},{arcs[0].StartM.Y:F1}{arcs[^1].EndM.X,10:F1},{arcs[^1].EndM.Y:F1}"
                + $"{on.PositionM.X,12:F1},{on.PositionM.Y:F1} at {atM:F2} m, {nearestM:F2} m off");
        }

        Console.WriteLine(
            $"{passing} driven lines pass within {AboutM:F0} m"
            + (passing > Listed ? $", {passing - Listed} of them not listed" : string.Empty));
    }

    /// <summary>
    /// What one driven line is, said the way <see cref="Paving.ArcsOfDriven"/> numbers them — which is the
    /// first thing a reader wants of a corner that came out wrong, since a lane, a movement through a box
    /// and a way into a bay fail at a corner for different reasons.
    /// </summary>
    static string Kind(Paving paving, int line) =>
        line < paving.Lanes.LaneCount ? "lane"
        : line - paving.Lanes.LaneCount < paving.Lanes.ConnectorCount ? "box"
        : "bay";

    /// <summary>
    /// <b>How near the two ends of a meeting actually stood</b>, worst first — which is the error in finding
    /// the crossing they both stop at and not a property of the town.
    /// </summary>
    /// <remarks>
    /// <b>It is the reading that says whether the cut is solved or searched for.</b> Two band edges cross at
    /// a point, so every one of these is nought and what is printed is how far the arithmetic missed by; the
    /// radius the pairing offers (<c>LaneShell.MeetM</c>) has to cover the worst of them, and a radius wide
    /// enough to cover a decimetre is wide enough to hand a car park's bay to the wrong neighbour.
    /// </remarks>
    static void Met(ShellReading reading)
    {
        var met = reading.MetAtM.OrderByDescending(apartM => apartM).ToArray();
        if (met.Length == 0) return;

        var over = met.Count(apartM => apartM > Kerbs.OnePlaceM);
        Console.WriteLine(
            $"cuts solved — {reading.CutSolved} stretch ends stand on a band's own boundary, "
            + $"{reading.CutWalked} were bisected because no band there explains them");
        Console.WriteLine(
            $"met — {met.Length} pairs stopped at one crossing, the worst of them {met[0] * 1000f:F0} mm apart, "
            + $"{over} over the {Kerbs.OnePlaceM * 1000f:F0} mm that is one place");
        Console.WriteLine(
            $"      median {met[met.Length / 2] * 1000f:F1} mm, "
            + $"{met.Count(apartM => apartM > Kerbs.JoinedM)} over a centimetre, "
            + $"{met.Count(apartM => apartM > Kerbs.RoundingM)} over a millimetre");
        Console.WriteLine();
    }

    /// <summary>
    /// <b>The hand-overs where the ring doubles back, sharpest first</b>, and how much straight was drawn
    /// across all of them. The two points are a framing to point <c>--shot</c> at.
    /// </summary>
    /// <remarks>
    /// <b>The turn is measured onto the straight and off it, and the sharper of the two is the reading.</b>
    /// A hand-over that goes out and comes back turns a half turn onto its straight and a half turn off it
    /// while arriving pointed much as it left, so the turn from one line to the next says nothing about it.
    /// And what is not a spike shows as one on neither: the outside really does reverse at the back of a
    /// car park and round the end of a one-way street's band, and both cross their straight square on.
    /// </remarks>
    static void Handovers(ShellReading reading, Paving paving)
    {
        var totalM = 0f;
        foreach (var handed in reading.Handovers) totalM += handed.LengthM;

        var spikes = reading.Handovers
            .Where(handed => MathF.Abs(handed.TurnRad) > SpikeRad)
            .OrderByDescending(handed => MathF.Abs(handed.TurnRad))
            .ToArray();

        // <b>A ring may double back without a spike standing out of the corner</b>, and two ways: it turns
        // at a point, or it turns onto the line it was already on and is drawn back down its own arcs.
        var unseen = spikes.Count(
            handed => handed.Line == handed.OtherLine || handed.LengthM <= Kerbs.OnePlaceM);

        Console.WriteLine(
            $"hand-overs — {totalM:F0} m of straight drawn across them, {spikes.Length} doubling back past "
            + $"{SpikeRad * 180f / MathF.PI:F0}°, {spikes.Length - unseen} of them off the line they turn back along");
        if (spikes.Length == 0)
        {
            Console.WriteLine();
            return;
        }

        Console.WriteLine($"{"turn",7}{"m",8}{"line",8}{"onto",8}   from            to");
        for (var at = 0; at < spikes.Length && at < Listed; at++)
        {
            var spike = spikes[at];
            Console.WriteLine(
                $"{spike.TurnRad * 180f / MathF.PI,6:F0}°{spike.LengthM,8:F2}{spike.Line,8}{spike.OtherLine,8}   "
                + $"{spike.FromM.X:F1},{spike.FromM.Y:F1}  to  {spike.ToM.X:F1},{spike.ToM.Y:F1}");
        }

        if (spikes.Length > Listed) Console.WriteLine($"{"",31}… and {spikes.Length - Listed} shallower");

        Console.WriteLine();
    }

    /// <summary>
    /// <b>The longest straights drawn where the ground was never cut</b>, longest first: a hand-over between
    /// two lines that both run over the same tarmac and were still not carried onto one another.
    /// </summary>
    /// <remarks>
    /// <b>It is the reading a notch in a corner shows up on and a spike does not.</b> A corner that misses by
    /// a metre turns nothing sharp enough to be read as doubling back, so the turn says it is fine; what says
    /// it is not is that a straight was drawn across open tarmac at all. Across a cut — the end of a road, the
    /// back of a car park — a straight is the answer and those are left out.
    /// </remarks>
    static void Corners(ShellReading reading, Paving paving)
    {
        var notches = reading.Handovers
            .Where(handed => !handed.AcrossACut && handed.LengthM > Kerbs.OnePlaceM)
            .OrderByDescending(handed => handed.LengthM)
            .ToArray();

        var notchedM = 0f;
        foreach (var handed in notches) notchedM += handed.LengthM;

        Console.WriteLine(
            $"corners — {notches.Length} drew a straight over ground nothing cut, {notchedM:F0} m of it");
        if (notches.Length == 0)
        {
            Console.WriteLine();
            return;
        }

        var why = new int[Enum.GetValues<ShellCorner>().Length];
        foreach (var notch in notches) why[(int)notch.Corner]++;

        Console.Write($"{"",10}");
        foreach (var corner in Enum.GetValues<ShellCorner>()) Console.Write($"{why[(int)corner]} {corner}   ");

        Console.WriteLine();
        Console.WriteLine(
            $"{"m",8}{"carried",9}{"turn",7}{"line",12}{"onto",12}  why        from            to");
        for (var at = 0; at < notches.Length && at < Listed; at++)
        {
            var notch = notches[at];
            Console.WriteLine(
                $"{notch.LengthM,8:F2}{notch.CarriedToM,9:F2}{notch.TurnRad * 180f / MathF.PI,6:F0}°"
                + $"{$"{notch.Line} {Kind(paving, notch.Line)}",12}"
                + $"{$"{notch.OtherLine} {Kind(paving, notch.OtherLine)}",12}  {notch.Corner,-9}  "
                + $"{notch.FromM.X:F1},{notch.FromM.Y:F1}  to  {notch.ToM.X:F1},{notch.ToM.Y:F1}");
        }

        if (notches.Length > Listed) Console.WriteLine($"{"",31}… and {notches.Length - Listed} shorter");

        Console.WriteLine();
    }

    /// <summary>
    /// <b>The longest straights drawn where the ground really is cut</b>, longest first — the other half of
    /// <see cref="Corners"/>, and the half a reader checks for a join that should never have been offered.
    /// </summary>
    /// <remarks>
    /// A cut is the end of a road, the back of a car park and the step at a mouth, and all three are a band's
    /// width or so of straight. <b>So the length is the diagnosis</b>: a cut longer than the carriageway it
    /// crosses is a hand-over reaching for whatever the ground would hold up, and one between two lines that
    /// carry on past it is a length of boundary struck across the middle of the town.
    /// </remarks>
    static void Cuts(ShellReading reading, Paving paving)
    {
        var cuts = reading.Handovers
            .Where(handed => handed.AcrossACut && handed.LengthM > Kerbs.OnePlaceM)
            .OrderByDescending(handed => handed.LengthM)
            .ToArray();

        var cutM = 0f;
        foreach (var cut in cuts) cutM += cut.LengthM;

        Console.WriteLine($"cuts — {cuts.Length} drew a straight where the ground is cut, {cutM:F0} m of it");
        if (cuts.Length == 0)
        {
            Console.WriteLine();
            return;
        }

        // <b>Which two kinds of line a cut joins is what says whether it is one.</b> A road's own lanes
        // capped on one another is the end of a street; the same pair at a mouth is the outside giving up on
        // the box in front of it, and there is nothing in the length to tell the two apart.
        var pairs = new Dictionary<string, (int Count, float LengthM)>(StringComparer.Ordinal);
        foreach (var cut in cuts)
        {
            var pair = $"{Kind(paving, cut.Line)}–{Kind(paving, cut.OtherLine)}";
            pairs.TryGetValue(pair, out var was);
            pairs[pair] = (was.Count + 1, was.LengthM + cut.LengthM);
        }

        Console.Write($"{"",8}");
        foreach (var (pair, of) in pairs.OrderByDescending(of => of.Value.Count))
        {
            Console.Write($"{of.Count} {pair} ({of.LengthM:F0} m)   ");
        }

        Console.WriteLine();
        Console.WriteLine($"{"m",8}{"turn",7}{"line",12}{"onto",12}  from            to");
        for (var at = 0; at < cuts.Length && at < Listed; at++)
        {
            var cut = cuts[at];
            Console.WriteLine(
                $"{cut.LengthM,8:F2}{cut.TurnRad * 180f / MathF.PI,6:F0}°"
                + $"{$"{cut.Line} {Kind(paving, cut.Line)}",12}{$"{cut.OtherLine} {Kind(paving, cut.OtherLine)}",12}  "
                + $"{cut.FromM.X:F1},{cut.FromM.Y:F1}  to  {cut.ToM.X:F1},{cut.ToM.Y:F1}");
        }

        if (cuts.Length > Listed) Console.WriteLine($"{"",25}… and {cuts.Length - Listed} shorter");

        Console.WriteLine();
    }

    /// <summary>
    /// <b>The straights drawn across the driven ground rather than along the edge of it</b>, deepest first.
    /// </summary>
    /// <remarks>
    /// <b>It is the one reading that does not take the shell's word for anything.</b> Every other table here
    /// is read off how the pairing classified a join; this one asks the town's own shape where the straight
    /// ended up (<see cref="Kerbs.OffTheDrivenM"/>), so a join the shell believes is a cut and the ground says
    /// is a chord through the middle of a road comes back here whatever the shell called it. A cap across the
    /// mouth of a car park is the shape it was opened for: the boundary there runs on round the bays, and the
    /// straight the ring drew instead stands half a carriageway in.
    /// </remarks>
    static void Across(ShellReading reading, Paving paving)
    {
        var across = reading.Handovers
            .Where(handed => handed.LengthM > Kerbs.OnePlaceM)
            .Select(handed => (Handed: handed, InM: -paving.Kerbs.OffTheDrivenM((handed.FromM + handed.ToM) * 0.5f)))
            .Where(of => of.InM >= AcrossM)
            .OrderByDescending(of => of.InM)
            .ToArray();

        var acrossM = 0f;
        foreach (var (handed, _) in across) acrossM += handed.LengthM;

        Console.WriteLine(
            $"across — {across.Length} straights stand {AcrossM:F1} m or more inside the driven ground rather "
            + $"than on its edge, {acrossM:F0} m of them");
        if (across.Length == 0)
        {
            Console.WriteLine();
            return;
        }

        Console.WriteLine($"{"in m",8}{"m",8}{"line",12}{"onto",12}  cut    where");
        for (var at = 0; at < across.Length && at < Listed; at++)
        {
            var (handed, inM) = across[at];
            Console.WriteLine(
                $"{inM,8:F2}{handed.LengthM,8:F2}"
                + $"{$"{handed.Line} {Kind(paving, handed.Line)}",12}"
                + $"{$"{handed.OtherLine} {Kind(paving, handed.OtherLine)}",12}  "
                + $"{(handed.AcrossACut ? "cut" : "met"),-6} "
                + $"{(handed.FromM.X + handed.ToM.X) * 0.5f:F1},{(handed.FromM.Y + handed.ToM.Y) * 0.5f:F1}");
        }

        if (across.Length > Listed) Console.WriteLine($"{"",25}… and {across.Length - Listed} shallower");

        Console.WriteLine();
    }

    /// <summary>
    /// <b>Where the ring leaves one line and comes straight back onto it</b>, the stretch between being a
    /// movement out of that very line — which is the outside following a turn where the line it was already
    /// on carries the boundary on.
    /// </summary>
    /// <remarks>
    /// A movement leaves the lane it serves at a few degrees and a way into a bay leaves it square, so both
    /// lie along the lane for their first metres and either can take the edge off it
    /// (<c>LaneShell.Walk.Owns</c>). Taken, the ring steps off the lane, runs a fraction of a metre of the
    /// turn and steps back — <b>a jog in what should be one line</b>, and along a street parked both sides
    /// there is one of them a bay apart the whole way. The reading is the count: a corner the ring really
    /// turns onto a movement does not come back to the line it left.
    /// </remarks>
    static void Dips(ShellReading reading, Paving paving)
    {
        var byAt = reading.Stood.ToDictionary(stood => stood.At);
        var dips = new List<(ShellStretch Off, ShellStretch Onto)>();
        foreach (var stood in reading.Stood)
        {
            if (stood.Dropped || stood.Follows < 0 || !byAt.TryGetValue(stood.Follows, out var onto)) continue;
            if (onto.Dropped || onto.Line == stood.Line || onto.Follows < 0) continue;
            if (!byAt.TryGetValue(onto.Follows, out var back) || back.Line != stood.Line) continue;

            dips.Add((stood, onto));
        }

        var borrowedM = 0f;
        var jogs = 0;
        foreach (var (_, onto) in dips)
        {
            borrowedM += onto.ToM - onto.FromM;
            if (onto.ToM - onto.FromM <= JogM) jogs++;
        }

        Console.WriteLine(
            $"dips — {dips.Count} times the ring stepped off a line onto one leaving it and back, "
            + $"{borrowedM:F0} m of the boundary carried by the turn; {jogs} of them under {JogM:F1} m");
        if (dips.Count == 0)
        {
            Console.WriteLine();
            return;
        }

        Console.WriteLine($"{"m",8}{"line",12}{"onto",12}  where");
        foreach (var (off, onto) in dips.OrderBy(dip => dip.Onto.ToM - dip.Onto.FromM).Take(Listed))
        {
            Console.WriteLine(
                $"{onto.ToM - onto.FromM,8:F2}{$"{off.Line} {Kind(paving, off.Line)}",12}"
                + $"{$"{onto.Line} {Kind(paving, onto.Line)}",12}  {off.LeavesAtM.X:F1},{off.LeavesAtM.Y:F1}");
        }

        if (dips.Count > Listed) Console.WriteLine($"{"",25}… and {dips.Count - Listed} longer");

        Console.WriteLine();
    }

    /// <summary>Every run walked, longest first, with what became of it.</summary>
    static void Runs(ShellReading reading)
    {
        var runs = reading.Runs.OrderByDescending(run => run.LengthM).ToArray();
        var kept = 0;
        var keptM = 0f;
        var droppedM = 0f;
        foreach (var run in reading.Runs)
        {
            if (run.Kept)
            {
                kept++;
                keptM += run.LengthM;
            }
            else droppedM += run.LengthM;
        }

        Console.WriteLine(
            $"runs — {reading.Runs.Count} walked, {kept} kept ({keptM / 1000f:F2} km), "
            + $"{reading.Runs.Count - kept} dropped ({droppedM / 1000f:F2} km)");

        // <b>The rings that were thrown away, and where each one broke.</b> The two points are a framing to
        // point --shot at: a run that would not close is a length of the town's edge nothing accounts for,
        // and it is the ground there that has to answer for it rather than the walking of it.
        Console.WriteLine($"{"stretches",11}{"lines",7}{"round m",10}{"gap m",9}  where it broke, and what became of it");
        var shown = 0;
        foreach (var run in runs)
        {
            if (run.Kept) continue;
            if (shown++ >= Listed) continue;

            Console.WriteLine(
                $"{run.Stretches,11}{run.Lines.Length,7}{run.LengthM,10:F1}{run.GapM,9:F3}  "
                + $"{run.TailM.X:F0},{run.TailM.Y:F0} to {run.HeadM.X:F0},{run.HeadM.Y:F0} — {run.Why}");
        }

        if (shown > Listed) Console.WriteLine($"{"",37}… and {shown - Listed} shorter");

        // <b>The join with no rule behind it.</b> Every other join in the shell is refused unless the ground
        // carries it, and the straight that shuts a ring is asked nothing — so a kept ring may be drawn over
        // grass or across the middle of a box. How many, and the widest gap any of them was shut over.
        var overGrass = 0;
        var shutOverAGap = 0;
        var widestM = 0f;
        foreach (var run in reading.Runs)
        {
            if (!run.Kept || !run.ShutOverAGap) continue;

            shutOverAGap++;
            widestM = MathF.Max(widestM, run.GapM);
            if (run.BareGapM > 0f) overGrass++;
        }

        Console.WriteLine(
            $"  {shutOverAGap} of the {kept} kept rings were shut with a straight nothing asked the ground "
            + $"about, the widest over {widestM:F2} m; {overGrass} of those leave the driven ground");
        foreach (var run in reading.Runs)
        {
            if (!run.Kept || !run.ShutOverAGap) continue;

            Console.WriteLine(
                $"{"",4}{run.GapM,8:F2} m  {run.TailM.X:F1},{run.TailM.Y:F1} to {run.HeadM.X:F1},{run.HeadM.Y:F1}");
        }
        Console.WriteLine();
    }

    /// <summary>
    /// The ends the pairing left over, nearest partner first. <b>The distance is the whole reading</b>: an
    /// end whose nearest is a millimetre off was not short of a partner, it lost the one it had.
    /// </summary>
    static void Loose(ShellReading reading, Paving paving)
    {
        if (reading.LooseEnds.Count == 0)
        {
            Console.WriteLine("ends left over — none");
            Console.WriteLine();
            return;
        }

        var ends = reading.LooseEnds.OrderBy(end => end.NearestM).ToArray();
        var within = 0;
        foreach (var end in ends)
        {
            if (end.NearestM <= Kerbs.OnePlaceM) within++;
        }

        Console.WriteLine(
            $"ends left over — {ends.Length}, of which {within} have a partner within {Kerbs.OnePlaceM:F2} m "
            + "(the bound on two ends that are one crossing)");
        Console.WriteLine($"{"line",10}{"x",10}{"y",10}{"nearest m",12}{"to line",13}  and was it already taken");

        for (var at = 0; at < ends.Length && at < Listed; at++)
        {
            var end = ends[at];
            Console.WriteLine(
                $"{$"{end.Line} {Kind(paving, end.Line)}",10}{end.AtM.X,10:F1}{end.AtM.Y,10:F1}{end.NearestM,12:F4}"
                + $"{$"{end.NearestLine} {Kind(paving, end.NearestLine)}",13}  "
                + $"{(end.NearestWasTaken ? "taken" : "free")}");
        }

        if (ends.Length > Listed) Console.WriteLine($"{"",25}… and {ends.Length - Listed} further off");

        Console.WriteLine();
    }

    /// <summary>
    /// <b>The lines the shell found on the boundary and handed over in nothing.</b> It is the answer to the
    /// question the instrument is opened for, and every line named here is a lane the perimeter layer draws
    /// no mark on although the ground says it is the outside.
    /// </summary>
    static void Lost(ShellReading reading, Paving paving)
    {
        var lost = reading.Lost();
        var lostM = 0f;
        foreach (var line in lost) lostM += paving.DrivenLengthM(line);

        Console.WriteLine(
            $"lines the outside runs along and no kept ring carries — {lost.Length} of {reading.Lines}, "
            + $"{lostM / 1000f:F2} km");
        for (var at = 0; at < lost.Length && at < Listed; at++)
        {
            Console.Write(at > 0 ? ", " : "  ");
            Console.Write(lost[at]);
        }

        if (lost.Length > Listed) Console.Write($", … and {lost.Length - Listed} more");
        if (lost.Length > 0) Console.WriteLine();
    }
}
