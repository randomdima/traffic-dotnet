using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.CityGen.Zones;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>What a surveyed place is zoned for, as its map holds it</b> (<see cref="TownMap.ZoneArrays"/>, GEN-58): the land
/// uses of the zones layer laid on squares over the map's frame, each square the kind of what stands on it, and every
/// patch of squares of one kind a zone — a part of the town built alike, never a place OSM names — with the settings its
/// buildings measure (<see cref="ZoneParam"/>), so the town its zones lay is built as the place is.
/// </summary>
/// <remarks>
/// <para>
/// <b>A square is the kind of the smallest land use holding its middle</b>, else the whole map's. A land use is a kind by
/// what it is (<see cref="KindOf"/>); a residential one by what is built in it — towers an estate, houses a suburb, a
/// terrace of flats built to the walk an old town, anything else a residential quarter — measured as a zone is.
/// </para>
/// <para>
/// <b>Then made plain</b>: a square whose neighbours are mostly one other kind is that kind (<see cref="SwayedBy"/>); a
/// patch of fewer squares than <see cref="LeastM2"/> covers is the kind it borders most; and a zone's outline is its
/// patch's, thinned to within <see cref="ToleranceM"/> and held to the frame. A patch inside another's outline is a zone
/// inside that one, so no zone has a hole; and ground no land use holds is the whole map's, measured as a zone's is.
/// </para>
/// <para>
/// <b>What its buildings measure is read off the map's own streets</b>: every street (a road of a class that is one,
/// <see cref="TracedRoad.Rank"/>) is walked <see cref="ProbeStepM"/> at a time, and either side of it a probe runs out
/// from its carriageway's edge to the first building it meets within <see cref="ProbeReachM"/> — the probe the zone's
/// own, read <see cref="AskedAtM"/> from the edge, as the engine reads which zone a front is in. A zone's buildings so
/// met are its front row, and of them it measures:
/// how many of its probes met one (<see cref="ZoneParam.Frontage"/>); how far off the edge they stand and how widely
/// that spreads (<see cref="ZoneParam.FrontM"/>, <see cref="ZoneParam.FrontSpreadM"/>); how far they turn off square to
/// their street (<see cref="ZoneParam.SkewDeg"/>); how many are not like the one before along it
/// (<see cref="ZoneParam.Variety"/>); and what they are drawn as (the looks' shares). Of the buildings standing in it that
/// no probe met it measures the ground of its own they cover (<see cref="ZoneParam.Interior"/>) and the bearing they
/// share, where they share one (<see cref="ZoneParam.BearingDeg"/>). A zone met too few times says none of it, and is
/// laid as its kind is.
/// </para>
/// </remarks>
internal static class ZoneHints
{
    /// <summary>The side of a square the land uses are laid on: a few plots, so a zone is a block or more and never a yard.</summary>
    const double SquareM = 25;

    /// <summary>The least ground a patch of one kind covers to be a zone; a smaller one is the kind round it.</summary>
    const double LeastM2 = 5000;

    /// <summary>How many of a square's eight neighbours of one other kind make it that kind: a spur or a notch a square wide.</summary>
    const int SwayedBy = 5;

    /// <summary>How far a zone's outline may stand off its patch's once thinned: under a square, so a staircase is a line.</summary>
    const double ToleranceM = 20;

    /// <summary>The ground a square of the whole map's own stands for, read where no land use holds it.</summary>
    const int Unzoned = -1;

    /// <summary>The cell the zones are filed in to be asked which holds a place, in metres: a few blocks.</summary>
    const double CellM = 250;

    /// <summary>The cell the buildings are filed in to be asked whether one stands at a place, in metres.</summary>
    const double BuildingCellM = 64;

    /// <summary>How far apart along a street its frontage is probed.</summary>
    const double ProbeStepM = 5;

    /// <summary>How finely a probe walks out from the carriageway's edge.</summary>
    const double ProbeWalkM = 1;

    /// <summary>How far past the carriageway's edge a probe looks for a front: an estate's slab set back across its green.</summary>
    const double ProbeReachM = 40;

    /// <summary>
    /// Where along a probe its zone is read, past the carriageway's edge: the engine's building line, a walk's width, and
    /// the depth behind it the engine asks a front's zone at.
    /// </summary>
    const double AskedAtM = 9;

    /// <summary>A lane's width, where nothing measured a carriageway's: the engine's own, near enough for a hint.</summary>
    const double LaneM = 3.5;

    /// <summary>The fewest probes, front-row buildings or buildings behind a zone is measured off; fewer say nothing.</summary>
    const int MeasuredLeast = 8;

    /// <summary>How near two footprints' areas stand, as a share, to be the same building again along a street.</summary>
    const double AlikeShare = 0.1;

    /// <summary>How strongly the buildings behind the frontage share one bearing, of one, before it is said.</summary>
    const double SharedBearingLeast = 0.6;

    /// <summary>The least share of a front row's buildings a look is said at; fewer is noise.</summary>
    const double LookLeast = 0.03;

    /// <summary>The share of an estate's front row that are towers, a suburb's that are houses.</summary>
    const double TowerShare = 0.3, HouseShare = 0.6;

    /// <summary>How built up an old town's frontage is and how much of its ground its buildings cover.</summary>
    const double OldFrontage = 0.6, OldCoverage = 0.3;

    /// <summary>A land use or a zone: its rings, its kind, and the one it stands inside.</summary>
    sealed class Zone(Pt[][] rings, ZoneKind kind)
    {
        public Pt[][] Rings { get; } = rings;
        public Box Box { get; } = Box.Of(rings[0]);
        public double AreaM2 { get; } = Shape.Area([rings[0]], rings[1..]);
        public ZoneKind Kind { get; set; } = kind;
        public int Parent { get; set; } = -1;
        public int Depth { get; set; }
    }

    /// <summary>What a zone's probes met, and what stands in it.</summary>
    sealed class Tally
    {
        public int Asked;
        public int Met;
        public readonly List<double> FrontM = [];
        public readonly List<double> SkewDeg = [];
        public readonly HashSet<int> FrontRow = [];
        public int Pairs;
        public int Alike;
        public readonly List<int> Standing = [];
    }

    /// <summary>The frame's squares, row by row, each a kind's number or <see cref="Unzoned"/>.</summary>
    sealed class Squares(int across, int down)
    {
        public int Across { get; } = across;
        public int Down { get; } = down;
        public int[] Kind { get; } = new int[across * down];

        public bool On(int x, int y) => (uint)x < (uint)Across && (uint)y < (uint)Down;
    }

    /// <param name="keeps">Whether a land use is kept, read off its outline's places on the map.</param>
    /// <param name="streets">The map whose streets the frontage is read along.</param>
    public static TownMap.ZoneArrays Of(string folder, Plane plane, SurveyFootprints buildings, Func<Pt[], bool> keeps, TownMap streets)
    {
        var outlines = new Pt[buildings.Count][];
        var footprints = new Dictionary<(int, int), List<int>>();
        for (var building = 0; building < buildings.Count; building++)
        {
            outlines[building] = Points(buildings.RingOf(buildings.RingOffsets[building]));
            var box = Box.Of(outlines[building]);
            for (var x = BuildingCell(box.MinX); x <= BuildingCell(box.MaxX); x++)
            {
                for (var y = BuildingCell(box.MinY); y <= BuildingCell(box.MaxY); y++)
                {
                    if (!footprints.TryGetValue((x, y), out var here)) footprints[(x, y)] = here = [];
                    here.Add(building);
                }
            }
        }

        var uses = Read(folder, plane, keeps);
        Nest(uses);
        var usesFiled = File(uses);
        var usesTallied = Tallied(uses.Count, at => Holding(uses, usesFiled, at), streets, buildings, outlines, footprints);
        for (var use = 0; use < uses.Count; use++)
        {
            if (uses[use].Kind == ZoneKind.Residential) uses[use].Kind = Residential(usesTallied[use], uses[use].AreaM2, buildings, outlines);
        }

        var (widthM, heightM) = (plane.Frame.WidthM, plane.Frame.HeightM);
        var squares = Laid(uses, usesFiled, widthM, heightM);
        Swayed(squares);
        Merged(squares);
        var zones = Zoned(squares, widthM, heightM);

        // The whole map is zone nought here, and every zone one on from where the list holds it.
        var filed = File(zones);
        var tallied = Tallied(zones.Count + 1, at => Holding(zones, filed, at) + 1, streets, buildings, outlines, footprints);
        var ownM2 = new double[zones.Count + 1];
        ownM2[TownMap.ZoneArrays.Root] = widthM * heightM;
        for (var zone = 0; zone < zones.Count; zone++)
        {
            ownM2[zone + 1] += zones[zone].AreaM2;
            ownM2[zones[zone].Parent + 1] -= zones[zone].AreaM2;
        }

        var builder = new TownMap.ZoneArrays.Builder();
        builder.Add(-1, ZoneKind.Town, [.. Measured(tallied[0], ownM2[0], buildings, outlines)], [TownMap.WholeOutline(new Vector2((float)widthM, (float)heightM))]);
        for (var zone = 0; zone < zones.Count; zone++)
        {
            builder.Add(zones[zone].Parent + 1, zones[zone].Kind, [.. Measured(tallied[zone + 1], ownM2[zone + 1], buildings, outlines)], [Vectors(zones[zone].Rings[0])]);
        }

        return builder.Arrays();
    }

    /// <summary>Every building counted to the zone its middle stands in, and every street's frontage probed into the zones.</summary>
    /// <param name="holding">The zone a place stands in, or −1 for none counted.</param>
    static Tally[] Tallied(
        int count, Func<Pt, int> holding, TownMap streets, SurveyFootprints buildings, Pt[][] outlines, Dictionary<(int, int), List<int>> footprints)
    {
        var tallies = new Tally[count];
        for (var zone = 0; zone < count; zone++) tallies[zone] = new Tally();
        for (var building = 0; building < buildings.Count; building++)
        {
            if (holding(Shape.Centroid(outlines[building])) is >= 0 and var standsIn) tallies[standsIn].Standing.Add(building);
        }

        Probe(tallies, holding, streets, buildings, outlines, footprints);
        return tallies;
    }

    /// <summary>Every land use given its parent — the smallest larger one holding a place inside it.</summary>
    static void Nest(List<Zone> zones)
    {
        var placed = new Dictionary<(int, int), List<int>>();
        foreach (var zone in Enumerable.Range(0, zones.Count).OrderByDescending(zone => zones[zone].AreaM2))
        {
            var at = Inside(zones[zone].Rings);
            var (parent, parentM2) = (-1, double.MaxValue);
            if (placed.TryGetValue((Cell(at.X), Cell(at.Y)), out var near))
            {
                foreach (var other in near)
                {
                    var candidate = zones[other];
                    if (candidate.AreaM2 >= parentM2 || candidate.AreaM2 <= zones[zone].AreaM2 || !candidate.Box.Holds(at) || !Shape.Inside(at, candidate.Rings)) continue;

                    (parent, parentM2) = (other, candidate.AreaM2);
                }
            }

            zones[zone].Parent = parent;
            zones[zone].Depth = parent >= 0 ? zones[parent].Depth + 1 : 1;
            var box = zones[zone].Box;
            for (var x = Cell(box.MinX); x <= Cell(box.MaxX); x++)
            {
                for (var y = Cell(box.MinY); y <= Cell(box.MaxY); y++)
                {
                    if (!placed.TryGetValue((x, y), out var here)) placed[(x, y)] = here = [];
                    here.Add(zone);
                }
            }
        }
    }

    /// <summary>A place inside an outline: its centre of area where that is inside it, else beside the middle of an edge.</summary>
    static Pt Inside(Pt[][] rings)
    {
        var centre = Shape.Centroid(rings[0]);
        if (Shape.Inside(centre, rings)) return centre;

        var outline = rings[0];
        for (var at = 0; at < outline.Length; at++)
        {
            var (a, b) = (outline[at], outline[(at + 1) % outline.Length]);
            var run = b - a;
            if (run.Length <= 0) continue;

            var middle = a + (run * 0.5);
            var across = new Pt(-run.Y / run.Length, run.X / run.Length) * Math.Min(1.0, run.Length * 0.1);
            if (Shape.Inside(middle + across, rings)) return middle + across;
            if (Shape.Inside(middle - across, rings)) return middle - across;
        }

        return centre;
    }

    static Dictionary<(int, int), int[]> File(List<Zone> zones)
    {
        var filed = new Dictionary<(int, int), List<int>>();
        for (var zone = 0; zone < zones.Count; zone++)
        {
            var box = zones[zone].Box;
            for (var x = Cell(box.MinX); x <= Cell(box.MaxX); x++)
            {
                for (var y = Cell(box.MinY); y <= Cell(box.MaxY); y++)
                {
                    if (!filed.TryGetValue((x, y), out var here)) filed[(x, y)] = here = [];
                    here.Add(zone);
                }
            }
        }

        return filed.ToDictionary(cell => cell.Key, cell => cell.Value.OrderByDescending(zone => zones[zone].Depth).ThenBy(zone => zones[zone].AreaM2).ToArray());
    }

    /// <summary>The deepest zone holding a place, the smaller of two as deep, else −1 — as the engine asks (<see cref="ZoneTree"/>).</summary>
    static int Holding(List<Zone> zones, Dictionary<(int, int), int[]> filed, Pt at)
    {
        if (!filed.TryGetValue((Cell(at.X), Cell(at.Y)), out var near)) return -1;

        foreach (var zone in near)
        {
            if (zones[zone].Box.Holds(at) && Shape.Inside(at, zones[zone].Rings)) return zone;
        }

        return -1;
    }

    /// <summary>Each square the kind of the land use its middle stands in, or <see cref="Unzoned"/>.</summary>
    static Squares Laid(List<Zone> uses, Dictionary<(int, int), int[]> filed, double widthM, double heightM)
    {
        var squares = new Squares((int)Math.Ceiling(widthM / SquareM), (int)Math.Ceiling(heightM / SquareM));
        for (var y = 0; y < squares.Down; y++)
        {
            for (var x = 0; x < squares.Across; x++)
            {
                var use = Holding(uses, filed, new Pt((x + 0.5) * SquareM, (y + 0.5) * SquareM));
                squares.Kind[(y * squares.Across) + x] = use >= 0 ? (int)uses[use].Kind : Unzoned;
            }
        }

        return squares;
    }

    /// <summary>Every square whose neighbours are <see cref="SwayedBy"/> or more of one other kind made that kind, read off the squares as they were.</summary>
    static void Swayed(Squares squares)
    {
        var was = squares.Kind.ToArray();
        var counted = new int[Enum.GetValues<ZoneKind>().Length + 1];
        for (var y = 0; y < squares.Down; y++)
        {
            for (var x = 0; x < squares.Across; x++)
            {
                Array.Clear(counted);
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        if ((dx, dy) != (0, 0) && squares.On(x + dx, y + dy)) counted[was[((y + dy) * squares.Across) + x + dx] + 1]++;
                    }
                }

                var most = Array.IndexOf(counted, counted.Max());
                if (counted[most] >= SwayedBy) squares.Kind[(y * squares.Across) + x] = most - 1;
            }
        }
    }

    /// <summary>
    /// Every patch of fewer squares than <see cref="LeastM2"/> covers made the kind it shares the most edges with, the
    /// smallest first, and the patches read again until none is that small but one bordering nothing else.
    /// </summary>
    static void Merged(Squares squares)
    {
        var least = (int)Math.Ceiling(LeastM2 / (SquareM * SquareM));
        var borders = new int[Enum.GetValues<ZoneKind>().Length + 1];
        for (var changed = true; changed;)
        {
            changed = false;
            foreach (var patch in Labelled(squares).Patches.Where(patch => patch.Count < least).OrderBy(patch => patch.Count))
            {
                var own = squares.Kind[patch[0]];
                Array.Clear(borders);
                foreach (var square in patch)
                {
                    var (x, y) = (square % squares.Across, square / squares.Across);
                    foreach (var (nx, ny) in (ReadOnlySpan<(int, int)>)[(x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)])
                    {
                        if (squares.On(nx, ny) && squares.Kind[(ny * squares.Across) + nx] is var other && other != own) borders[other + 1]++;
                    }
                }

                var most = Array.IndexOf(borders, borders.Max());
                if (borders[most] == 0) continue;

                foreach (var square in patch) squares.Kind[square] = most - 1;
                changed = true;
            }
        }
    }

    /// <summary>The squares in patches of one kind each, a square meeting its own kind along an edge and not at a corner.</summary>
    static (int[] Label, List<List<int>> Patches) Labelled(Squares squares)
    {
        var label = new int[squares.Kind.Length];
        Array.Fill(label, -1);
        var patches = new List<List<int>>();
        var open = new Stack<int>();
        for (var first = 0; first < label.Length; first++)
        {
            if (label[first] >= 0) continue;

            var patch = new List<int>();
            label[first] = patches.Count;
            open.Push(first);
            while (open.TryPop(out var square))
            {
                patch.Add(square);
                var (x, y) = (square % squares.Across, square / squares.Across);
                foreach (var (nx, ny) in (ReadOnlySpan<(int, int)>)[(x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)])
                {
                    var next = (ny * squares.Across) + nx;
                    if (!squares.On(nx, ny) || label[next] >= 0 || squares.Kind[next] != squares.Kind[first]) continue;

                    label[next] = patches.Count;
                    open.Push(next);
                }
            }

            patches.Add(patch);
        }

        return (label, patches);
    }

    /// <summary>
    /// <b>Every patch of a kind a zone</b>: its outline traced round its squares, nested in the smallest other whose
    /// outline holds it, then thinned and held to the frame — handed back largest first, so a parent is always before
    /// its zones. One thinned to nothing is dropped, and its zones are its parent's.
    /// </summary>
    static List<Zone> Zoned(Squares squares, double widthM, double heightM)
    {
        var (label, patches) = Labelled(squares);
        var traced = patches.Where(patch => squares.Kind[patch[0]] != Unzoned)
            .Select(patch => (Patch: patch, Outline: Outline(squares, label, patch)))
            .Select(patch => (patch.Patch, patch.Outline, Box: Box.Of(patch.Outline), AreaM2: Math.Abs(Shape.SignedArea(patch.Outline))))
            .OrderByDescending(patch => patch.AreaM2)
            .ToList();

        var zones = new List<Zone>();
        var keptAs = new int[traced.Count];
        for (var at = 0; at < traced.Count; at++)
        {
            var square = traced[at].Patch[0];
            var middle = new Pt(((square % squares.Across) + 0.5) * SquareM, ((square / squares.Across) + 0.5) * SquareM);
            var parent = -1;
            for (var other = at - 1; other >= 0; other--)
            {
                if (traced[other].AreaM2 > traced[at].AreaM2 && traced[other].Box.Holds(middle) && Shape.Inside(middle, [traced[other].Outline]))
                {
                    parent = keptAs[other];
                    break;
                }
            }

            var outline = Thinned(traced[at].Outline).Select(point => new Pt(Math.Clamp(point.X, 0, widthM), Math.Clamp(point.Y, 0, heightM))).ToArray();
            if (outline.Length < 3 || Math.Abs(Shape.SignedArea(outline)) <= 0)
            {
                keptAs[at] = parent;
                continue;
            }

            keptAs[at] = zones.Count;
            zones.Add(new Zone([outline], (ZoneKind)squares.Kind[square]) { Parent = parent, Depth = parent >= 0 ? zones[parent].Depth + 1 : 1 });
        }

        return zones;
    }

    /// <summary>
    /// <b>A patch's outline</b>, in metres: the edges its squares share with none of its own, each run with the patch on
    /// its left and joined end to start, turning toward the patch where two leave one corner — so a patch meeting itself
    /// at a corner is two rings rather than one crossing itself. The largest ring is the outline; the rest are its holes,
    /// whose ground the zones inside it hold.
    /// </summary>
    static Pt[] Outline(Squares squares, int[] label, List<int> patch)
    {
        var own = label[patch[0]];
        var leaving = new Dictionary<(int X, int Y), List<(int DX, int DY)>>();
        foreach (var square in patch)
        {
            var (x, y) = (square % squares.Across, square / squares.Across);
            if (!Own(x, y - 1)) Leave((x, y), (1, 0));
            if (!Own(x + 1, y)) Leave((x + 1, y), (0, 1));
            if (!Own(x, y + 1)) Leave((x + 1, y + 1), (-1, 0));
            if (!Own(x - 1, y)) Leave((x, y + 1), (0, -1));
        }

        var used = new HashSet<((int, int), (int, int))>();
        var (outline, outlineArea) = (Array.Empty<Pt>(), 0.0);
        foreach (var (corner, ways) in leaving)
        {
            foreach (var way in ways)
            {
                if (used.Contains((corner, way))) continue;

                var ring = new List<Pt>();
                var (at, heading) = (corner, way);
                do
                {
                    used.Add((at, heading));
                    var next = (at.X + heading.DX, at.Y + heading.DY);
                    var onward = Onward(next, heading);
                    if (onward != heading) ring.Add(new Pt(next.Item1 * SquareM, next.Item2 * SquareM));

                    (at, heading) = (next, onward);
                }
                while ((at, heading) != (corner, way));

                var area = Math.Abs(Shape.SignedArea([.. ring]));
                if (area > outlineArea) (outline, outlineArea) = ([.. ring], area);
            }
        }

        return outline;

        bool Own(int x, int y) => squares.On(x, y) && label[(y * squares.Across) + x] == own;

        void Leave((int, int) corner, (int, int) way)
        {
            if (!leaving.TryGetValue(corner, out var ways)) leaving[corner] = ways = [];
            ways.Add(way);
        }

        // Toward the patch where it can, else on, else away.
        (int DX, int DY) Onward((int, int) corner, (int DX, int DY) heading)
        {
            var ways = leaving[corner];
            var toward = (-heading.DY, heading.DX);
            if (ways.Contains(toward)) return toward;

            return ways.Contains(heading) ? heading : (heading.DY, -heading.DX);
        }
    }

    /// <summary>
    /// <b>Every street's frontage probed either side</b>: from the carriageway's edge out to the first building, each
    /// probe counted to the zone it is asked in, the building it met — if any — that zone's front row, and how far off,
    /// how turned and how like the last one along it is.
    /// </summary>
    static void Probe(
        Tally[] tallies, Func<Pt, int> holding, TownMap streets, SurveyFootprints buildings, Pt[][] outlines, Dictionary<(int, int), List<int>> footprints)
    {
        var area = new double[buildings.Count];
        for (var building = 0; building < buildings.Count; building++) area[building] = Math.Abs(Shape.SignedArea(outlines[building]));

        foreach (var road in streets.Roads)
        {
            if (TracedRoad.Rank(road.Highway) == 0) continue;

            var halfM = (road.WidthM ?? (road.Lanes * LaneM)) * 0.5;
            foreach (var side in (ReadOnlySpan<double>)[-1, 1])
            {
                var last = -1;
                for (var at = 1; at < road.Points.Length; at++)
                {
                    var (fromM, toM) = (streets.PointM[road.Points[at - 1]], streets.PointM[road.Points[at]]);
                    var (a, b) = (new Pt(fromM.X, fromM.Y), new Pt(toM.X, toM.Y));
                    var lengthM = (b - a).Length;
                    if (lengthM <= 0) continue;

                    var along = (b - a) * (1 / lengthM);
                    var across = new Pt(-along.Y, along.X) * side;
                    for (var stepM = ProbeStepM * 0.5; stepM < lengthM; stepM += ProbeStepM)
                    {
                        var edge = a + (along * stepM) + (across * halfM);
                        if (holding(edge + (across * AskedAtM)) is not (>= 0 and var zone)) continue;

                        var asked = tallies[zone];
                        asked.Asked++;
                        if (Met(edge, across) is not (>= 0 and var building, var offM))
                        {
                            last = -1;
                            continue;
                        }

                        asked.Met++;
                        asked.FrontM.Add(offM);
                        if (building == last) continue;

                        if (asked.FrontRow.Add(building)) asked.SkewDeg.Add(Skew(outlines[building], along));
                        if (last >= 0)
                        {
                            asked.Pairs++;
                            if (buildings.Look[last] == buildings.Look[building] && Math.Abs(area[last] - area[building]) <= AlikeShare * Math.Max(area[last], area[building])) asked.Alike++;
                        }

                        last = building;
                    }
                }
            }
        }

        // The first building a probe meets walking out from the edge, and how far out.
        (int Building, double OffM) Met(Pt edge, Pt across)
        {
            for (var offM = 0.0; offM <= ProbeReachM; offM += ProbeWalkM)
            {
                var at = edge + (across * offM);
                if (!footprints.TryGetValue((BuildingCell(at.X), BuildingCell(at.Y)), out var near)) continue;

                foreach (var building in near)
                {
                    var inside = false;
                    for (var ring = buildings.RingOffsets[building]; ring < buildings.RingOffsets[building + 1]; ring++) inside ^= Encloses(buildings.RingOf(ring), at);

                    if (inside) return (building, offM);
                }
            }

            return (-1, 0);
        }
    }

    /// <summary>How far a building turns off square to a street, by its longest wall, in degrees from nought to forty-five.</summary>
    static double Skew(Pt[] outline, Pt along)
    {
        var (longest, wall) = (0.0, along);
        for (var at = 0; at < outline.Length; at++)
        {
            var run = outline[(at + 1) % outline.Length] - outline[at];
            if (run.Length > longest) (longest, wall) = (run.Length, run);
        }

        var turnDeg = Math.Abs(Math.Atan2(Pt.Cross(along, wall), Pt.Dot(along, wall))) * (180 / Math.PI) % 90;
        return Math.Min(turnDeg, 90 - turnDeg);
    }

    /// <summary>
    /// <b>What a zone's buildings measure</b>, each setting where enough of them were met to say it: the share of its
    /// probes that met a front, how far off the edge they stand and how widely, how turned, how alike, what they are drawn
    /// as, and the ground behind them built over and the bearing it is built on.
    /// </summary>
    /// <param name="ownM2">The ground the zone holds: its outline's, less the zones' inside it.</param>
    static List<(ZoneParam, float)> Measured(Tally zone, double ownM2, SurveyFootprints buildings, Pt[][] outlines)
    {
        var settings = new List<(ZoneParam, float)>();
        if (zone.Asked >= MeasuredLeast) settings.Add((ZoneParam.Frontage, (float)zone.Met / zone.Asked));

        if (zone.FrontM.Count >= MeasuredLeast)
        {
            zone.FrontM.Sort();
            settings.Add((ZoneParam.FrontM, (float)Quantile(zone.FrontM, 0.5)));
            settings.Add((ZoneParam.FrontSpreadM, (float)((Quantile(zone.FrontM, 0.75) - Quantile(zone.FrontM, 0.25)) * 0.5)));
        }

        if (zone.FrontRow.Count >= MeasuredLeast)
        {
            zone.SkewDeg.Sort();

            // A turn drawn evenly either way of square up to a skew is turned half the skew in the middle of them.
            settings.Add((ZoneParam.SkewDeg, (float)(2 * Quantile(zone.SkewDeg, 0.5))));
            if (zone.Pairs >= MeasuredLeast) settings.Add((ZoneParam.Variety, 1f - ((float)zone.Alike / zone.Pairs)));

            var looks = new int[ZoneParams.Looks];
            foreach (var building in zone.FrontRow) looks[(int)buildings.Look[building]]++;
            foreach (var (look, share) in Shares(looks)) settings.Add((ZoneParams.Of(look), share));
            settings.Add((ZoneParam.FootprintM2, (float)Mean(zone.FrontRow, outlines)));
        }

        var behind = zone.Standing.Where(building => !zone.FrontRow.Contains(building)).ToList();
        if (zone.Standing.Count >= MeasuredLeast && ownM2 > 0)
        {
            settings.Add((ZoneParam.Interior, (float)(behind.Sum(building => Math.Abs(Shape.SignedArea(outlines[building]))) / ownM2)));
            if (behind.Count >= MeasuredLeast)
            {
                settings.Add((ZoneParam.BehindM2, (float)Mean(behind, outlines)));
                if (Bearing(behind, outlines) is { } bearingDeg) settings.Add((ZoneParam.BearingDeg, (float)bearingDeg));
            }
        }

        return settings;
    }

    /// <summary>
    /// The ground some buildings cover on average — the mean and not the middle one, so that as many buildings of it
    /// cover what they did: a few big blocks among many sheds are most of the ground.
    /// </summary>
    static double Mean(IEnumerable<int> standing, Pt[][] outlines) => standing.Average(building => Math.Abs(Shape.SignedArea(outlines[building])));

    /// <summary>Each look's share of the counted, a look under <see cref="LookLeast"/> dropped and the rest weighed again.</summary>
    static IEnumerable<(BuildingLook Look, float Share)> Shares(int[] counted)
    {
        var total = counted.Sum();
        var kept = Enumerable.Range(0, counted.Length).Where(look => counted[look] >= LookLeast * total).ToArray();
        var keptTotal = kept.Sum(look => counted[look]);
        return keptTotal == 0 ? [] : kept.Select(look => ((BuildingLook)look, (float)counted[look] / keptTotal));
    }

    /// <summary>
    /// The bearing buildings share, in degrees east of north, where their longest walls agree on one to within a right
    /// angle strongly enough (<see cref="SharedBearingLeast"/>), else null.
    /// </summary>
    static double? Bearing(List<int> standing, Pt[][] outlines)
    {
        var (sumX, sumY, weight) = (0.0, 0.0, 0.0);
        foreach (var building in standing)
        {
            var outline = outlines[building];
            var (longest, wall) = (0.0, new Pt(1, 0));
            for (var at = 0; at < outline.Length; at++)
            {
                var run = outline[(at + 1) % outline.Length] - outline[at];
                if (run.Length > longest) (longest, wall) = (run.Length, run);
            }

            var fourTimes = 4 * Math.Atan2(wall.Y, wall.X);
            (sumX, sumY, weight) = (sumX + (Math.Cos(fourTimes) * longest), sumY + (Math.Sin(fourTimes) * longest), weight + longest);
        }

        if (weight <= 0 || Math.Sqrt((sumX * sumX) + (sumY * sumY)) / weight < SharedBearingLeast) return null;

        // An angle off east with y south, turned to a bearing off north, within a quarter turn.
        var eastRad = Math.Atan2(sumY, sumX) / 4;
        var bearingDeg = (90 + (eastRad * 180 / Math.PI)) % 90;
        return bearingDeg < 0 ? bearingDeg + 90 : bearingDeg;
    }

    /// <summary>What a residential land use is built as, by the looks of its front row and how built up it is.</summary>
    static ZoneKind Residential(Tally use, double areaM2, SurveyFootprints buildings, Pt[][] outlines)
    {
        if (use.FrontRow.Count < MeasuredLeast) return ZoneKind.Residential;

        var share = (BuildingLook look) => use.FrontRow.Count(building => buildings.Look[building] == look) / (double)use.FrontRow.Count;
        if (share(BuildingLook.Tower) >= TowerShare) return ZoneKind.HighRise;
        if (share(BuildingLook.House) >= HouseShare) return ZoneKind.Suburb;

        var frontage = use.Asked >= MeasuredLeast ? (double)use.Met / use.Asked : 0;
        var coverage = use.Standing.Sum(building => Math.Abs(Shape.SignedArea(outlines[building]))) / areaM2;
        return frontage >= OldFrontage && coverage >= OldCoverage ? ZoneKind.OldTown : ZoneKind.Residential;
    }

    static double Quantile(List<double> sorted, double share) => sorted[Math.Clamp((int)Math.Round(share * (sorted.Count - 1)), 0, sorted.Count - 1)];

    static bool Encloses(ReadOnlySpan<Vector2> ring, Pt at)
    {
        var inside = false;
        for (int a = 0, b = ring.Length - 1; a < ring.Length; b = a++)
        {
            if ((ring[a].Y > at.Y) != (ring[b].Y > at.Y) && at.X < ((ring[b].X - ring[a].X) * (at.Y - ring[a].Y) / (ring[b].Y - ring[a].Y)) + ring[a].X) inside = !inside;
        }

        return inside;
    }

    static int BuildingCell(double atM) => (int)Math.Floor(atM / BuildingCellM);

    /// <summary>Every land use of the zones layer of a kind that says what is built or grows on it.</summary>
    static List<Zone> Read(string folder, Plane plane, Func<Pt[], bool> keeps)
    {
        var uses = new List<Zone>();
        foreach (var record in Layers.Read<ZoneRecord>(folder, "zones")?.Items ?? [])
        {
            if (record.Outer is null || KindOf(record.Kind, record.Sub) is not { } kind) continue;

            var inners = (record.Inner ?? []).Select(plane.Line).Where(ring => ring.Length >= 3).ToArray();
            foreach (var outer in record.Outer.Select(plane.Line))
            {
                if (outer.Length < 3 || !keeps(outer)) continue;

                uses.Add(new Zone([outer, .. inners.Where(inner => Shape.Inside(inner[0], [outer]))], kind));
            }
        }

        return uses;
    }

    /// <summary>
    /// <b>What kind of zone a land use is</b>, by OSM's own values, or null for one that says nothing of what is built or
    /// grows there. A residential one is <see cref="ZoneKind.Residential"/> until what is built in it says which
    /// (<see cref="Residential"/>).
    /// </summary>
    static ZoneKind? KindOf(string kind, string? sub) => kind switch
    {
        "residential" => ZoneKind.Residential,
        "garages" => ZoneKind.Garages,
        "industrial" or "port" or "railway" or "military" => ZoneKind.Industrial,
        "education" or "health" or "religious" or "civic" or "bus_station" or "ferry_terminal" => ZoneKind.Civic,
        "commercial" or "retail" or "fuel" or "car_wash" or "market" => ZoneKind.Commercial,
        "parking" or "parking_space" or "charging_station" => ZoneKind.Parking,
        "leisure" when sub is "sports_centre" or "stadium" => ZoneKind.Civic,
        "green" or "leisure" or "cemetery" => ZoneKind.Park,
        "nature" when sub is "heath" or "grassland" => ZoneKind.Park,
        "nature" => ZoneKind.Wild,
        "agriculture" or "allotments" => ZoneKind.Farmland,
        "water" or "beach" or "square" or "pier" or "construction" or "waste_land" or "airport" or "land" or "bridge" => ZoneKind.Open,
        _ => null,
    };

    static int Cell(double atM) => (int)Math.Floor(atM / CellM);

    static Pt[] Points(ReadOnlySpan<Vector2> ring)
    {
        var points = new Pt[ring.Length];
        for (var at = 0; at < ring.Length; at++) points[at] = new Pt(ring[at].X, ring[at].Y);
        return points;
    }

    static Vector2[] Vectors(Pt[] ring) => [.. ring.Select(point => new Vector2((float)point.X, (float)point.Y))];

    /// <summary>A ring thinned by Douglas–Peucker to within <see cref="ToleranceM"/>, without the repeat of its first point.</summary>
    static Pt[] Thinned(Pt[] ring)
    {
        var count = ring.Length > 1 && ring[0] == ring[^1] ? ring.Length - 1 : ring.Length;
        if (count < 3) return [];

        var keep = new bool[count + 1];
        var far = 0;
        for (var at = 1; at < count; at++)
        {
            if ((ring[at] - ring[0]).Length > (ring[far] - ring[0]).Length) far = at;
        }

        (keep[0], keep[far]) = (true, true);
        Mark(0, far);
        Mark(far, count);

        var kept = new List<Pt>();
        for (var at = 0; at < count; at++)
        {
            if (keep[at]) kept.Add(ring[at]);
        }

        return [.. kept];

        // The point furthest off the chord between two kept ones, kept where it is further than the tolerance; the last
        // is the first again.
        void Mark(int from, int to)
        {
            var (a, b) = (ring[from], ring[to % count]);
            var (furthest, furthestM) = (-1, ToleranceM);
            for (var at = from + 1; at < to; at++)
            {
                var offM = Shape.Nearest(ring[at], [a, b]).OffM;
                if (offM <= furthestM) continue;

                (furthest, furthestM) = (at, offM);
            }

            if (furthest < 0) return;

            keep[furthest] = true;
            Mark(from, furthest);
            Mark(furthest, to);
        }
    }
}
