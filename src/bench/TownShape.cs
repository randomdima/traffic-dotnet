using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Statics;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>What shape a town came out</b>: how much its streets bend, how far apart its junctions stand, what
/// bearings its straights are on, and which of its junctions are places nothing meets (GEN-51).
/// </summary>
/// <remarks>
/// <para>
/// <b>It reads the plan and stands no world up</b>, which is what separates it from <see cref="TownCensus"/>:
/// the census says what a town holds and what the graphs made of it, and this says what the geometry came
/// to. Neither figure here costs more than a walk over the arrays.
/// </para>
/// <para>
/// <b>The distributions are quoted and gate nothing.</b> How much a generated town bends is a fact about
/// one seed, so what a rule can be held to is asked of the suite and what is asked here is what a reader
/// wants to know before changing a figure.
/// </para>
/// </remarks>
internal static class TownShape
{
    /// <summary>Below this curvature an arc is a straight: a radius of 10 km, straighter than any street bends.</summary>
    const float StraightCurvature = 1e-4f;

    /// <summary>One row a map, which is the reading a change to the generator is weighed against.</summary>
    public static void Table(SimConfig config)
    {
        Console.WriteLine($"{"map",-10}{"m",12}{"junc",7}{"roads",7}{"km",8}{"bend",7}{"strt",7}{"sinu",7}" +
                          $"{"bldg",7}{"bays",7}{"props",7}{"spawn",7}");
        foreach (var map in Maps.Shipped())
        {
            var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
            var roads = Figures(plan);
            Console.WriteLine(
                $"{plan.Name,-10}{Extent(plan),12}{plan.Junctions.Count,7}{plan.Roads.Count,7}" +
                $"{roads.TotalM / 1000f,8:F1}{roads.BentShare * 100f,6:F0}%" +
                $"{LaidStraight(plan).StraightShare * 100f,6:F0}%{Percentile(roads.Sinuosity, 50),7:F3}" +
                $"{plan.Buildings.Count,7}{plan.ParkingLots.SpaceCount,7}{plan.Props.Count,7}{plan.Spawns.Count,7}");
        }
    }

    /// <summary>
    /// <b>How much of the roads laid straight came out straight</b> (GEN-47) — every district's share of its
    /// streets, which bend only at the corners they were joined through: how many, how long, and the share of
    /// that length on straight pieces.
    /// </summary>
    static (int Roads, float LengthM, float StraightShare) LaidStraight(CityPlan plan)
    {
        var roads = 0;
        var lengthM = 0f;
        var straightM = 0f;
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            if (!plan.Roads.IsLaidStraight(road)) continue;

            roads++;
            foreach (var piece in plan.Roads.SegmentsOf(road))
            {
                lengthM += piece.LengthM;
                if (MathF.Abs(piece.Curvature) < StraightCurvature) straightM += piece.LengthM;
            }
        }

        return (roads, lengthM, lengthM > 0f ? straightM / lengthM : 0f);
    }

    public static void Run(string map, SimConfig config)
    {
        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        var roads = Figures(plan);

        Console.WriteLine($"{plan.Name}  seed {plan.Seed}  {Extent(plan)} m  pavement {plan.PavementWidthM:F1} m");
        Console.WriteLine();
        Console.WriteLine($"  roads            {plan.Roads.Count}, {roads.TotalM / 1000f:F1} km, " +
                          $"{plan.Roads.Segments.Length} arcs, {OneWay(plan)} of them one way");
        Console.WriteLine($"  junctions        {plan.Junctions.Count}, {ArmsAt(plan, 2)} of two arms, {ArmsAt(plan, 1)} dead ends");
        Console.WriteLine(
            $"  car parks        {plan.CarParks.Count} of {AskedFor(plan, config)} asked, " +
            $"{Bays(plan)} bays, each a road of its own");
        Console.WriteLine();
        Console.WriteLine("  road length      " + Spread(roads.LengthM, " m"));
        Console.WriteLine("  arcs per road    " + Spread(roads.Arcs));
        Console.WriteLine($"  curved length    {roads.BentShare * 100f:F1}% of the network");
        var laid = LaidStraight(plan);
        Console.WriteLine(
            $"  laid straight    {laid.Roads} roads, {laid.LengthM / 1000f:F1} km, " +
            $"{laid.StraightShare * 100f:F1}% of it on straight pieces");
        Console.WriteLine("  bend per road    " + Spread(roads.BendDeg, " deg"));
        Console.WriteLine("  sinuosity        " + Spread(roads.Sinuosity));
        Console.WriteLine("  curve radius     " + Spread(roads.RadiusM, " m"));
        Console.WriteLine("  turn per arc     " + Spread(roads.TurnDeg, " deg"));
        Console.WriteLine("  junction spacing " + Spread(Spacings(plan), " m"));
        Console.WriteLine("  prop radius      " + Spread([.. plan.Props.RadiusM], " m"));
        Console.WriteLine();
        Console.WriteLine("  arms per junction  " + Arms(plan));
        Console.WriteLine("  through deflect  " + Spread(Deflections(plan, out _), " deg"));
        Console.WriteLine("  arms apart       " + Spread(ArmsApart(plan), " deg"));
        Console.WriteLine("  straight bearings  " + Bearings(roads.StraightsAt, roads.StraightsM));
    }

    /// <summary>
    /// <b>Every junction only two roads meet at, where it stands and what kept it</b> (GEN-51). A joint is a
    /// place the town's own arithmetic stopped a line, so what matters is not how many are left but which of
    /// the rule's two structures each one answers to — a bridgehead, a ring node, or a run that comes back
    /// where it set off.
    /// </summary>
    /// <remarks>
    /// <b>Anything else is a refusal and is named as one</b>: the joined road the layout could not lay, cut
    /// back at the place it was refused over. The plan carries no record that a join was offered, so what
    /// this says is that neither structure kept it and never why.
    /// </remarks>
    /// <summary>
    /// <b>Every car park the town laid off a kerb</b> (GEN-53): where the middle of its rank stands, how many bays
    /// stand each side of its street — which is what a reader needs to frame a picture of one (<c>--at</c>,
    /// SHT-1) — which street that is and which way it runs, and how far the bays' mouths run back over it.
    /// </summary>
    /// <remarks>
    /// <b>The kerb column is what the rank's tarmac joining the street's is read off</b> (TER-3c.8): the deepest
    /// and the shallowest any bay's mouth reaches back over the street's own band, across its width. A positive
    /// shallowest is a bay standing off its street, a strip of verge between them.
    /// </remarks>
    public static void Parks(string map, SimConfig config)
    {
        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        var parks = plan.CarParks;

        Console.WriteLine(
            $"{plan.Name}  {parks.Count} car parks of {AskedFor(plan, config)} asked, {Bays(plan)} bays, "
            + $"{OnAOneWayStreet(plan)} on a street that runs one way");

        if (parks.Count == 0) return;

        Console.WriteLine();
        Console.WriteLine($"  {"x",9}{"y",9}{"bays",10}{"street",9}{"runs",11}{"kerb overlap cm",20}");
        for (var park = 0; park < parks.Count; park++)
        {
            var atM = parks.AtM[park];
            var street = parks.Street[park];
            var bays = $"{parks.BaysOn(park, right: true)}+{parks.BaysOn(park, right: false)}";
            var (leastM, mostM) = KerbOverlapM(plan, street, parks.RoadsOf(park));
            Console.WriteLine(
                $"  {atM.X,9:F0}{atM.Y,9:F0}{bays,10}{street,9}{plan.Roads.Flow[street],11}"
                + $"{leastM * 100f,10:F1}{mostM * 100f,10:F1}");
        }
    }

    /// <summary>
    /// <b>How far the bays' mouths run back over their street's band</b> — the least and the most over every
    /// mouth's two corners, measured square off the street's own line.
    /// </summary>
    static (float LeastM, float MostM) KerbOverlapM(CityPlan plan, int street, ReadOnlySpan<int> bays)
    {
        var line = plan.Roads.SegmentsOf(street);
        var lengthM = Spline.TotalLengthM(line);
        var halfM = plan.Roads.WidthM[street] * 0.5f;
        var leastM = float.PositiveInfinity;
        var mostM = float.NegativeInfinity;
        foreach (var bay in bays)
        {
            var mouth = plan.Roads.SegmentsOf(bay)[0];
            var across = Heading.RightOf(mouth.StartUnit) * (plan.Roads.WidthM[bay] * 0.5f);
            foreach (var cornerM in (ReadOnlySpan<Vector2>)[mouth.StartM + across, mouth.StartM - across])
            {
                var on = Spline.SampleAt(line, Spline.ProjectM(line, cornerM, lengthM * 0.5f, lengthM));
                var overlapM = halfM - MathF.Abs(Vector2.Dot(cornerM - on.PositionM, on.Right));
                leastM = MathF.Min(leastM, overlapM);
                mostM = MathF.Max(mostM, overlapM);
            }
        }

        return (leastM, mostM);
    }

    /// <summary>
    /// How many of the town's car parks stand on a street that runs one way (GEN-18, GEN-53), whose bays are each
    /// reached the one way there is rather than two.
    /// </summary>
    static int OnAOneWayStreet(CityPlan plan)
    {
        var found = 0;
        foreach (var street in plan.CarParks.Street)
        {
            if (plan.Roads.Flow[street] != RoadFlow.BothWays) found++;
        }

        return found;
    }

    public static void Joints(string map, SimConfig config)
    {
        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        var arms = plan.Ground.ArmsPerJunction();
        Deflections(plan, out var deflectionAt);

        var onARing = OnARing(plan);
        var onABridge = new bool[plan.Roads.Count];
        foreach (var road in plan.Bridges.Road) onABridge[road] = true;

        var joints = 0;
        var rows = new List<string>();
        var kept = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
            if (arms[junction] != 2) continue;

            joints++;
            var pair = RoadsAt(plan, junction);
            var why = Why(plan, onARing, onABridge, junction, pair[0], pair[1], out var ends);
            kept[why] = kept.GetValueOrDefault(why) + 1;

            var atM = plan.Junctions.CentreM[junction];
            rows.Add($"  {junction,6}{atM.X,9:F0}{atM.Y,9:F0}{deflectionAt[junction],9:F1}°   {why,-13}" +
                     $"{ends.One} ({plan.Junctions.CentreM[ends.One].X:F0},{plan.Junctions.CentreM[ends.One].Y:F0})  " +
                     $"{ends.Other} ({plan.Junctions.CentreM[ends.Other].X:F0},{plan.Junctions.CentreM[ends.Other].Y:F0})");
        }

        Console.WriteLine($"{plan.Name}  {joints} junctions of {plan.Junctions.Count} have two arms  " +
                          $"({plan.Bridges.Count} bridges, {plan.Roundabouts.Count} roundabouts)");

        var reasons = new List<string>(kept.Keys);
        reasons.Sort(StringComparer.Ordinal);
        var said = new List<string>(reasons.Count);
        foreach (var reason in reasons) said.Add($"{reason}: {kept[reason]}");
        Console.WriteLine("  " + (said.Count > 0 ? string.Join("  ", said) : "none"));

        if (rows.Count == 0) return;

        Console.WriteLine();
        Console.WriteLine($"  {"at",6}{"x",9}{"y",9}{"deflect",10}   {"kept by",-13}the run it would be, end to end");
        rows.Sort(StringComparer.Ordinal);
        foreach (var row in rows) Console.WriteLine(row);
    }

    /// <summary>
    /// <b>Every road between two junctions of three arms or more standing nearer than a traced map lays two</b>
    /// (<see cref="CityGenFigures.TracedJunctionsMergedM"/>): how many, how far apart, what kind of road joins them, and
    /// how many such pairs chain into one group.
    /// </summary>
    public static void Close(string map, SimConfig config)
    {
        const int ClosestListed = 40;

        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        var arms = plan.Ground.ArmsPerJunction();
        var onARing = OnARing(plan);
        var withinM = config.CityGen.TracedJunctionsMergedM;

        var group = new int[plan.Junctions.Count];
        for (var junction = 0; junction < group.Length; junction++) group[junction] = junction;

        var apartM = new List<float>();
        var kinds = new Dictionary<string, int>(StringComparer.Ordinal);
        var rows = new List<(float ApartM, string Row)>();
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var (from, to) = (plan.Roads.FromJunction[road], plan.Roads.ToJunction[road]);
            if (from == to || arms[from] < 3 || arms[to] < 3) continue;

            var fromM = plan.Junctions.CentreM[from];
            var toM = plan.Junctions.CentreM[to];
            var apart = Vector2.Distance(fromM, toM);
            if (apart >= withinM) continue;

            var kind = onARing[from] || onARing[to] ? "ring"
                : plan.Roads.LevelOf(road) != CityPlan.RoadArrays.Ground ? "bridge"
                : plan.Roads.Flow[road] == RoadFlow.BothWays ? "two-way" : "one-way";
            kinds[kind] = kinds.GetValueOrDefault(kind) + 1;
            apartM.Add(apart);
            rows.Add((apart, $"  {road,6}{(fromM.X + toM.X) * 0.5f,9:F0}{(fromM.Y + toM.Y) * 0.5f,9:F0}{apart,8:F1}   {kind,-8}" +
                             $"{arms[from]}+{arms[to]} arms"));
            group[Root(from)] = Root(to);
        }

        var members = new Dictionary<int, int>();
        for (var junction = 0; junction < group.Length; junction++) members[Root(junction)] = members.GetValueOrDefault(Root(junction)) + 1;

        var sizes = new SortedDictionary<int, int>();
        foreach (var count in members.Values)
        {
            if (count > 1) sizes[count] = sizes.GetValueOrDefault(count) + 1;
        }

        Console.WriteLine($"{plan.Name}  {apartM.Count} roads join junctions of three arms or more nearer than {withinM:F0} m, " +
                          $"of {plan.Roads.Count} roads");
        Console.WriteLine("  apart            " + Spread(apartM, " m"));
        Console.WriteLine("  kind             " + string.Join("  ", kinds.OrderBy(kind => kind.Key, StringComparer.Ordinal).Select(kind => $"{kind.Key}: {kind.Value}")));
        Console.WriteLine("  junctions a group " + string.Join("  ", sizes.Select(size => $"{size.Key}: {size.Value}")));
        if (rows.Count == 0) return;

        // A sample across the whole spread, nearest first, which is what a reader frames pictures from.
        Console.WriteLine();
        Console.WriteLine($"  {"road",6}{"x",9}{"y",9}{"apart",8}   kind");
        rows.Sort((one, other) => one.ApartM.CompareTo(other.ApartM));
        var every = Math.Max(1, rows.Count / ClosestListed);
        for (var row = 0; row < rows.Count; row += every) Console.WriteLine(rows[row].Row);

        int Root(int junction)
        {
            while (group[junction] != junction) junction = group[junction] = group[group[junction]];
            return junction;
        }
    }

    /// <summary>What kept one two-armed junction, and the two junctions the run through it would join.</summary>
    static string Why(
        CityPlan plan, bool[] onARing, bool[] onABridge, int junction, int one, int other,
        out (int One, int Other) ends)
    {
        var from = Walk(plan, onARing, onABridge, junction, one);
        var to = Walk(plan, onARing, onABridge, junction, other);
        ends = from <= to ? (from, to) : (to, from);

        if (onARing[junction]) return "ring";
        if (onABridge[one] || onABridge[other]) return "bridge";
        if (!Chains(plan, junction, one, other)) return "flow";

        return ends.One == ends.Other ? "loop" : "refused";
    }

    /// <summary>Where the run through a joint ends: on through every joint it carries on into.</summary>
    static int Walk(CityPlan plan, bool[] onARing, bool[] onABridge, int junction, int road)
    {
        var seen = new HashSet<int> { road };
        while (true)
        {
            junction = plan.Roads.FromJunction[road] == junction
                ? plan.Roads.ToJunction[road]
                : plan.Roads.FromJunction[road];

            var at = RoadsAt(plan, junction);
            if (at.Count != 2 || onARing[junction]) return junction;

            var onward = at[0] == road || seen.Contains(at[0]) ? at[1] : at[0];
            if (seen.Contains(onward) || onABridge[road] || onABridge[onward]
                || !Chains(plan, junction, road, onward))
            {
                return junction;
            }

            road = onward;
            seen.Add(road);
        }
    }

    /// <summary>Whether one road's traffic carries on into the other, which is what makes the pair one road.</summary>
    static bool Chains(CityPlan plan, int junction, int one, int other) =>
        Arrives(plan, junction, one) == Leaves(plan, junction, other)
        && Leaves(plan, junction, one) == Arrives(plan, junction, other)
        && (Arrives(plan, junction, one) || Leaves(plan, junction, one));

    static bool Arrives(CityPlan plan, int junction, int road) =>
        plan.Roads.FromJunction[road] == junction
            ? plan.Roads.Flow[road] != RoadFlow.WithTheRoad
            : plan.Roads.Flow[road] != RoadFlow.AgainstTheRoad;

    static bool Leaves(CityPlan plan, int junction, int road) =>
        plan.Roads.FromJunction[road] == junction
            ? plan.Roads.Flow[road] != RoadFlow.AgainstTheRoad
            : plan.Roads.Flow[road] != RoadFlow.WithTheRoad;

    static List<int> RoadsAt(CityPlan plan, int junction)
    {
        var at = new List<int>(4);
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            if (plan.Roads.FromJunction[road] == junction || plan.Roads.ToJunction[road] == junction) at.Add(road);
        }

        return at;
    }

    static bool[] OnARing(CityPlan plan)
    {
        var onARing = new bool[plan.Junctions.Count];
        for (var ring = 0; ring < plan.Roundabouts.Count; ring++)
        {
            foreach (var road in plan.Roundabouts.RoadsOf(ring))
            {
                onARing[plan.Roads.FromJunction[road]] = true;
                onARing[plan.Roads.ToJunction[road]] = true;
            }
        }

        return onARing;
    }

    /// <summary>What every road came out as, read off the arcs it was laid in.</summary>
    readonly record struct RoadFigures(
        float TotalM, float BentShare, List<float> LengthM, List<float> Arcs, List<float> BendDeg,
        List<float> Sinuosity, List<float> RadiusM, List<float> TurnDeg, List<float> StraightsAt,
        List<float> StraightsM);

    static RoadFigures Figures(CityPlan plan)
    {
        List<float> lengthM = [], arcs = [], bendDeg = [], sinuosity = [], radiusM = [], turnDeg = [];
        List<float> straightsAt = [], straightsM = [];
        var totalM = 0f;
        var bentM = 0f;

        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var pieces = plan.Roads.SegmentsOf(road);
            var roadM = Spline.TotalLengthM(pieces);
            if (roadM <= 0f) continue;

            totalM += roadM;
            lengthM.Add(roadM);
            arcs.Add(pieces.Length);

            var turnedRad = 0f;
            foreach (var piece in pieces)
            {
                var turn = MathF.Abs(piece.Curvature) * piece.LengthM;
                turnedRad += turn;
                if (MathF.Abs(piece.Curvature) >= StraightCurvature)
                {
                    bentM += piece.LengthM;
                    radiusM.Add(1f / MathF.Abs(piece.Curvature));
                    turnDeg.Add(float.RadiansToDegrees(turn));
                }
                else
                {
                    // Mod a quarter turn: a grid's two axes are one spike, and which of the four it is
                    // says nothing about how the town was laid.
                    straightsAt.Add(Wrapped(float.RadiansToDegrees(piece.HeadingRad), 90f));
                    straightsM.Add(piece.LengthM);
                }
            }

            bendDeg.Add(float.RadiansToDegrees(turnedRad));

            var chordM = Vector2.Distance(pieces[0].StartM, pieces[^1].EndM);
            if (chordM > 1e-3f) sinuosity.Add(roadM / chordM);
        }

        return new RoadFigures(
            totalM, totalM > 0f ? bentM / totalM : 0f, lengthM, arcs, bendDeg, sinuosity, radiusM, turnDeg,
            straightsAt, straightsM);
    }

    /// <summary>
    /// How far off half a turn the two arms of every two-armed junction stand — the reading that says whether
    /// a junction is a place roads meet or a point one line was stopped at. Near nought is a cut.
    /// </summary>
    static List<float> Deflections(CityPlan plan, out float[] atJunction)
    {
        var outward = Outward(plan);
        atJunction = new float[plan.Junctions.Count];

        var off = new List<float>();
        for (var junction = 0; junction < outward.Length; junction++)
        {
            if (outward[junction].Count != 2) continue;

            var apart = MathF.Abs(MathF.IEEERemainder(outward[junction][0] - outward[junction][1], MathF.Tau));
            atJunction[junction] = 180f - float.RadiansToDegrees(apart);
            off.Add(atJunction[junction]);
        }

        return off;
    }

    /// <summary>
    /// How far apart the nearest two arms of each junction stand — GEN-13's own subject. Near nought is two
    /// carriageways lying along each other, with everything laid on one landing on the other.
    /// </summary>
    static List<float> ArmsApart(CityPlan plan)
    {
        var outward = Outward(plan);
        var tightest = new List<float>();
        foreach (var arms in outward)
        {
            if (arms.Count < 2) continue;

            var apartDeg = float.MaxValue;
            for (var one = 0; one < arms.Count; one++)
            {
                for (var other = one + 1; other < arms.Count; other++)
                {
                    apartDeg = MathF.Min(
                        apartDeg,
                        float.RadiansToDegrees(MathF.Abs(MathF.IEEERemainder(arms[one] - arms[other], MathF.Tau))));
                }
            }

            tightest.Add(apartDeg);
        }

        return tightest;
    }

    /// <summary>Which way each road leaves each of its two junctions, read off the line that was laid.</summary>
    static List<float>[] Outward(CityPlan plan)
    {
        var outward = new List<float>[plan.Junctions.Count];
        for (var junction = 0; junction < outward.Length; junction++) outward[junction] = [];

        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var pieces = plan.Roads.SegmentsOf(road);
            if (pieces.Length == 0) continue;

            var last = pieces[^1];
            outward[plan.Roads.FromJunction[road]].Add(pieces[0].HeadingRad);
            outward[plan.Roads.ToJunction[road]].Add(last.HeadingAtRad(last.LengthM) + MathF.PI);
        }

        return outward;
    }

    static string Arms(CityPlan plan)
    {
        var arms = plan.Ground.ArmsPerJunction();
        var tally = new int[8];
        foreach (var at in arms) tally[Math.Min(at, tally.Length - 1)]++;

        var said = new List<string>();
        for (var at = 0; at < tally.Length; at++)
        {
            if (tally[at] > 0) said.Add($"{at} arms: {tally[at]}");
        }

        return string.Join("  ", said);
    }

    /// <summary>Each junction's distance to its nearest neighbour, which is the block size near enough.</summary>
    static List<float> Spacings(CityPlan plan)
    {
        var near = new List<float>(plan.Junctions.Count);
        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
            var bestM = float.MaxValue;
            for (var other = 0; other < plan.Junctions.Count; other++)
            {
                if (other == junction) continue;

                bestM = MathF.Min(
                    bestM,
                    Vector2.DistanceSquared(plan.Junctions.CentreM[junction], plan.Junctions.CentreM[other]));
            }

            if (bestM < float.MaxValue) near.Add(MathF.Sqrt(bestM));
        }

        return near;
    }

    /// <summary>Straight-piece bearings in five-degree bins, weighted by length: a grid shows one or two spikes.</summary>
    static string Bearings(List<float> at, List<float> lengthM)
    {
        if (at.Count == 0) return "none";

        const int binDeg = 5;
        var bins = new float[90 / binDeg];
        var totalM = 0f;
        for (var straight = 0; straight < at.Count; straight++)
        {
            bins[Math.Clamp((int)(at[straight] / binDeg), 0, bins.Length - 1)] += lengthM[straight];
            totalM += lengthM[straight];
        }

        var order = new int[bins.Length];
        for (var bin = 0; bin < order.Length; bin++) order[bin] = bin;
        Array.Sort(order, (one, other) => bins[other].CompareTo(bins[one]));

        var said = new List<string>();
        for (var row = 0; row < MostBearingRows && row < order.Length; row++)
        {
            if (bins[order[row]] <= 0f) break;

            said.Add($"{order[row] * binDeg}-{(order[row] + 1) * binDeg}: {bins[order[row]] * 100f / totalM:F0}%");
        }

        return string.Join("  ", said);
    }

    /// <summary>How many bins of the bearing histogram are printed: enough to show a grid, short of a list.</summary>
    const int MostBearingRows = 6;

    static int OneWay(CityPlan plan)
    {
        var found = 0;
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            if (plan.Roads.Flow[road] != RoadFlow.BothWays) found++;
        }

        return found;
    }

    /// <summary>How many bays the town's car parks came to, one road each (GEN-53).</summary>
    static int Bays(CityPlan plan) => plan.CarParks.Road.Length;

    /// <summary>
    /// <b>How many car parks the map asked for</b>, which is what its planned buildings come to
    /// (<see cref="SimConfig.CarParksFor"/>, GEN-6) and a yard for each of its districts' services (GEN-56) — so
    /// what is printed beside it is what the ground could carry of that (GEN-8). <b>A map that plans no buildings asks
    /// for none.</b>
    /// </summary>
    static int AskedFor(CityPlan plan, SimConfig config)
    {
        var buildings = plan.Zones.Planned;
        if (buildings == 0) return 0;

        return config.CarParksFor(buildings)
               + (ServiceBuildings.Uses.Length * ServiceBuildings.OfEachUse(buildings, plan.Districts));
    }

    static int ArmsAt(CityPlan plan, int count)
    {
        var found = 0;
        foreach (var at in plan.Ground.ArmsPerJunction())
        {
            if (at == count) found++;
        }

        return found;
    }

    static string Extent(CityPlan plan) => $"{plan.WorldSizeM.X:F0}x{plan.WorldSizeM.Y:F0}";

    static float Wrapped(float value, float by) => value - (by * MathF.Floor(value / by));

    internal static string Spread(List<float> values, string unit = "")
    {
        if (values.Count == 0) return "none";

        return $"min {Percentile(values, 0):F1}{unit}  p10 {Percentile(values, 10):F1}{unit}  " +
               $"p50 {Percentile(values, 50):F1}{unit}  p90 {Percentile(values, 90):F1}{unit}  " +
               $"max {Percentile(values, 100):F1}{unit}";
    }

    /// <summary>
    /// One percentile of a set of readings, interpolated between the two either side of it. <b>It sorts the
    /// list it was given</b>: every caller here is done with the order it had.
    /// </summary>
    static float Percentile(List<float> values, float which)
    {
        if (values.Count == 0) return 0f;

        values.Sort();
        var at = (values.Count - 1) * which / 100f;
        var low = (int)MathF.Floor(at);
        var high = (int)MathF.Ceiling(at);
        return values[low] + ((values[high] - values[low]) * (at - low));
    }
}
