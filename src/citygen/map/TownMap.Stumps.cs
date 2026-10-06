using System.Numerics;
using TrafficSimulation.CityGen.Traced;

namespace TrafficSimulation.CityGen.Map;

/// <summary>
/// <b>A road that stops: from a dead end back to the first place three ways meet</b> (<see cref="TownMap.Stumps"/>),
/// how it stands against the buildings — whether its dead end is inside a footprint, and how many of its metres are —
/// and whether every road it runs along is a single lane.
/// </summary>
/// <param name="Cuts">
/// What dropping it takes off each road it runs along: the road, the index of the first point kept from its start, or
/// the last kept from its end — a road it runs the whole of keeping one point, which is none.
/// </param>
internal readonly record struct Stump(
    Vector2D EndM, float LengthM, float InsideM, bool EndsInside, bool SingleLane, (int Road, int Kept, bool FromStart)[] Cuts);

internal sealed partial record TownMap
{
    /// <summary>How finely a stump is read for the buildings it stands in.</summary>
    const float StumpStepM = 1f;

    /// <summary>How big a square of the map the footprints are filed under, to be looked up by the place.</summary>
    const float FootprintCellM = 64f;

    /// <summary>
    /// <b>Every stump on the map</b>: each dead end on it — a point one road's end and nothing else passes, a road's last
    /// point past the frame being where it runs off the map instead — walked back along its roads through every place
    /// only two ways run on into each other, to the first where three meet or to another dead end, read against the
    /// buildings' footprints (a courtyard being no building), which the map does not hold and the caller reads off the
    /// survey's layers.
    /// </summary>
    public List<Stump> Stumps(SurveyFootprints buildings)
    {
        var arms = new int[PointM.Length];
        var ending = new Dictionary<int, List<(int Road, bool AtStart)>>();
        for (var road = 0; road < Roads.Length; road++)
        {
            var points = Roads[road].Points;
            for (var at = 0; at < points.Length; at++) arms[points[at]] += at == 0 || at == points.Length - 1 ? 1 : 2;

            Ends(points[0]).Add((road, true));
            Ends(points[^1]).Add((road, false));
        }

        var footprints = new FootprintCells(buildings);
        var stumps = new List<Stump>();
        for (var road = 0; road < Roads.Length; road++)
        {
            foreach (var atStart in (ReadOnlySpan<bool>)[true, false])
            {
                var points = Roads[road].Points;
                var deadEnd = atStart ? points[0] : points[^1];
                if (arms[deadEnd] == 1 && OnTheMap(PointM[deadEnd])) stumps.Add(Walked(road, atStart, deadEnd));
            }
        }

        return stumps;

        List<(int Road, bool AtStart)> Ends(int point)
        {
            if (!ending.TryGetValue(point, out var ends)) ending[point] = ends = [];
            return ends;
        }

        Stump Walked(int road, bool atStart, int deadEnd)
        {
            var cuts = new List<(int Road, int Kept, bool FromStart)>();
            var (lengthM, insideM) = (0f, 0f);
            var endsInside = footprints.Holds(Flat(PointM[deadEnd]));
            var singleLane = true;
            while (true)
            {
                singleLane &= Roads[road].Lanes == 1;
                var points = Roads[road].Points;
                var (step, at) = atStart ? (1, 0) : (-1, points.Length - 1);
                while (true)
                {
                    var next = at + step;
                    Read(PointM[points[at]], PointM[points[next]]);
                    at = next;
                    if (arms[points[at]] != 2 || at == 0 || at == points.Length - 1) break;
                }

                cuts.Add((road, at, atStart));
                var reached = points[at];
                var endOfRoad = at == 0 || at == points.Length - 1;
                if (arms[reached] != 2 || !endOfRoad) break;

                // Two ways run on into each other here, and the stump runs on along the other.
                var onward = ending[reached].FindIndex(end => end.Road != road);
                if (onward < 0) break;

                (road, atStart) = ending[reached][onward];
            }

            return new Stump(PointM[deadEnd], lengthM, insideM, endsInside, singleLane, [.. cuts]);

            void Read(Vector2D fromM, Vector2D toM)
            {
                var (a, b) = (Flat(fromM), Flat(toM));
                var runM = Vector2.Distance(a, b);
                var steps = Math.Max(1, (int)MathF.Ceiling(runM / StumpStepM));
                for (var step = 0; step < steps; step++)
                {
                    if (footprints.Holds(Vector2.Lerp(a, b, (step + 0.5f) / steps))) insideM += runM / steps;
                }

                lengthM += runM;
            }
        }

        static Vector2 Flat(Vector2D atM) => new((float)atM.X, (float)atM.Y);

        bool OnTheMap(Vector2D atM) => atM.X >= 0 && atM.Y >= 0 && atM.X <= Frame.WidthM && atM.Y <= Frame.HeightM;
    }

    /// <summary>
    /// <b>This map without the stumps given</b>: each road cut back as the stump takes it, and a road left with fewer
    /// than two points gone.
    /// </summary>
    public TownMap Without(IEnumerable<Stump> stumps)
    {
        var (keptFrom, keptTo) = (new int[Roads.Length], new int[Roads.Length]);
        for (var road = 0; road < Roads.Length; road++) keptTo[road] = Roads[road].Points.Length - 1;

        foreach (var stump in stumps)
        {
            foreach (var (road, upTo, fromStart) in stump.Cuts)
            {
                if (fromStart) keptFrom[road] = Math.Max(keptFrom[road], upTo);
                else keptTo[road] = Math.Min(keptTo[road], upTo);
            }
        }

        var renumbered = new int[PointM.Length];
        Array.Fill(renumbered, -1);
        var kept = new List<Vector2D>();
        var roads = new List<TracedRoad>(Roads.Length);
        for (var road = 0; road < Roads.Length; road++)
        {
            if (keptTo[road] - keptFrom[road] < 1) continue;

            roads.Add(Roads[road] with { Points = Renumbered(Roads[road].Points[keptFrom[road]..(keptTo[road] + 1)]) });
        }

        var coast = Coast.Select(Renumbered).ToArray();
        return this with { PointM = [.. kept], Roads = [.. roads], Coast = coast };

        int[] Renumbered(int[] points)
        {
            var into = new int[points.Length];
            for (var at = 0; at < points.Length; at++)
            {
                ref var point = ref renumbered[points[at]];
                if (point < 0)
                {
                    point = kept.Count;
                    kept.Add(PointM[points[at]]);
                }

                into[at] = point;
            }

            return into;
        }
    }

    /// <summary>The footprints filed by the squares their outlines' boxes cover, to be asked whether a place is inside one.</summary>
    sealed class FootprintCells
    {
        readonly SurveyFootprints _footprints;
        readonly Dictionary<(int X, int Y), List<int>> _cells = [];

        public FootprintCells(SurveyFootprints footprints)
        {
            _footprints = footprints;
            for (var footprint = 0; footprint < footprints.Count; footprint++)
            {
                var (leastM, mostM) = (new Vector2(float.MaxValue), new Vector2(float.MinValue));
                foreach (var pointM in footprints.RingOf(footprints.RingOffsets[footprint])) (leastM, mostM) = (Vector2.Min(leastM, pointM), Vector2.Max(mostM, pointM));

                var (least, most) = (Cell(leastM), Cell(mostM));
                for (var x = least.X; x <= most.X; x++)
                {
                    for (var y = least.Y; y <= most.Y; y++)
                    {
                        if (!_cells.TryGetValue((x, y), out var here)) _cells[(x, y)] = here = [];
                        here.Add(footprint);
                    }
                }
            }
        }

        /// <summary>Whether a place stands inside a building: inside its outline and inside none of its courtyards.</summary>
        public bool Holds(Vector2 atM)
        {
            if (!_cells.TryGetValue(Cell(atM), out var here)) return false;

            foreach (var footprint in here)
            {
                var inside = false;
                for (var ring = _footprints.RingOffsets[footprint]; ring < _footprints.RingOffsets[footprint + 1]; ring++)
                {
                    inside ^= Encloses(_footprints.RingOf(ring), atM);
                }

                if (inside) return true;
            }

            return false;
        }

        static (int X, int Y) Cell(Vector2 atM) => ((int)MathF.Floor(atM.X / FootprintCellM), (int)MathF.Floor(atM.Y / FootprintCellM));

        static bool Encloses(ReadOnlySpan<Vector2> ring, Vector2 atM)
        {
            var inside = false;
            for (int at = 0, before = ring.Length - 1; at < ring.Length; before = at++)
            {
                var (a, b) = (ring[at], ring[before]);
                if ((a.Y > atM.Y) != (b.Y > atM.Y) && atM.X < a.X + ((atM.Y - a.Y) / (b.Y - a.Y) * (b.X - a.X))) inside = !inside;
            }

            return inside;
        }
    }
}
