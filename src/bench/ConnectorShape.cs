using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>What shape a town's movements came out</b> (TER-5d.2): every connector read for the ways a line across a junction
/// stops being a corner — winding more than its two ends ask, heading back on itself, swinging away before it turns,
/// starting past where its two lanes' lines cross, bowing out over its chord, turning tighter than the junction's design
/// speed holds, or leaving the junction's own disc — and the movements off one arm that cross each other (TER-5j), the
/// junctions whose arms end short of what they ask, and the ground inside a box no movement covers.
/// </summary>
/// <remarks>
/// <b>Quoted and gating nothing</b>: which of these a town lays is a fact about its survey, and the rows are what a
/// reader frames pictures from (<c>--at</c>, SHT-1).
/// </remarks>
internal static class ConnectorShape
{
    const int WorstListed = 40;

    /// <summary>Heading spent over what the two ends ask before a line is said to wind.</summary>
    const float WindsDeg = 10f;

    /// <summary>How far the first arc of a turn may curve the other way before the turn is said to swing out first.</summary>
    const float SwingsDeg = 5f;

    /// <summary>
    /// How far past the junction's disc a line may stand, beyond what either of its own ends stands past it, before
    /// it is said to leave it.
    /// </summary>
    const float OutsideM = 0.5f;

    /// <summary>How many movements that stray off their junction's disc are listed past the worst, to be looked at.</summary>
    const int OutsideListed = 8;

    /// <summary>A turn this far round is a U-turn, whose two lines cross nowhere near, and is counted on its own.</summary>
    const float UTurnDeg = 135f;

    /// <param name="atM">A place to read every movement near, arc by arc, instead of the town's.</param>
    public static void Run(string map, SimConfig config, Vector2? atM = null)
    {
        var plan = Maps.Plan(map, config, ShippedArt.Sizes);
        var lanes = plan.Paving(config).Lanes;
        if (atM is { } placeM)
        {
            Near(lanes, placeM, config);
            return;
        }

        var arms = plan.Ground.ArmsPerJunction();
        var floorM = config.JunctionCorneringRadiusM;

        var counts = new Dictionary<(string Kind, string What), int>();
        var byKind = new Dictionary<string, int>(StringComparer.Ordinal);
        var pastM = new List<float>();
        var rows = new List<(float Score, string Row)>();
        var spiralled = new SortedSet<int>();
        var outside = new List<string>();
        for (var connector = 0; connector < lanes.ConnectorCount; connector++)
        {
            var line = lanes.ArcsOfConnector(connector);
            if (line.Length == 0) continue;

            var junction = lanes.JunctionOfConnector(connector);
            var (fromM, toM) = (line[0].StartM, line[^1].EndM);
            var (fromRad, toRad) = (line[0].HeadingRad, line[^1].HeadingAtRad(line[^1].LengthM));

            var sweptRad = Spline.SweptRad(line);
            var askedRad = Spline.AskedRad(line);
            var turnedRad = Spline.TurnedRad(line);
            var uTurn = askedRad > UTurnDeg * MathF.PI / 180f;
            var kind = uTurn ? "UTurn" : lanes.ConnectorKind[connector].ToString();
            byKind[kind] = byKind.GetValueOrDefault(kind) + 1;
            var lengthM = Spline.TotalLengthM(line);
            var chordM = Vector2.Distance(fromM, toM);
            var tightestM = TightestM(line);
            var swingRad = Swing(line, turnedRad);
            var outsideM = Outside(line, plan.Junctions.CentreM[junction], plan.Junctions.RadiusM[junction]);
            var past = !uTurn && askedRad > config.Road.TurnStraightToleranceDeg * MathF.PI / 180f
                       && Spline.ToTheCorner(fromM, fromRad, toM, toRad, out _, out var beforeM, out var afterM)
                ? MathF.Min(beforeM, afterM)
                : float.PositiveInfinity;

            var what = new List<string>();
            if (sweptRad - askedRad > WindsDeg * MathF.PI / 180f) what.Add("winds");
            if (HeadsAway(line, fromRad, toRad)) what.Add("loops");
            if (swingRad > SwingsDeg * MathF.PI / 180f) what.Add("swings");
            if (past < 0f) what.Add("past");
            if (lengthM > Spline.HalfATurnOfItsChord * chordM + LineTolerance.JoinedM) what.Add("bows");
            if (tightestM < floorM) what.Add("tight");
            if (tightestM < config.CarTurningRadiusM) what.Add("unsteered");
            if (outsideM > OutsideM) what.Add("outside");
            foreach (var one in what) counts[(kind, one)] = counts.GetValueOrDefault((kind, one)) + 1;
            if (past < 0f) pastM.Add(-past);
            if (what.Count == 0) continue;

            var centreM = plan.Junctions.CentreM[junction];
            if (outsideM > OutsideM && outside.Count < OutsideListed)
            {
                outside.Add($"  {connector,7}{centreM.X,8:F0}{centreM.Y,8:F0}  {kind} turned {Deg(turnedRad):F0}°, {outsideM:F1} m off its disc of {plan.Junctions.RadiusM[junction]:F1} m");
            }

            if (uTurn && sweptRad - askedRad <= WindsDeg * MathF.PI / 180f) continue;
            if (what.Contains("loops") || what.Contains("past")) spiralled.Add(junction);

            var score = (what.Contains("loops") ? MathF.PI : 0f) + (sweptRad - askedRad) + MathF.Max(0f, -past) * 0.1f;
            rows.Add((score,
                $"  {connector,7}{centreM.X,8:F0}{centreM.Y,8:F0}{arms[junction],5}{plan.Junctions.RadiusM[junction],7:F1}" +
                $"  {kind,-9}{Deg(turnedRad),7:F0}{Deg(sweptRad),7:F0}{Deg(swingRad),7:F0}" +
                $"{(float.IsPositiveInfinity(past) ? "" : past.ToString("F1")),8}{lengthM / MathF.Max(chordM, 1e-3f),7:F2}" +
                $"{MathF.Min(tightestM, 999f),7:F1}{outsideM,7:F1}  {string.Join(',', what)}"));
        }

        var drivenM = 0.0;
        var turns = new HashSet<RoadTurn>();
        for (var connector = 0; connector < lanes.ConnectorCount; connector++)
        {
            drivenM += lanes.ConnectorLengthM[connector];
            turns.Add(new RoadTurn(
                lanes.JunctionOfConnector(connector), lanes.LaneRoad[lanes.ConnectorFromLane[connector]], lanes.LaneRoad[lanes.ConnectorToLane[connector]]));
        }

        Console.WriteLine($"{plan.Name}  {lanes.ConnectorCount} connectors, {drivenM:F3} m of them, making {turns.Count} turns road to road " +
                          $"at {plan.Junctions.Count} junctions; cornering floor {floorM:F1} m");
        var kinds = byKind.Keys.Order(StringComparer.Ordinal).ToArray();
        Console.WriteLine("  " + string.Join("  ", kinds.Select(kind => $"{kind}: {byKind[kind]}")));
        foreach (var what in (string[])["winds", "loops", "swings", "past", "bows", "tight", "unsteered", "outside"])
        {
            Console.WriteLine($"  {what,-8}" + string.Join("  ", kinds.Select(kind => $"{kind}: {counts.GetValueOrDefault((kind, what))}")));
        }

        if (pastM.Count > 0) Console.WriteLine("  past by  " + Spread(pastM));
        Unsteered(lanes, config);
        Crossing(plan, lanes);
        Opposed(plan, lanes, config.Road.TurnStraightToleranceDeg);
        HolesInBoxes(plan, config);
        if (rows.Count == 0) return;

        Console.WriteLine();
        Console.WriteLine($"  {"conn",7}{"x",8}{"y",8}{"arms",5}{"discM",7}  {"kind",-9}{"turn",7}{"swept",7}{"swing",7}" +
                          $"{"cornM",8}{"len/ch",7}{"tightM",7}{"outM",7}  what");
        rows.Sort((one, other) => other.Score.CompareTo(one.Score));
        foreach (var row in rows.Take(WorstListed)) Console.WriteLine(row.Row);
        foreach (var row in outside) Console.WriteLine(row);
        if (spiralled.Count == 0) return;

        // What squeezed each junction a movement loops or runs past its corner at: its shortest road, and what is on
        // the far end of it.
        Console.WriteLine();
        Console.WriteLine($"  {spiralled.Count} junctions where a movement loops or runs past its corner");
        Console.WriteLine($"  {"junc",7}{"x",8}{"y",8}{"arms",5}{"discM",7}{"shortM",8}{"farArms",8}{"farDiscM",9}");
        var roadM = new float[plan.Roads.Count];
        for (var road = 0; road < roadM.Length; road++) roadM[road] = Spline.TotalLengthM(plan.Roads.SegmentsOf(road));
        var shortestNear = new List<float>();
        foreach (var junction in spiralled)
        {
            var (shortest, far) = (-1, -1);
            for (var road = 0; road < plan.Roads.Count; road++)
            {
                var (from, to) = (plan.Roads.FromJunction[road], plan.Roads.ToJunction[road]);
                if (from != junction && to != junction) continue;
                if (shortest >= 0 && roadM[road] >= roadM[shortest]) continue;

                (shortest, far) = (road, from == junction ? to : from);
            }

            var centreM = plan.Junctions.CentreM[junction];
            shortestNear.Add(roadM[shortest]);
            if (shortestNear.Count > WorstListed) continue;

            Console.WriteLine($"  {junction,7}{centreM.X,8:F0}{centreM.Y,8:F0}{arms[junction],5}{plan.Junctions.RadiusM[junction],7:F1}" +
                              $"{roadM[shortest],8:F1}{arms[far],8}{plan.Junctions.RadiusM[far],9:F1}");
        }

        Console.WriteLine("  shortest road at them  " + Spread(shortestNear));
        Squeezed(plan, config, arms, spiralled);
    }

    /// <summary>
    /// <b>Every junction whose lanes end nearer its centre than its widest arm asks</b>
    /// (<see cref="SimConfig.JunctionRadiusAcrossM"/>), read off where each road's line begins and ends: by how much,
    /// and what stands at the far end of the road nearest it.
    /// </summary>
    static void Squeezed(CityPlan plan, SimConfig config, int[] arms, SortedSet<int> spiralled)
    {
        var widestM = new float[plan.Junctions.Count];
        var endM = new float[plan.Junctions.Count];
        var nearest = new int[plan.Junctions.Count];
        Array.Fill(endM, float.PositiveInfinity);
        Array.Fill(nearest, -1);
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var line = plan.Roads.SegmentsOf(road);
            foreach (var (junction, far, atM) in (ReadOnlySpan<(int, int, Vector2)>)
                     [(plan.Roads.FromJunction[road], plan.Roads.ToJunction[road], line[0].StartM),
                      (plan.Roads.ToJunction[road], plan.Roads.FromJunction[road], line[^1].EndM)])
            {
                widestM[junction] = MathF.Max(widestM[junction], plan.Roads.WidthM[road]);
                var offM = Vector2.Distance(atM, plan.Junctions.CentreM[junction]);
                if (offM >= endM[junction]) continue;

                (endM[junction], nearest[junction]) = (offM, far);
            }
        }

        var byFar = new SortedDictionary<string, (int All, int Spiralled, List<float> ShortM)>(StringComparer.Ordinal);
        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
            if (arms[junction] < 3 || plan.Junctions.RunsOff(junction)) continue;

            var shortM = config.JunctionRadiusAcrossM(widestM[junction]) - endM[junction];
            if (shortM <= LineTolerance.JoinedM) continue;

            var far = nearest[junction] < 0 ? "none" : arms[nearest[junction]] >= 3 ? "3+ arms" : $"{arms[nearest[junction]]} arms";
            var (all, spiralledHere, list) = byFar.GetValueOrDefault(far, (0, 0, []));
            list.Add(shortM);
            byFar[far] = (all + 1, spiralledHere + (spiralled.Contains(junction) ? 1 : 0), list);
        }

        Console.WriteLine();
        Console.WriteLine("  junctions of 3+ arms whose lanes end short of what their widest arm asks, by what the nearest road's far end is:");
        foreach (var (far, (all, spiralledHere, list)) in byFar)
        {
            Console.WriteLine($"    {far,-8} {all,5}, {spiralledHere} of them with a movement looping or past its corner;  short by  {Spread(list)}");
        }
    }

    /// <summary>
    /// <b>Ground inside a junction's box no movement covers</b>: every ring of the driven ground's boundary standing
    /// wholly inside one junction's disc — a kerbed island where the movements round it leave a patch between them,
    /// which the driven ground fills (<c>LaneShell</c>, TER-3c.7), so any counted here is one that filling missed.
    /// </summary>
    static void HolesInBoxes(CityPlan plan, SimConfig config)
    {
        const int Listed = 8;

        var (holes, areaM2) = (0, 0.0);
        var rows = new List<string>();
        foreach (var ring in plan.Paving(config).Perimeter(config).Chains)
        {
            var (leastM, mostM) = (new Vector2(float.MaxValue), new Vector2(float.MinValue));
            foreach (var arc in ring)
            {
                leastM = Vector2.Min(leastM, Vector2.Min(arc.StartM, arc.EndM));
                mostM = Vector2.Max(mostM, Vector2.Max(arc.StartM, arc.EndM));
            }

            var middleM = (leastM + mostM) * 0.5f;
            var reachM = Vector2.Distance(leastM, mostM) * 0.5f;
            for (var junction = 0; junction < plan.Junctions.Count; junction++)
            {
                if (Vector2.Distance(middleM, plan.Junctions.CentreM[junction]) + reachM > plan.Junctions.RadiusM[junction]) continue;

                holes++;
                var ringM2 = Math.Abs(Spline.EnclosedM2(ring));
                areaM2 += ringM2;
                if (rows.Count < Listed) rows.Add($"    {middleM.X,8:F0}{middleM.Y,8:F0}  {ringM2,6:F1} m²");
                break;
            }
        }

        Console.WriteLine($"  holes    {holes} rings of uncovered ground inside a junction's disc, {areaM2:F0} m² in all");
        foreach (var row in rows) Console.WriteLine(row);
    }

    /// <summary>
    /// Every street lane that bends anywhere tighter than a car steers (<see cref="SimConfig.CarTurningRadiusM"/>), worst
    /// first by how wide of it a car on full lock is ridden: past its allowance off the line it loses it (CAR-10a).
    /// </summary>
    static void Unsteered(LaneLines lanes, SimConfig config)
    {
        var steersM = config.CarTurningRadiusM;
        var rows = new List<(float WideM, string Row)>();
        var wider = 0;
        for (var lane = 0; lane < lanes.LaneCount; lane++)
        {
            if (!lanes.IsAStreetLane(lane)) continue;

            var (wideM, atM, alongM, sweptRad, tightestM) = (0f, Vector2.Zero, 0f, 0f, float.PositiveInfinity);
            var (runM, bendRad, bendTightestM, bendAtM, bendAlongM) = (0f, 0f, float.PositiveInfinity, Vector2.Zero, 0f);
            foreach (var arc in lanes.ArcsOf(lane))
            {
                var radiusM = arc.Curvature == 0f ? float.PositiveInfinity : 1f / MathF.Abs(arc.Curvature);
                if (radiusM < steersM && (bendRad == 0f || MathF.Sign(arc.Curvature) == MathF.Sign(bendRad)))
                {
                    if (bendRad == 0f) (bendAtM, bendAlongM) = (arc.StartM, runM);
                    bendRad += arc.Curvature * arc.LengthM;
                    bendTightestM = MathF.Min(bendTightestM, radiusM);
                }
                else
                {
                    Close();
                    if (radiusM < steersM) (bendRad, bendTightestM, bendAtM, bendAlongM) = (arc.Curvature * arc.LengthM, radiusM, arc.StartM, runM);
                }

                runM += arc.LengthM;
            }

            Close();
            if (wideM <= 0f) continue;

            if (wideM > lanes.LaneWidthM[lane]) wider++;
            rows.Add((wideM, $"    lane {lane,6} road {lanes.LaneRoad[lane],5} at {atM.X,7:F0}{atM.Y,7:F0}, {alongM,6:F1}/{lanes.LaneLengthM[lane],6:F1} m, " +
                             $"radius {tightestM:F2} m over {Deg(MathF.Abs(sweptRad)),4:F0}°, ridden {wideM:F2} m wide"));

            void Close()
            {
                if (bendRad == 0f) return;

                // A circle of the car's own drawn round the same turn stands off the bend's tightest by this much at its apex.
                var halfRad = MathF.Min(MathF.Abs(bendRad) * 0.5f, MathF.PI * 0.5f - 1e-3f);
                var bendWideM = (steersM - bendTightestM) * ((1f / MathF.Cos(halfRad)) - 1f);
                if (bendWideM > wideM) (wideM, atM, alongM, sweptRad, tightestM) = (bendWideM, bendAtM, bendAlongM, bendRad, bendTightestM);
                bendRad = 0f;
                bendTightestM = float.PositiveInfinity;
            }
        }

        Console.WriteLine($"  {rows.Count} street lanes bend tighter than a car steers ({steersM:F2} m), {wider} of them ridden wider of it than the lane");
        rows.Sort((one, other) => other.WideM.CompareTo(one.WideM));
        foreach (var (_, row) in rows.Take(WorstListed)) Console.WriteLine(row);
    }

    /// <summary>How near a place a movement's line passes to be read at it.</summary>
    const float NearM = 12f;

    /// <summary>Every movement whose line passes within <see cref="NearM"/> of a place: its lanes, its turn and each of its arcs.</summary>
    static void Near(LaneLines lanes, Vector2 placeM, SimConfig config)
    {
        Console.WriteLine($"movements within {NearM:F0} m of {placeM.X:F0}, {placeM.Y:F0}; a car steers {config.CarTurningRadiusM:F2} m");
        for (var connector = 0; connector < lanes.ConnectorCount; connector++)
        {
            var line = lanes.ArcsOfConnector(connector);
            if (line.Length == 0 || Sampled(line).Min(pointM => Vector2.Distance(pointM, placeM)) > NearM) continue;

            var (from, to) = (lanes.ConnectorFromLane[connector], lanes.ConnectorToLane[connector]);
            var (leaves, joins) = (Spline.SampleAt(lanes.ArcsOf(from), lanes.LaneLengthM[from]), Spline.SampleAt(lanes.ArcsOf(to), 0f));
            Console.WriteLine(
                $"    from ({leaves.PositionM.X:F2}, {leaves.PositionM.Y:F2}) heading {Deg(leaves.HeadingRad):F1}° " +
                $"to ({joins.PositionM.X:F2}, {joins.PositionM.Y:F2}) heading {Deg(joins.HeadingRad):F1}°");
            Console.WriteLine(
                $"  {connector,6} {lanes.ConnectorKind[connector],-9} lane {from} ({lanes.LaneLengthM[from]:F1} m) -> {to} ({lanes.LaneLengthM[to]:F1} m), " +
                $"turned {Deg(Spline.TurnedRad(line)):F0}° swept {Deg(Spline.SweptRad(line)):F0}°, {Spline.TotalLengthM(line):F1} m, tightest {TightestM(line):F2} m");
            foreach (var arc in line)
            {
                Console.WriteLine(
                    $"      ({arc.StartM.X:F1}, {arc.StartM.Y:F1}) heading {Deg(arc.HeadingRad):F0}° {arc.LengthM:F2} m " +
                    $"radius {(arc.Curvature == 0f ? "straight" : (1f / arc.Curvature).ToString("F2"))}");
            }
        }
    }

    static float Deg(float rad) => rad * 180f / MathF.PI;

    /// <summary>
    /// <b>Movements off one arm that cross each other</b>, from two different lanes onto two different lanes: lanes
    /// handed out to the turns in an order their lines do not keep, a car from an inner lane cutting across one from the
    /// kerb lane.
    /// </summary>
    static void Crossing(CityPlan plan, LaneLines lanes)
    {
        const int Listed = 12;

        var byArm = new Dictionary<(int Junction, int Road, bool Forward), List<int>>();
        for (var connector = 0; connector < lanes.ConnectorCount; connector++)
        {
            var from = lanes.ConnectorFromLane[connector];
            var arm = (lanes.JunctionOfConnector(connector), lanes.LaneRoad[from], lanes.LaneForward[from]);
            if (!byArm.TryGetValue(arm, out var list)) byArm[arm] = list = [];
            list.Add(connector);
        }

        var (pairs, arms) = (0, 0);
        var rows = new List<string>();
        foreach (var (arm, connectors) in byArm)
        {
            var crossedHere = 0;
            for (var one = 0; one < connectors.Count; one++)
            {
                for (var other = one + 1; other < connectors.Count; other++)
                {
                    var (a, b) = (connectors[one], connectors[other]);
                    if (lanes.ConnectorFromLane[a] == lanes.ConnectorFromLane[b] || lanes.ConnectorToLane[a] == lanes.ConnectorToLane[b]) continue;
                    if (!Crosses(Sampled(lanes.ArcsOfConnector(a)), Sampled(lanes.ArcsOfConnector(b)))) continue;

                    crossedHere++;
                    if (rows.Count < Listed)
                    {
                        var atM = plan.Junctions.CentreM[arm.Junction];
                        rows.Add($"    {atM.X,8:F0}{atM.Y,8:F0}  road {arm.Road} lane {lanes.LaneFromKerb[lanes.ConnectorFromLane[a]]} {lanes.ConnectorKind[a]} " +
                                 $"onto road {lanes.LaneRoad[lanes.ConnectorToLane[a]]} crosses lane {lanes.LaneFromKerb[lanes.ConnectorFromLane[b]]} " +
                                 $"{lanes.ConnectorKind[b]} onto road {lanes.LaneRoad[lanes.ConnectorToLane[b]]}");
                    }
                }
            }

            pairs += crossedHere;
            if (crossedHere > 0) arms++;
        }

        Console.WriteLine($"  crossing {pairs} pairs of movements off one arm cross each other, at {arms} of {byArm.Count} arms");
        foreach (var row in rows) Console.WriteLine(row);
    }

    /// <summary>
    /// <b>Turns to the far side, and U-turns, off arms of one junction facing each other whose ground overlaps</b>
    /// (TER-5d.2) — lines nearer than half their two widths: two cars turning across each other's way at once collide
    /// unless each turns on its own side of the box.
    /// </summary>
    static void Opposed(CityPlan plan, LaneLines lanes, float straightToleranceDeg)
    {
        const int Listed = 12;

        var byJunction = new Dictionary<int, List<int>>();
        for (var connector = 0; connector < lanes.ConnectorCount; connector++)
        {
            var line = lanes.ArcsOfConnector(connector);
            if (line.Length == 0) continue;
            if (lanes.ConnectorKind[connector] != LaneTurn.FarSide && Spline.AskedRad(line) <= UTurnDeg * MathF.PI / 180f) continue;

            var junction = lanes.JunctionOfConnector(connector);
            if (!byJunction.TryGetValue(junction, out var list)) byJunction[junction] = list = [];
            list.Add(connector);
        }

        var (lefts, uTurns, crossing, junctions) = (0, 0, 0, new HashSet<int>());
        var headOnRad = MathF.PI - (straightToleranceDeg * MathF.PI / 180f);
        var rows = new List<string>();
        foreach (var (junction, connectors) in byJunction)
        {
            for (var one = 0; one < connectors.Count; one++)
            {
                for (var other = one + 1; other < connectors.Count; other++)
                {
                    var a = lanes.ArcsOfConnector(connectors[one]);
                    var b = lanes.ArcsOfConnector(connectors[other]);
                    if (MathF.Abs(Spline.WrapRad(a[0].HeadingRad - b[0].HeadingRad)) < headOnRad) continue;

                    var clearM = (lanes.ConnectorWidthM(connectors[one]) + lanes.ConnectorWidthM(connectors[other])) * 0.5f;
                    var nearestM = Nearest(Sampled(a), Sampled(b));
                    if (nearestM >= clearM) continue;
                    if (nearestM == 0f) crossing++;

                    var uTurn = Spline.AskedRad(a) > UTurnDeg * MathF.PI / 180f || Spline.AskedRad(b) > UTurnDeg * MathF.PI / 180f;
                    if (uTurn) uTurns++;
                    else lefts++;

                    junctions.Add(junction);
                    if (rows.Count < Listed)
                    {
                        var atM = plan.Junctions.CentreM[junction];
                        rows.Add($"    {atM.X,8:F0}{atM.Y,8:F0}  {(uTurn ? "U-turns" : "far-side turns")} {connectors[one]} and {connectors[other]}, " +
                                 $"{nearestM:F2} m apart of {clearM:F2}, {a.Length} and {b.Length} pieces, tightest {TightestM(a):F1} and {TightestM(b):F1} m");
                    }
                }
            }
        }

        Console.WriteLine($"  opposed  {lefts} pairs of far-side turns and {uTurns} of U-turns off arms facing each other overlap, {crossing} of them " +
                          $"crossing, at {junctions.Count} junctions");
        foreach (var row in rows) Console.WriteLine(row);
    }

    /// <summary>How near two polylines come, nought where they cross.</summary>
    static float Nearest(List<Vector2> one, List<Vector2> other)
    {
        if (Crosses(one, other)) return 0f;

        var nearestM = float.PositiveInfinity;
        foreach (var atM in one)
        {
            foreach (var otherM in other) nearestM = MathF.Min(nearestM, Vector2.Distance(atM, otherM));
        }

        return nearestM;
    }

    static List<Vector2> Sampled(ReadOnlySpan<ArcSeg> line)
    {
        var pointsM = new List<Vector2>();
        foreach (var arc in line)
        {
            for (var alongM = 0f; alongM < arc.LengthM; alongM += 0.5f) pointsM.Add(arc.PointAtM(alongM));
        }

        pointsM.Add(line[^1].EndM);
        return pointsM;
    }

    /// <summary>Whether two polylines cross anywhere but within half a metre of either's ends, where movements off one arm meet.</summary>
    static bool Crosses(List<Vector2> one, List<Vector2> other)
    {
        for (var a = 1; a < one.Count; a++)
        {
            for (var b = 1; b < other.Count; b++)
            {
                if (!Segments(one[a - 1], one[a], other[b - 1], other[b], out var atM)) continue;
                if (Vector2.Distance(atM, one[0]) < 0.5f || Vector2.Distance(atM, other[0]) < 0.5f) continue;
                if (Vector2.Distance(atM, one[^1]) < 0.5f || Vector2.Distance(atM, other[^1]) < 0.5f) continue;

                return true;
            }
        }

        return false;
    }

    static bool Segments(Vector2 p, Vector2 p2, Vector2 q, Vector2 q2, out Vector2 atM)
    {
        atM = default;
        var r = p2 - p;
        var s = q2 - q;
        var denominator = Spline.Cross(r, s);
        if (MathF.Abs(denominator) < 1e-9f) return false;

        var t = Spline.Cross(q - p, s) / denominator;
        var u = Spline.Cross(q - p, r) / denominator;
        if (t < 0f || t > 1f || u < 0f || u > 1f) return false;

        atM = p + (r * t);
        return true;
    }

    /// <summary>
    /// Whether the line anywhere heads more than a quarter turn off both the way it sets off and the way it arrives,
    /// sampled every quarter metre: a line going back on itself, which a corner, a jog and a U-turn never do.
    /// </summary>
    static bool HeadsAway(ReadOnlySpan<ArcSeg> line, float fromRad, float toRad)
    {
        const float AwayRad = (MathF.PI * 0.5f) + 0.05f;
        foreach (var arc in line)
        {
            for (var alongM = 0f; alongM <= arc.LengthM; alongM += 0.25f)
            {
                var headingRad = arc.HeadingAtRad(alongM);
                if (MathF.Abs(Spline.WrapRad(headingRad - fromRad)) > AwayRad && MathF.Abs(Spline.WrapRad(headingRad - toRad)) > AwayRad) return true;
            }
        }

        return false;
    }

    static float TightestM(ReadOnlySpan<ArcSeg> line)
    {
        var bend = 0f;
        foreach (var arc in line) bend = MathF.Max(bend, MathF.Abs(arc.Curvature));

        return bend <= 1e-6f ? float.PositiveInfinity : 1f / bend;
    }

    /// <summary>
    /// How much heading a line spends turning against the way it turns overall, read from its start: what a turn
    /// swings out by before it turns, and for a straight on nothing it has to.
    /// </summary>
    static float Swing(ReadOnlySpan<ArcSeg> line, float turnedRad)
    {
        if (MathF.Abs(turnedRad) < 1e-3f) return 0f;

        var against = 0f;
        foreach (var arc in line)
        {
            var bentRad = arc.LengthM * arc.Curvature;
            if (MathF.Sign(bentRad) == MathF.Sign(turnedRad)) break;

            against += MathF.Abs(bentRad);
        }

        return against;
    }

    /// <summary>
    /// How far past the junction's disc the line strays beyond what its own two ends stand past it, sampled every half
    /// metre.
    /// </summary>
    static float Outside(ReadOnlySpan<ArcSeg> line, Vector2 centreM, float radiusM)
    {
        radiusM = MathF.Max(radiusM, MathF.Max(Vector2.Distance(line[0].StartM, centreM), Vector2.Distance(line[^1].EndM, centreM)));
        var most = 0f;
        foreach (var arc in line)
        {
            for (var alongM = 0f; alongM <= arc.LengthM; alongM += 0.5f)
            {
                most = MathF.Max(most, Vector2.Distance(arc.PointAtM(alongM), centreM) - radiusM);
            }
        }

        return most;
    }

    static string Spread(List<float> values)
    {
        values.Sort();
        return $"n {values.Count}  p50 {values[values.Count / 2]:F1}  p90 {values[values.Count * 9 / 10]:F1}  max {values[^1]:F1} m";
    }
}
