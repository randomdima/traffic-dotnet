using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.CityGen.Traced;

internal static partial class TracedStreets
{
    /// <summary>How finely a road is read for the buildings a wider walk would run into.</summary>
    const float WalkStepM = 2f;

    /// <summary>
    /// One road of a street as the street runs along it: from its <see cref="Road.From"/> where
    /// <see cref="Forward"/>, and how long it runs, junction to junction.
    /// </summary>
    readonly record struct Along(Road Road, bool Forward, float LengthM)
    {
        /// <summary>Its carriageway as the street runs: its lanes with the street, and the roadside beside their kerb.</summary>
        public Carriage Carriage => Forward ? Road.Carriage : Road.Carriage.Turned;

        public Along Turned => this with { Forward = !Forward };

        public void Lay(Carriage carriage) => Road.Carriage = Forward ? carriage : carriage.Turned;
    }

    /// <summary>A street's roads in the order it runs, and whether it starts and ends at a junction three or more meet at.</summary>
    readonly record struct Street(List<Along> Roads, bool StartsAtJunction, bool EndsAtJunction);

    /// <summary>
    /// <b>Every street's carriageway held even along it</b> (GEN-57): its lanes where they change and change back
    /// (<see cref="HeldThrough"/>), and where it loses them close short of a junction (<see cref="HeldShortOf"/>), and
    /// its roadsides on one side, none short and the rest along all of it that is driven the same ways
    /// (<see cref="Roadsides"/>) — so a road that then differs from the one it runs on into by nothing is that road
    /// carried on, and the place between them goes (<see cref="JoinedThrough"/>).
    /// </summary>
    /// <remarks>
    /// <b>A lane count and a roadside are read off a width measured way by way</b> (<see cref="Survey"/>), and a street's
    /// ways are cut wherever OSM's mappers cut them: one read a lane each way and a roadside at each kerb where the next
    /// read two lanes each way off the same width, and a single roadside stands beside whichever kerb its way was drawn
    /// along. What the town holds is the street.
    /// </remarks>
    static void Evened(List<Road> roads, List<Vector2> centreM, SimConfig config)
    {
        var figures = config.CityGen;
        var streets = Streets(roads, centreM, config);
        foreach (var street in streets)
        {
            HeldThrough(street.Roads, figures.TracedLanesHeldM);
            if (street.EndsAtJunction) HeldShortOf(street.Roads, figures.TracedLanesHeldShortOfJunctionM);
            if (street.StartsAtJunction) HeldShortOf(Reversed(street.Roads), figures.TracedLanesHeldShortOfJunctionM);
        }

        var carriageways = new Carriageways(roads, figures.TracedRoadsideWidthM);
        foreach (var street in streets)
        {
            var carriage = street.Roads[0].Carriage;
            if (carriage.Level != CityPlan.RoadArrays.Ground || carriage.Circulates) continue;

            foreach (var stretch in DrivenAlikeAlong(street.Roads)) Roadsides(stretch, centreM, carriageways, config);
        }
    }

    /// <summary>
    /// A street cut wherever it changes the ways it is driven — run one way, both, or the other — each stretch evened
    /// for its roadsides apart (<see cref="Roadsides"/>): a lane each way between two roadsides running on into a single
    /// lane one way is a slip or a narrows, which the roadsides would carry to twice its width.
    /// </summary>
    static List<List<Along>> DrivenAlikeAlong(List<Along> street)
    {
        var stretches = new List<List<Along>> { new() { street[0] } };
        for (var step = 1; step < street.Count; step++)
        {
            if (!DrivenAlike(street[step].Carriage.Lanes, street[step - 1].Carriage.Lanes)) stretches.Add([]);
            stretches[^1].Add(street[step]);
        }

        return stretches;
    }

    /// <summary>
    /// <b>Every street</b>: roads run on into each other through every place only two meet at, and through every junction
    /// they run straight on through where nothing else meeting there is a street (<see cref="Minor"/>) — each the way on
    /// nearest straight ahead from the other, within <see cref="RoadFigures.TurnStraightToleranceDeg"/> — and only on
    /// one level, round a roundabout or off it.
    /// </summary>
    static List<Street> Streets(List<Road> roads, List<Vector2> centreM, SimConfig config)
    {
        var at = new List<(Road Road, bool AtTo)>[centreM.Count];
        for (var junction = 0; junction < at.Length; junction++) at[junction] = [];
        foreach (var road in roads)
        {
            at[road.From].Add((road, false));
            at[road.To].Add((road, true));
        }

        var onward = new Dictionary<(Road Road, bool AtTo), (Road Road, bool AtTo)>();
        var leastCos = MathF.Cos(float.DegreesToRadians(config.Road.TurnStraightToleranceDeg));
        var outOf = new List<Vector2>();
        for (var junction = 0; junction < at.Length; junction++)
        {
            var arms = at[junction];
            if (arms.Count == 2)
            {
                if (arms[0].Road != arms[1].Road && Alike(arms[0].Road, arms[1].Road)) Joined(arms[0], arms[1]);
                continue;
            }

            if (arms.Count < 3) continue;

            var widestM = 0f;
            foreach (var (arm, _) in arms) widestM = MathF.Max(widestM, arm.Carriage.WidthM);

            var radiusM = config.JunctionRadiusAcrossM(widestM);
            outOf.Clear();
            foreach (var (arm, atTo) in arms) outOf.Add(OutOf(arm, atTo, centreM[junction], radiusM));

            for (var one = 0; one < arms.Count; one++)
            {
                var other = Straightest(outOf, one, leastCos);
                if (other < one || Straightest(outOf, other, leastCos) != one) continue;
                if (!Alike(arms[one].Road, arms[other].Road) || !OnlyMinorBeside(arms, one, other)) continue;

                Joined(arms[one], arms[other]);
            }
        }

        var streets = new List<Street>();
        var walked = new HashSet<Road>();
        foreach (var road in roads)
        {
            if (walked.Contains(road)) continue;

            // Back to where the street starts, or round a ring to this road again.
            var first = new Along(road, true, LengthOf(road));
            while (onward.TryGetValue((first.Road, !first.Forward), out var before) && before.Road != road)
            {
                first = new Along(before.Road, before.AtTo, LengthOf(before.Road));
            }

            var street = new List<Along>();
            for (var step = first; ;)
            {
                street.Add(step);
                walked.Add(step.Road);
                if (!onward.TryGetValue((step.Road, step.Forward), out var next) || walked.Contains(next.Road)) break;

                step = new Along(next.Road, !next.AtTo, LengthOf(next.Road));
            }

            var (start, end) = (street[0], street[^1]);
            streets.Add(new Street(
                street, at[start.Forward ? start.Road.From : start.Road.To].Count > 2, at[end.Forward ? end.Road.To : end.Road.From].Count > 2));
        }

        return streets;

        void Joined((Road Road, bool AtTo) one, (Road Road, bool AtTo) other)
        {
            onward[one] = other;
            onward[other] = one;
        }
    }

    /// <summary>Whether a street runs on from one road into another: on one level, and both round a roundabout or neither.</summary>
    static bool Alike(Road one, Road other) =>
        one.Carriage.Level == other.Carriage.Level && one.Carriage.Circulates == other.Carriage.Circulates;

    /// <summary>
    /// Whether two arms of a junction are streets and everything else meeting there is a way of no street's class
    /// (<see cref="Minor"/>).
    /// </summary>
    static bool OnlyMinorBeside(List<(Road Road, bool AtTo)> arms, int one, int other)
    {
        if (Minor(arms[one].Road.Highway) || Minor(arms[other].Road.Highway)) return false;

        for (var arm = 0; arm < arms.Count; arm++)
        {
            if (arm != one && arm != other && !Minor(arms[arm].Road.Highway)) return false;
        }

        return true;
    }

    /// <summary>
    /// <b>Whether a way is of no street's class</b> — a yard's, a car park's, a track — whose meeting a street is no
    /// junction of streets. A link is a street's own way on and off, and is not.
    /// </summary>
    static bool Minor(string highway) => Rank(highway) == 0 && !highway.EndsWith("_link", StringComparison.Ordinal);

    /// <summary>
    /// The arm of a junction whose way out is nearest straight ahead from the way in along another, no further off it
    /// than <paramref name="leastCos"/> allows, or −1.
    /// </summary>
    static int Straightest(List<Vector2> outOf, int arm, float leastCos)
    {
        var (straightest, mostCos) = (-1, leastCos);
        for (var other = 0; other < outOf.Count; other++)
        {
            var cos = -Vector2.Dot(outOf[arm], outOf[other]);
            if (other == arm || cos < mostCos) continue;

            (straightest, mostCos) = (other, cos);
        }

        return straightest;
    }

    /// <summary>Which way one arm leaves a junction: toward where its line leaves the junction's disc.</summary>
    static Vector2 OutOf(Road arm, bool atTo, Vector2 centreM, float radiusM)
    {
        var (leg, leavesM) = Leaving(arm.PointsM, centreM, radiusM, forward: !atTo);
        var outM = leg < 0 ? (atTo ? arm.PointsM[0] : arm.PointsM[^1]) : leavesM;
        return Vector2.Normalize(outM - centreM);
    }

    static float LengthOf(Road road)
    {
        var lengthM = 0f;
        for (var point = 1; point < road.PointsM.Count; point++) lengthM += Vector2.Distance(road.PointsM[point - 1], road.PointsM[point]);
        return lengthM;
    }

    static List<Along> Reversed(List<Along> street)
    {
        var reversed = new List<Along>(street.Count);
        for (var step = street.Count - 1; step >= 0; step--) reversed.Add(street[step].Turned);
        return reversed;
    }

    /// <summary>
    /// <b>A stretch of a street whose lanes change and change back is laid in the lanes either side of it</b>
    /// (<see cref="CityGenFigures.TracedLanesHeldM"/>): in the carriageway of the longer of the two roads either side,
    /// where those are alike in their lanes and the stretch is driven the ways they are.
    /// </summary>
    static void HeldThrough(List<Along> street, float heldM)
    {
        for (var first = 1; first + 1 < street.Count; first++)
        {
            var lanes = street[first - 1].Carriage.Lanes;
            if (street[first].Carriage.Lanes == lanes) continue;

            var lengthM = 0f;
            for (var last = first; last + 1 < street.Count; last++)
            {
                lengthM += street[last].LengthM;
                if (lengthM > heldM || !DrivenAlike(street[last].Carriage.Lanes, lanes)) break;
                if (street[last + 1].Carriage.Lanes != lanes) continue;

                var (before, after) = (street[first - 1], street[last + 1]);
                var held = before.LengthM >= after.LengthM ? before.Carriage : after.Carriage;
                for (var step = first; step <= last; step++) street[step].Lay(held);

                first = last;
                break;
            }
        }
    }

    /// <summary>
    /// <b>A street that loses lanes close short of the junction it ends at runs into it in the lanes it had</b>
    /// (<see cref="CityGenFigures.TracedLanesHeldShortOfJunctionM"/>): the roads that close to it are laid in the
    /// carriageway of the road before them, where that one runs more lanes toward the junction than any of them and they
    /// are driven the ways it is. One gaining lanes into it is as OSM has it.
    /// </summary>
    static void HeldShortOf(List<Along> street, float shortOfM)
    {
        var (lengthM, mostAfter, held) = (0f, 0, -1);
        for (var step = street.Count - 1; step >= 1; step--)
        {
            lengthM += street[step].LengthM;
            if (lengthM > shortOfM || !DrivenAlike(street[step].Carriage.Lanes, street[step - 1].Carriage.Lanes)) break;

            mostAfter = Math.Max(mostAfter, street[step].Carriage.Lanes.With);
            if (street[step - 1].Carriage.Lanes.With > mostAfter) held = step - 1;
        }

        if (held < 0) return;

        for (var step = held + 1; step < street.Count; step++) street[step].Lay(street[held].Carriage);
    }

    /// <summary>Whether two carriageways are driven the same ways, however many lanes each.</summary>
    static bool DrivenAlike(RoadLanes one, RoadLanes other) => (one.With > 0) == (other.With > 0) && (one.Against > 0) == (other.Against > 0);

    /// <summary>
    /// <b>A stretch of street's roadsides held even along it</b>, the stretch driven the same ways throughout: a single
    /// one beside the kerb it holds more of its roadside along, a stretch of roadside shorter than
    /// <see cref="CityGenFigures.TracedRoadsideShortestM"/> along a longer one taken off, and the rest carried on along
    /// every road of it that leaves clear of every other road
    /// (<see cref="KeepsClear"/>) — every road it is carried along laid as though measured wide enough to hold it, as
    /// <see cref="Survey"/> lays any roadside. The buildings stand back with its walk (<see cref="TracedBuildings"/>).
    /// </summary>
    static void Roadsides(List<Along> street, List<Vector2> centreM, Carriageways carriageways, SimConfig config)
    {
        var (withM, againstM, streetM) = (0f, 0f, 0f);
        foreach (var step in street)
        {
            if (step.Carriage.RoadsideWithM > 0f) withM += step.LengthM;
            if (step.Carriage.RoadsideAgainstM > 0f) againstM += step.LengthM;
            streetM += step.LengthM;
        }

        if (withM != againstM)
        {
            var withTheStreet = withM > againstM;
            foreach (var step in street)
            {
                var carriage = step.Carriage;
                var (with, against) = (carriage.RoadsideWithM > 0f, carriage.RoadsideAgainstM > 0f);
                if (with != against && with != withTheStreet) step.Lay(carriage with { RoadsideWithM = carriage.RoadsideAgainstM, RoadsideAgainstM = carriage.RoadsideWithM });
            }
        }

        foreach (var withTheStreet in (ReadOnlySpan<bool>)[true, false])
        {
            var (runM, run) = (0f, 0);
            for (var step = 0; step <= street.Count; step++)
            {
                if (step < street.Count && street[step].Carriage.RoadsideM(withTheStreet) > 0f)
                {
                    runM += street[step].LengthM;
                    continue;
                }

                if (runM > 0f && runM < config.CityGen.TracedRoadsideShortestM && runM < streetM)
                {
                    for (var bare = run; bare < step; bare++) street[bare].Lay(street[bare].Carriage.Unedged(withTheStreet));
                }

                (runM, run) = (0f, step + 1);
            }

            var stripM = 0f;
            foreach (var step in street) stripM = MathF.Max(stripM, step.Carriage.RoadsideM(withTheStreet));
            if (stripM <= 0f) continue;

            foreach (var step in street)
            {
                if (step.Carriage.RoadsideM(withTheStreet) > 0f) continue;

                var edged = step.Carriage.Edged(withTheStreet, stripM);
                if (KeepsClear(step.Road, step.Forward ? edged : edged.Turned, centreM, carriageways, config)) step.Lay(edged);
            }
        }
    }

    /// <summary>
    /// <b>Whether a road laid on a wider carriageway stays clear of every other road it stood clear of</b>: no place along
    /// its line, its junctions' discs apart, where its kerb or its walk's outer edge would stand on another road's
    /// carriageway on its level and stood off it before.
    /// </summary>
    static bool KeepsClear(Road road, Carriage wider, List<Vector2> centreM, Carriageways carriageways, SimConfig config)
    {
        var (fromM, toM) = (centreM[road.From], centreM[road.To]);
        var discM = config.JunctionRadiusAcrossM(wider.WidthM);
        var (wasM, willM) = (road.Carriage.WidthM * 0.5f, wider.WidthM * 0.5f);
        var pointsM = road.PointsM;
        for (var leg = 1; leg < pointsM.Count; leg++)
        {
            var runM = pointsM[leg] - pointsM[leg - 1];
            var legM = runM.Length();
            if (legM <= 0f) continue;

            var right = new Vector2(-runM.Y, runM.X) / legM;
            var steps = (int)MathF.Ceiling(legM / WalkStepM);
            for (var step = 0; step <= steps; step++)
            {
                var atM = pointsM[leg - 1] + (runM * ((float)step / steps));
                if (Vector2.Distance(atM, fromM) < discM || Vector2.Distance(atM, toM) < discM) continue;

                var middleM = atM + (right * road.Carriage.CentreOffsetM);
                foreach (var side in (ReadOnlySpan<float>)[1f, -1f])
                {
                    foreach (var walkM in (ReadOnlySpan<float>)[0f, config.PavementWidthM])
                    {
                        var wasAtM = middleM + (right * (side * (wasM + walkM)));
                        var willAtM = middleM + (right * (side * (willM + walkM)));
                        if (carriageways.Holds(willAtM, road) && !carriageways.Holds(wasAtM, road)) return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>
    /// <b>The roads' carriageways filed by the squares their legs cover</b>, to be asked whether a place stands on one —
    /// each leg as wide as its road's carriageway about its middle, and filed a roadside wider, since a road may be laid
    /// one wider after it is filed.
    /// </summary>
    sealed class Carriageways
    {
        /// <summary>How big a square of the map the legs are filed under, to be looked up by the place.</summary>
        const float CellM = 32f;

        readonly Dictionary<(int X, int Y), List<(Road Road, int Leg)>> _cells = [];

        public Carriageways(List<Road> roads, float roadsideM)
        {
            foreach (var road in roads)
            {
                var reachM = (road.Carriage.WidthM * 0.5f) + MathF.Abs(road.Carriage.CentreOffsetM) + roadsideM;
                for (var leg = 1; leg < road.PointsM.Count; leg++)
                {
                    var (least, most) = (Cell(Vector2.Min(road.PointsM[leg - 1], road.PointsM[leg]) - new Vector2(reachM)),
                        Cell(Vector2.Max(road.PointsM[leg - 1], road.PointsM[leg]) + new Vector2(reachM)));
                    for (var x = least.X; x <= most.X; x++)
                    {
                        for (var y = least.Y; y <= most.Y; y++)
                        {
                            if (!_cells.TryGetValue((x, y), out var here)) _cells[(x, y)] = here = [];
                            here.Add((road, leg));
                        }
                    }
                }
            }
        }

        /// <summary>Whether a place stands on the carriageway of a road on the level of the one given, other than it.</summary>
        public bool Holds(Vector2 atM, Road besides)
        {
            if (!_cells.TryGetValue(Cell(atM), out var here)) return false;

            foreach (var (road, leg) in here)
            {
                if (road == besides || road.Carriage.Level != besides.Carriage.Level) continue;

                var (fromM, toM) = (road.PointsM[leg - 1], road.PointsM[leg]);
                var runM = toM - fromM;
                var legSq = runM.LengthSquared();
                if (legSq <= 0f) continue;

                var offM = new Vector2(-runM.Y, runM.X) / MathF.Sqrt(legSq) * road.Carriage.CentreOffsetM;
                var along = Math.Clamp(Vector2.Dot(atM - fromM - offM, runM) / legSq, 0f, 1f);
                if (Vector2.Distance(atM, fromM + offM + (runM * along)) < road.Carriage.WidthM * 0.5f) return true;
            }

            return false;
        }

        static (int X, int Y) Cell(Vector2 atM) => ((int)MathF.Floor(atM.X / CellM), (int)MathF.Floor(atM.Y / CellM));
    }
}
