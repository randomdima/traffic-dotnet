using System.Numerics;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.CityGen.Traced;

/// <summary>
/// <b>A traced map's car parks, laid as a mall's are</b> (GEN-53a): inside every car park its zones outline, aisles side by
/// side a rank of bays and a walk apart, joined at both ends by an aisle across them, the ladder joined to the streets
/// round it — and a rank of bays down both kerbs of every aisle.
/// </summary>
/// <remarks>
/// <para>
/// <b>The aisles are the map's and the bays are the town's.</b> The aisles are laid into the map once, as roads of its
/// own (<see cref="Parked"/>, <c>qq osm --parking</c>), so a town laid off it reads them as it reads any street: a
/// junction wherever two of them share a point, and where an entrance shares one with the street it joins. The bays are
/// laid along them when the town is (<see cref="Bays"/>), as every town's are (GEN-53).
/// </para>
/// <para>
/// <b>What OSM drew inside a car park is not laid</b> (<c>qq osm --drop-zone-ways</c>): a mapper's aisles are a sketch of
/// where cars go, and the car park is laid again as the engine lays one — the outline is the fact, the inside is ours.
/// </para>
/// </remarks>
internal static partial class TracedCarParks
{
    /// <summary>What laying a map's car parks came to.</summary>
    /// <param name="Lots">The car parks its zones outline.</param>
    /// <param name="Laid">Those laid: room for a ladder of aisles, and a street to join it to.</param>
    /// <param name="NoRoom">Those with no room for one aisle ranked both sides with the fewest bays a car park holds.</param>
    /// <param name="NoStreet">Those with room and no street within reach that an entrance may join.</param>
    /// <param name="Placed">Where each car park laid stands, and how many bays its ladder was planned to rank.</param>
    internal readonly record struct Parking(
        TownMap Map, int Lots, int Laid, int NoRoom, int NoStreet, int Aisles, int Entrances, List<(Vector2 AtM, int Bays)> Placed);

    /// <summary>
    /// <b>The map with its car parks' aisles laid into it</b>: any it held before taken out, and every <see cref="ZoneKind.Parking"/>
    /// zone's laid again, the largest first, each a ladder (<see cref="Planned"/>) joined to the streets by its nearest
    /// entrances (<see cref="Entrances"/>). A street an entrance joins is given the point it is joined at.
    /// </summary>
    public static Parking Parked(TownMap map, SimConfig config)
    {
        var roads = new List<TracedRoad>();
        var points = new List<List<int>>();
        foreach (var road in map.Roads)
        {
            if (road.Aisle) continue;

            roads.Add(road);
            points.Add([.. road.Points]);
        }

        var pointM = new List<Vector2D>(map.PointM);
        var obstacles = new Obstacles();
        for (var road = 0; road < roads.Count; road++)
        {
            obstacles.Add(roads[road], points[road], pointM, Joinable(roads[road]), config);
        }

        var junctions = new Junctions(points, pointM);
        var zones = map.Zones;
        var lots = Enumerable.Range(0, zones.Count).Where(zone => zones.Kind[zone] == ZoneKind.Parking)
            .OrderByDescending(zone => MathF.Abs(SignedAreaM2(zones.OutlineOf(zone)))).ToList();

        var aisles = new List<(TracedRoad Road, List<int> Points)>();
        var (laid, noRoom, noStreet, entrances) = (0, 0, 0, 0);
        var placed = new List<(Vector2 AtM, int Bays)>();
        foreach (var lot in lots)
        {
            var outline = zones.OutlineOf(lot).ToArray();
            if (Planned(outline, obstacles, config) is not { } ladder)
            {
                noRoom++;
                continue;
            }

            var joins = Entrances(ladder, obstacles, junctions, config);
            if (joins.Count == 0)
            {
                noStreet++;
                continue;
            }

            var before = aisles.Count;
            Lay(ladder, joins, roads, points, pointM, aisles, config);
            for (var aisle = before; aisle < aisles.Count; aisle++)
            {
                obstacles.Add(aisles[aisle].Road, aisles[aisle].Points, pointM, false, config);
                junctions.Add(aisles[aisle].Points, pointM);
            }

            laid++;
            placed.Add((ladder.Frame.Out((ladder.FromU + ladder.ToU) * 0.5f, (ladder.RungV[0] + ladder.RungV[^1]) * 0.5f), ladder.Bays));
            entrances += joins.Count;
        }

        var kept = new TracedRoad[roads.Count + aisles.Count];
        for (var road = 0; road < roads.Count; road++) kept[road] = roads[road] with { Points = [.. points[road]] };
        for (var aisle = 0; aisle < aisles.Count; aisle++) kept[roads.Count + aisle] = aisles[aisle].Road with { Points = [.. aisles[aisle].Points] };

        return new Parking(map with { PointM = [.. pointM], Roads = kept }, lots.Count, laid, noRoom, noStreet, aisles.Count, entrances, placed);
    }

    /// <summary>
    /// <b>Whether an entrance may join a road</b>: a street on the ground that is not a motorway's or a trunk road's, nor
    /// a roundabout's ring, nor another car park's aisle.
    /// </summary>
    static bool Joinable(TracedRoad road) =>
        !road.Bridge && !road.Roundabout && !road.Aisle && road.Highway is not ("motorway" or "motorway_link" or "trunk" or "trunk_link");

    /// <summary>
    /// <b>A car park's aisles in its own frame</b>: <see cref="Frame.U"/> along the aisles, each at one of
    /// <see cref="RungV"/> across them, running from <see cref="FromU"/> to <see cref="ToU"/> — where an aisle across
    /// them joins their ends, if there are two or more.
    /// </summary>
    sealed record Ladder(Frame Frame, float[] RungV, float FromU, float ToU, int Bays);

    /// <summary>A frame turned to a car park's bearing: <see cref="U"/> along its aisles and <see cref="V"/> across them.</summary>
    readonly record struct Frame(Vector2 OriginM, Vector2 U, Vector2 V)
    {
        public Vector2 In(Vector2 atM) => new(Vector2.Dot(atM - OriginM, U), Vector2.Dot(atM - OriginM, V));

        public Vector2 Out(float u, float v) => OriginM + (U * u) + (V * v);

        public Vector2 OutAlong(Vector2 local) => (U * local.X) + (V * local.Y);
    }

    /// <summary>How finely the ground across a car park's aisles is read for room, in metres a row.</summary>
    const float RowM = 1f;

    /// <summary>How many offsets across its bearing a car park's aisles are tried at, a pitch shared between them.</summary>
    const int Phases = 4;

    /// <summary>The fewest metres an outline's side runs to be a bearing a car park's aisles are tried along.</summary>
    const float BearingSideLeastM = 8f;

    /// <summary>How close two bearings stand, a quarter turn apart or not, to be tried once: a degree's worth.</summary>
    const float SameBearingRad = MathF.PI / 180f;

    /// <summary>
    /// <b>The ladder that ranks the most bays inside an outline</b>, or none where not one aisle ranked both sides with
    /// the fewest bays a car park holds fits (<see cref="CityGenFigures.BaysPerLotFewest"/>). Tried along every bearing
    /// one of its sides runs, at <see cref="Phases"/> offsets across it: each run of consecutive aisles the room it shares
    /// along the bearing — inside the outline across the whole of their ranks and the walks between, and clear of every
    /// road — and the run whose ranks hold the most bays is the ladder.
    /// </summary>
    static Ladder? Planned(Vector2[] outline, Obstacles obstacles, SimConfig config)
    {
        var aisleM = config.CarParkAisleWidthM;
        var halfBandM = (aisleM * 0.5f) + config.CarParkBayDepthM;
        var pitchM = config.CarParkAislePitchM;
        var near = obstacles.Near(Box.Of(outline).Grown(obstacles.WidestHalfM));

        Ladder? best = null;
        foreach (var bearingRad in Bearings(outline))
        {
            var frame = new Frame(outline[0], new Vector2(MathF.Cos(bearingRad), MathF.Sin(bearingRad)), new Vector2(-MathF.Sin(bearingRad), MathF.Cos(bearingRad)));
            var local = Array.ConvertAll(outline, frame.In);
            var (lowV, highV) = (local.Min(at => at.Y), local.Max(at => at.Y));
            var crossing = near.Select(segment => (A: frame.In(segment.AM), B: frame.In(segment.BM), HalfM: segment.HalfM + config.WalkOuterM)).ToList();
            var rows = new List<(float From, float To)>[(int)MathF.Ceiling((highV - lowV) / RowM)];
            for (var row = 0; row < rows.Length; row++) rows[row] = Free(local, crossing, lowV + ((row + 0.5f) * RowM));

            for (var phase = 0; phase < Phases; phase++)
            {
                var rungs = new List<float>();
                for (var v = lowV + halfBandM + (phase * pitchM / Phases); v + halfBandM <= highV; v += pitchM) rungs.Add(v);

                for (var first = 0; first < rungs.Count; first++)
                {
                    List<(float From, float To)>? shared = null;
                    var readTo = Row(rungs[first] - halfBandM, lowV);
                    for (var last = first; last < rungs.Count; last++)
                    {
                        var upTo = Math.Min(rows.Length - 1, Row(rungs[last] + halfBandM, lowV));
                        for (; readTo <= upTo; readTo++) shared = shared is null ? rows[readTo] : Shared(shared, rows[readTo]);

                        if (shared is null || Longest(shared) is not { } room) break;

                        var (fromU, toU) = room;
                        var count = last - first + 1;
                        var (aisleFromU, aisleToU) = count > 1 ? (fromU + (aisleM * 0.5f), toU - (aisleM * 0.5f)) : (fromU, toU);
                        var bays = count * 2 * RankBays(aisleToU - aisleFromU, config);
                        if (bays <= (best?.Bays ?? 0)) continue;

                        best = new Ladder(frame, [.. rungs.GetRange(first, count)], aisleFromU, aisleToU, bays);
                    }
                }
            }
        }

        return best;
    }

    /// <summary>
    /// <b>How many bays one side of an aisle this long ranks</b>: none short of the fewest a car park holds. Its rank
    /// stands clear of the junction at either end by a disc and the zebra struck past it
    /// (<see cref="SimConfig.CarParkRankClearM"/>).
    /// </summary>
    static int RankBays(float aisleM, SimConfig config)
    {
        var bays = (int)((aisleM - (2f * (config.JunctionRadiusM + config.CarParkRankClearM))) / config.LaneWidthM);
        return bays >= config.CityGen.BaysPerLotFewest ? bays : 0;
    }

    static int Row(float v, float lowV) => Math.Max(0, (int)MathF.Floor((v - lowV) / RowM));

    /// <summary>Every bearing an outline's sides run that is long enough to square aisles to, once each a quarter turn round.</summary>
    static List<float> Bearings(Vector2[] outline)
    {
        var sides = new List<(float Rad, float LengthM)>();
        for (var at = 0; at < outline.Length; at++)
        {
            var run = outline[(at + 1) % outline.Length] - outline[at];
            if (run.Length() >= BearingSideLeastM) sides.Add((MathF.Atan2(run.Y, run.X), run.Length()));
        }

        var bearings = new List<float>();
        foreach (var (rad, _) in sides.OrderByDescending(side => side.LengthM))
        {
            foreach (var turned in (ReadOnlySpan<float>)[rad, rad + (MathF.PI * 0.5f)])
            {
                if (!bearings.Exists(other => LinesApartRad(other, turned) <= SameBearingRad)) bearings.Add(turned);
            }
        }

        return bearings;
    }

    /// <summary>How far apart two lines' bearings stand, a half turn being no turn at all for a line.</summary>
    static float LinesApartRad(float one, float other) => MathF.Abs(MathF.IEEERemainder(one - other, MathF.PI));

    /// <summary>
    /// <b>What of one row across a car park is room</b>: inside its outline, and further from every road than that road's
    /// half and a walk (<see cref="SimConfig.WalkOuterM"/>) — the outline's chords at the row less every road's reach
    /// across it — so the walk wrapping the car park parts it from the street.
    /// </summary>
    static List<(float From, float To)> Free(Vector2[] local, List<(Vector2 A, Vector2 B, float HalfM)> crossing, float v)
    {
        var xs = new List<float>();
        for (var at = 0; at < local.Length; at++)
        {
            var (p, q) = (local[at], local[(at + 1) % local.Length]);
            if ((p.Y <= v) != (q.Y <= v)) xs.Add(p.X + ((v - p.Y) * (q.X - p.X) / (q.Y - p.Y)));
        }

        xs.Sort();
        var free = new List<(float From, float To)>();
        for (var at = 0; at + 1 < xs.Count; at += 2) free.Add((xs[at], xs[at + 1]));

        foreach (var (a, b, halfM) in crossing)
        {
            if (Reach(a, b, halfM, v) is { } taken) free = Less(free, taken);
        }

        return free;
    }

    /// <summary>How far along a row a road's carriageway reaches across it, or none where it does not reach the row.</summary>
    static (float From, float To)? Reach(Vector2 a, Vector2 b, float halfM, float v)
    {
        var (lowV, highV) = (v - halfM, v + halfM);
        if (MathF.Max(a.Y, b.Y) < lowV || MathF.Min(a.Y, b.Y) > highV) return null;

        var (from, to) = (0f, 1f);
        var runV = b.Y - a.Y;
        if (MathF.Abs(runV) > float.Epsilon)
        {
            var (atLow, atHigh) = ((lowV - a.Y) / runV, (highV - a.Y) / runV);
            from = MathF.Max(from, MathF.Min(atLow, atHigh));
            to = MathF.Min(to, MathF.Max(atLow, atHigh));
            if (from > to) return null;
        }

        var (x0, x1) = (a.X + ((b.X - a.X) * from), a.X + ((b.X - a.X) * to));
        return (MathF.Min(x0, x1) - halfM, MathF.Max(x0, x1) + halfM);
    }

    static List<(float From, float To)> Less(List<(float From, float To)> free, (float From, float To) taken)
    {
        var left = new List<(float From, float To)>(free.Count + 1);
        foreach (var (from, to) in free)
        {
            if (taken.To <= from || taken.From >= to)
            {
                left.Add((from, to));
                continue;
            }

            if (taken.From > from) left.Add((from, taken.From));
            if (taken.To < to) left.Add((taken.To, to));
        }

        return left;
    }

    static List<(float From, float To)> Shared(List<(float From, float To)> one, List<(float From, float To)> other)
    {
        var shared = new List<(float From, float To)>();
        var (i, j) = (0, 0);
        while (i < one.Count && j < other.Count)
        {
            var (from, to) = (MathF.Max(one[i].From, other[j].From), MathF.Min(one[i].To, other[j].To));
            if (from < to) shared.Add((from, to));
            if (one[i].To < other[j].To) i++;
            else j++;
        }

        return shared;
    }

    static (float From, float To)? Longest(List<(float From, float To)> free)
    {
        (float From, float To)? longest = null;
        foreach (var span in free)
        {
            if (longest is not { } held || span.To - span.From > held.To - held.From) longest = span;
        }

        return longest;
    }

    /// <summary>One way into a car park: where on its aisles it leaves, and where on which street it joins.</summary>
    /// <param name="Rung">The aisle it leaves, as an index into <see cref="Ladder.RungV"/>.</param>
    /// <param name="AtU">Where along that aisle it leaves.</param>
    /// <param name="Road">The street it joins.</param>
    readonly record struct Entrance(int Rung, float AtU, int Road, Vector2 FromM, Vector2 JoinsM);

    /// <summary>
    /// <b>The ways into a car park</b>, the shortest first and one for every <see cref="CityGenFigures.CarParkBaysPerEntrance"/>
    /// bays its ladder ranks, as far as there are places to join, no two within a junction's clearance of each other.
    /// Each runs straight out from its outermost aisles to the first road it meets — out along an aisle past its
    /// end, or square off the first or the last one — where that road may be joined (<see cref="Joinable"/>), it is met
    /// squarer than <see cref="RoadFigures.TurnStraightToleranceDeg"/> off straight on, the place it is met stands clear
    /// of every junction of the street (<see cref="SimConfig.CarParkEntranceClearM"/>), and nothing else stands within
    /// the entrance's own half and that thing's of the way there.
    /// </summary>
    static List<Entrance> Entrances(Ladder ladder, Obstacles obstacles, Junctions junctions, SimConfig config)
    {
        var frame = ladder.Frame;
        var (fromU, toU) = (ladder.FromU, ladder.ToU);
        var clearM = config.CarParkEntranceClearM;
        var (first, last) = (0, ladder.RungV.Length - 1);

        // Out along each aisle at either end is past its end, which on a ladder is off a corner it shares with the aisle
        // across; square off the outermost aisles is from any place along them far enough from those corners.
        var tried = new List<(int Rung, float AtU, Vector2 Out)>
        {
            (first, fromU, -Vector2.UnitX), (first, toU, Vector2.UnitX), (last, fromU, -Vector2.UnitX), (last, toU, Vector2.UnitX),
        };

        if (first != last)
        {
            tried.Add((first, fromU, -Vector2.UnitY));
            tried.Add((first, toU, -Vector2.UnitY));
            tried.Add((last, fromU, Vector2.UnitY));
            tried.Add((last, toU, Vector2.UnitY));
        }

        for (var u = fromU + clearM; u <= toU - clearM; u += config.LaneWidthM)
        {
            tried.Add((first, u, -Vector2.UnitY));
            tried.Add((last, u, Vector2.UnitY));
        }

        var minSquareSin = MathF.Cos(float.DegreesToRadians(config.Road.TurnStraightToleranceDeg));
        var halfAisleM = config.CarParkAisleWidthM * 0.5f;
        var reachM = config.CityGen.CarParkEntranceReachM;
        var found = new List<(Entrance Entrance, float LengthM)>();
        foreach (var (rung, atU, outward) in tried)
        {
            var fromM = frame.Out(atU, ladder.RungV[rung]);
            var directionM = frame.OutAlong(outward);
            if (obstacles.FirstHit(fromM, directionM, reachM) is not { } hit || !hit.Segment.Joinable) continue;

            var along = Vector2.Normalize(hit.Segment.BM - hit.Segment.AM);
            if (MathF.Abs((directionM.X * along.Y) - (directionM.Y * along.X)) < minSquareSin) continue;
            if (junctions.Within(hit.AtM, clearM)) continue;
            if (!obstacles.ClearAlong(fromM, directionM, hit.LengthM - hit.Segment.HalfM - halfAisleM, halfAisleM)) continue;

            found.Add((new Entrance(rung, atU, hit.Segment.Road, fromM, hit.AtM), hit.LengthM));
        }

        var wanted = (ladder.Bays + config.CityGen.CarParkBaysPerEntrance - 1) / config.CityGen.CarParkBaysPerEntrance;
        var chosen = new List<Entrance>();
        foreach (var (entrance, _) in found.OrderBy(candidate => candidate.LengthM))
        {
            if (chosen.Count >= wanted) break;
            if (chosen.Exists(other => Vector2.Distance(other.JoinsM, entrance.JoinsM) < clearM || Vector2.Distance(other.FromM, entrance.FromM) < clearM)) continue;

            chosen.Add(entrance);
        }

        return chosen;
    }

    /// <summary>
    /// <b>A ladder and its entrances laid as roads of the map</b>: each aisle a road from end to end through every place an
    /// entrance leaves it, the aisle across each end through every aisle's end where there are two or more, and each
    /// entrance a road from its aisle to the street — the street given the point it is joined at where it has none there.
    /// </summary>
    static void Lay(
        Ladder ladder, List<Entrance> entrances, List<TracedRoad> roads, List<List<int>> points, List<Vector2D> pointM,
        List<(TracedRoad Road, List<int> Points)> aisles, SimConfig config)
    {
        var aisle = new TracedRoad
        {
            OsmId = 0, Highway = AisleHighway, LanesForward = 1, LanesBackward = 1, LanesShared = 0, LanesTagged = true,
            WidthM = config.CarParkAisleWidthM, Mended = true, Aisle = true, Points = [],
        };

        int Placed(Vector2 atM)
        {
            pointM.Add(new Vector2D(atM.X, atM.Y));
            return pointM.Count - 1;
        }

        var frame = ladder.Frame;
        var count = ladder.RungV.Length;
        var fromEnd = new int[count];
        var toEnd = new int[count];
        for (var rung = 0; rung < count; rung++)
        {
            fromEnd[rung] = Placed(frame.Out(ladder.FromU, ladder.RungV[rung]));
            toEnd[rung] = Placed(frame.Out(ladder.ToU, ladder.RungV[rung]));
        }

        var leaves = new int[entrances.Count];
        for (var entrance = 0; entrance < entrances.Count; entrance++)
        {
            var (rung, atU) = (entrances[entrance].Rung, entrances[entrance].AtU);
            leaves[entrance] = atU <= ladder.FromU ? fromEnd[rung] : atU >= ladder.ToU ? toEnd[rung] : Placed(entrances[entrance].FromM);
        }

        for (var rung = 0; rung < count; rung++)
        {
            var along = new List<(float U, int Point)> { (ladder.FromU, fromEnd[rung]), (ladder.ToU, toEnd[rung]) };
            for (var entrance = 0; entrance < entrances.Count; entrance++)
            {
                var (onRung, atU) = (entrances[entrance].Rung, entrances[entrance].AtU);
                if (onRung == rung && atU > ladder.FromU && atU < ladder.ToU) along.Add((atU, leaves[entrance]));
            }

            aisles.Add((aisle, [.. along.OrderBy(at => at.U).Select(at => at.Point)]));
        }

        if (count > 1)
        {
            aisles.Add((aisle, [.. fromEnd]));
            aisles.Add((aisle, [.. toEnd]));
        }

        for (var entrance = 0; entrance < entrances.Count; entrance++)
        {
            aisles.Add((aisle, [leaves[entrance], Joined(points[entrances[entrance].Road], pointM, entrances[entrance].JoinsM)]));
        }
    }

    /// <summary>OSM's own class for a car park's aisle: <c>highway=service</c>, which <c>service=parking_aisle</c> says more of.</summary>
    const string AisleHighway = "service";

    /// <summary>How near a point a street already has an entrance joins it at that point rather than at one of its own.</summary>
    const float JoinedAtItsPointM = 1f;

    /// <summary>
    /// <b>The point a street is joined at</b>: one of its own within <see cref="JoinedAtItsPointM"/>, else a point put into
    /// it on the run between the two either side.
    /// </summary>
    static int Joined(List<int> street, List<Vector2D> pointM, Vector2 atM)
    {
        var (nearest, nearestM) = (0, float.MaxValue);
        for (var at = 0; at + 1 < street.Count; at++)
        {
            var offM = Vector2.Distance(atM, Nearest(Of(pointM[street[at]]), Of(pointM[street[at + 1]]), atM));
            if (offM < nearestM) (nearest, nearestM) = (at, offM);
        }

        foreach (var end in (ReadOnlySpan<int>)[street[nearest], street[nearest + 1]])
        {
            if (Vector2.Distance(Of(pointM[end]), atM) <= JoinedAtItsPointM) return end;
        }

        pointM.Add(new Vector2D(atM.X, atM.Y));
        street.Insert(nearest + 1, pointM.Count - 1);
        return pointM.Count - 1;
    }

    static Vector2 Of(Vector2D atM) => new((float)atM.X, (float)atM.Y);

    static Vector2 Nearest(Vector2 a, Vector2 b, Vector2 atM)
    {
        var run = b - a;
        var lengthSq = run.LengthSquared();
        return lengthSq <= 0f ? a : a + (run * Math.Clamp(Vector2.Dot(atM - a, run) / lengthSq, 0f, 1f));
    }

    static float SignedAreaM2(ReadOnlySpan<Vector2> ring)
    {
        var twice = 0f;
        for (var at = 0; at < ring.Length; at++)
        {
            var (p, q) = (ring[at], ring[(at + 1) % ring.Length]);
            twice += (p.X * q.Y) - (q.X * p.Y);
        }

        return twice * 0.5f;
    }

    /// <summary>A box in metres, for filing.</summary>
    readonly record struct Box(Vector2 MinM, Vector2 MaxM)
    {
        public static Box Of(ReadOnlySpan<Vector2> points)
        {
            var (min, max) = (new Vector2(float.MaxValue), new Vector2(float.MinValue));
            foreach (var at in points) (min, max) = (Vector2.Min(min, at), Vector2.Max(max, at));
            return new Box(min, max);
        }

        public Box Grown(float byM) => new(MinM - new Vector2(byM), MaxM + new Vector2(byM));
    }

    /// <summary>One straight of a road's line, its carriageway's half either side, and whether an entrance may join it.</summary>
    readonly record struct Segment(Vector2 AM, Vector2 BM, float HalfM, int Road, bool Joinable);

    /// <summary>Where a line out of a car park first meets a road's line: the straight, how far out, and the place.</summary>
    readonly record struct Hit(Segment Segment, float LengthM, Vector2 AtM);

    /// <summary>Every road's line in straights, filed by where they stand, for what a car park's ground and entrances meet.</summary>
    sealed class Obstacles
    {
        const float CellM = 32f;

        readonly List<Segment> _segments = [];
        readonly Dictionary<(int, int), List<int>> _cells = [];
        int _roads;

        /// <summary>The widest half any road filed has: how far past a box a road filed outside it can still reach in.</summary>
        public float WidestHalfM { get; private set; }

        public void Add(TracedRoad road, List<int> points, List<Vector2D> pointM, bool joinable, SimConfig config)
        {
            var halfM = (road.WidthM ?? (road.Lanes * config.CityGen.TracedLaneWidthM)) * 0.5f;
            WidestHalfM = MathF.Max(WidestHalfM, halfM);
            for (var at = 0; at + 1 < points.Count; at++)
            {
                var segment = new Segment(Of(pointM[points[at]]), Of(pointM[points[at + 1]]), halfM, _roads, joinable);
                var box = Box.Of([segment.AM, segment.BM]).Grown(halfM);
                for (var x = Cell(box.MinM.X); x <= Cell(box.MaxM.X); x++)
                {
                    for (var y = Cell(box.MinM.Y); y <= Cell(box.MaxM.Y); y++)
                    {
                        if (!_cells.TryGetValue((x, y), out var here)) _cells[(x, y)] = here = [];
                        here.Add(_segments.Count);
                    }
                }

                _segments.Add(segment);
            }

            _roads++;
        }

        public List<Segment> Near(Box box)
        {
            var seen = new HashSet<int>();
            for (var x = Cell(box.MinM.X); x <= Cell(box.MaxM.X); x++)
            {
                for (var y = Cell(box.MinM.Y); y <= Cell(box.MaxM.Y); y++)
                {
                    if (_cells.TryGetValue((x, y), out var here)) seen.UnionWith(here);
                }
            }

            return [.. seen.Order().Select(segment => _segments[segment])];
        }

        /// <summary>Where a line out of a place first crosses a road's line within reach, or none.</summary>
        public Hit? FirstHit(Vector2 fromM, Vector2 directionM, float reachM)
        {
            Hit? first = null;
            foreach (var segment in Near(Box.Of([fromM, fromM + (directionM * reachM)])))
            {
                var run = segment.BM - segment.AM;
                var denominator = Cross(directionM, run);
                if (MathF.Abs(denominator) <= float.Epsilon) continue;

                var offset = segment.AM - fromM;
                var (lengthM, share) = (Cross(offset, run) / denominator, Cross(offset, directionM) / denominator);
                if (lengthM <= 0f || lengthM > reachM || share < 0f || share > 1f || lengthM >= (first?.LengthM ?? float.MaxValue)) continue;

                first = new Hit(segment, lengthM, fromM + (directionM * lengthM));
            }

            return first;
        }

        /// <summary>Whether a line this far out of a place stands further from every road than that road's half and its own.</summary>
        public bool ClearAlong(Vector2 fromM, Vector2 directionM, float lengthM, float halfM)
        {
            if (lengthM <= 0f) return true;

            var toM = fromM + (directionM * lengthM);
            foreach (var segment in Near(Box.Of([fromM, toM]).Grown(halfM + WidestHalfM)))
            {
                for (var atM = 0f; atM <= lengthM; atM += RowM)
                {
                    var hereM = fromM + (directionM * atM);
                    if (Vector2.Distance(hereM, Nearest(segment.AM, segment.BM, hereM)) < segment.HalfM + halfM) return false;
                }
            }

            return true;
        }

        static float Cross(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);

        static int Cell(float atM) => (int)MathF.Floor(atM / CellM);
    }

    /// <summary>Every place two roads meet or a road ends, filed by where it stands, for how far an entrance stands from one.</summary>
    sealed class Junctions
    {
        const float CellM = 32f;

        readonly Dictionary<(int, int), List<Vector2>> _cells = [];

        public Junctions(List<List<int>> points, List<Vector2D> pointM)
        {
            var uses = new Dictionary<int, int>();
            foreach (var line in points)
            {
                foreach (var point in line.Distinct()) uses[point] = uses.GetValueOrDefault(point) + 1;
            }

            foreach (var line in points)
            {
                foreach (var end in (ReadOnlySpan<int>)[line[0], line[^1]]) File(Of(pointM[end]));
            }

            foreach (var (point, count) in uses)
            {
                if (count > 1) File(Of(pointM[point]));
            }
        }

        /// <summary>A road's ends and every point of it filed, which for a car park's aisles is each place it meets another.</summary>
        public void Add(List<int> points, List<Vector2D> pointM)
        {
            foreach (var point in points) File(Of(pointM[point]));
        }

        public bool Within(Vector2 atM, float reachM)
        {
            for (var x = Cell(atM.X - reachM); x <= Cell(atM.X + reachM); x++)
            {
                for (var y = Cell(atM.Y - reachM); y <= Cell(atM.Y + reachM); y++)
                {
                    if (!_cells.TryGetValue((x, y), out var here)) continue;

                    foreach (var junctionM in here)
                    {
                        if (Vector2.Distance(junctionM, atM) < reachM) return true;
                    }
                }
            }

            return false;
        }

        void File(Vector2 atM)
        {
            if (!_cells.TryGetValue((Cell(atM.X), Cell(atM.Y)), out var here)) _cells[(Cell(atM.X), Cell(atM.Y))] = here = [];
            here.Add(atM);
        }

        static int Cell(float atM) => (int)MathF.Floor(atM / CellM);
    }
}
