using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// <b>A shell's line cut to the map</b> (GEN-2b): the closed rings of what of it stands inside the rectangle from the
/// origin to the map's far corner, a ring running off the map closed along the map's edge between where it leaves and
/// where the shell comes back on.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read as <see cref="ShellFill"/> reads a shell</b>: rings of points, each walked with its ground on the walker's
/// right, so an outside ring encloses a positive area and a hole a negative one. The map walked from the origin along
/// x first encloses a positive area too, so the stretch of edge between a run leaving and the next one entering is
/// walked that way round, and the ground stays on the right of every ring this hands back.
/// </para>
/// <para>
/// <b>Every place it puts on the edge is on it exactly</b>, so a piece lying along the edge is known by its two ends
/// (<see cref="AlongTheEdge"/>) — the stretch a kerb is not struck along, the ground running on past it.
/// </para>
/// <para>
/// A ring wholly inside is handed back as it was and one wholly outside is dropped, so a shell that never leaves the
/// map comes back unchanged. <b>A ring round the whole map that never crosses its edge is dropped as well</b>: no
/// ground this town lays encloses the map.
/// </para>
/// </remarks>
internal static class MapCut
{
    public static Vector2[][] Rings(ReadOnlySpan<Vector2[]> rings, Vector2 sizeM)
    {
        var kept = new List<Vector2[]>(rings.Length);
        var runs = new List<List<Vector2>>();
        foreach (var ring in rings)
        {
            var outside = -1;
            for (var at = 0; at < ring.Length && outside < 0; at++)
            {
                if (!Holds(ring[at], sizeM)) outside = at;
            }

            if (outside < 0) kept.Add(ring);
            else Runs(ring, outside, sizeM, runs);
        }

        if (runs.Count > 0) Joined(runs, sizeM, kept);
        return [.. kept];
    }

    /// <summary>Whether a piece lies along the map's edge, both its ends on one side of the map.</summary>
    public static bool AlongTheEdge(Vector2 fromM, Vector2 toM, Vector2 sizeM) =>
        (fromM.X == 0f && toM.X == 0f) || (fromM.Y == 0f && toM.Y == 0f)
        || (fromM.X == sizeM.X && toM.X == sizeM.X) || (fromM.Y == sizeM.Y && toM.Y == sizeM.Y);

    static bool Holds(Vector2 atM, Vector2 sizeM) => atM.X >= 0f && atM.Y >= 0f && atM.X <= sizeM.X && atM.Y <= sizeM.Y;

    /// <summary>
    /// <b>Each stretch of one ring inside the map</b>, from where it comes on to where it goes off, walked from a point
    /// of it off the map so that no stretch is left open at the end.
    /// </summary>
    static void Runs(Vector2[] ring, int outside, Vector2 sizeM, List<List<Vector2>> runs)
    {
        List<Vector2>? run = null;
        for (var step = 0; step < ring.Length; step++)
        {
            var fromM = ring[(outside + step) % ring.Length];
            var toM = ring[(outside + step + 1) % ring.Length];
            if (run is not null && Holds(toM, sizeM))
            {
                Join(run, toM);
                continue;
            }

            if (!Crossing(fromM, toM, sizeM, out var enters, out var leaves)) continue;

            run ??= [OnTheEdge(Vector2.Lerp(fromM, toM, enters), sizeM)];
            if (leaves >= 1f)
            {
                Join(run, toM);
                continue;
            }

            Join(run, OnTheEdge(Vector2.Lerp(fromM, toM, leaves), sizeM));
            if (run.Count >= 2) runs.Add(run);
            run = null;
        }

        static void Join(List<Vector2> run, Vector2 atM)
        {
            if (run[^1] != atM) run.Add(atM);
        }
    }

    /// <summary>
    /// <b>The runs closed into rings along the map's edge</b>: from where each leaves, on round the edge the way the
    /// map's own ring runs to the nearest place one comes back on, and on through that one — Weiler and Atherton's
    /// walk, with the map for the window.
    /// </summary>
    static void Joined(List<List<Vector2>> runs, Vector2 sizeM, List<Vector2[]> into)
    {
        var perimeterM = 2.0 * (sizeM.X + sizeM.Y);
        var entersAt = new double[runs.Count];
        for (var run = 0; run < runs.Count; run++) entersAt[run] = Along(runs[run][0], sizeM);

        ReadOnlySpan<(double AtM, Vector2 CornerM)> corners =
        [
            (0.0, Vector2.Zero), (sizeM.X, new Vector2(sizeM.X, 0f)), (sizeM.X + sizeM.Y, sizeM),
            ((2.0 * sizeM.X) + sizeM.Y, new Vector2(0f, sizeM.Y)),
        ];

        var used = new bool[runs.Count];
        var passed = new List<(double AheadM, Vector2 CornerM)>(corners.Length);
        for (var first = 0; first < runs.Count; first++)
        {
            if (used[first]) continue;

            var ring = new List<Vector2>();
            var run = first;
            while (!used[run])
            {
                used[run] = true;
                ring.AddRange(runs[run]);

                var leavesAt = Along(runs[run][^1], sizeM);
                var (next, nextM) = (first, double.MaxValue);
                for (var other = 0; other < runs.Count; other++)
                {
                    var aheadM = Ahead(leavesAt, entersAt[other]);
                    if (aheadM < nextM) (next, nextM) = (other, aheadM);
                }

                passed.Clear();
                foreach (var (atM, cornerM) in corners)
                {
                    var aheadM = Ahead(leavesAt, atM);
                    if (aheadM > 0.0 && aheadM < nextM) passed.Add((aheadM, cornerM));
                }

                passed.Sort((one, other) => one.AheadM.CompareTo(other.AheadM));
                foreach (var (_, cornerM) in passed) ring.Add(cornerM);
                run = next;
            }

            if (ring.Count >= 3) into.Add([.. ring]);
        }

        double Ahead(double fromM, double toM) => (((toM - fromM) % perimeterM) + perimeterM) % perimeterM;
    }

    /// <summary>
    /// How far round the map's edge a place on it stands, walked from the origin along x first — the way the map's
    /// own ring runs — read off the side it stands nearest.
    /// </summary>
    static double Along(Vector2 atM, Vector2 sizeM)
    {
        var (x, y, w, h) = ((double)atM.X, (double)atM.Y, (double)sizeM.X, (double)sizeM.Y);
        var (offM, alongM) = (y, x);
        if (w - x < offM) (offM, alongM) = (w - x, w + y);
        if (h - y < offM) (offM, alongM) = (h - y, w + h + (w - x));
        if (x < offM) alongM = (2.0 * w) + h + (h - y);
        return alongM;
    }

    /// <summary>A place a run comes on or goes off at, put on the side of the map it stands nearest exactly.</summary>
    static Vector2 OnTheEdge(Vector2 atM, Vector2 sizeM)
    {
        var (x, y) = (Math.Clamp(atM.X, 0f, sizeM.X), Math.Clamp(atM.Y, 0f, sizeM.Y));
        var offM = MathF.Min(MathF.Min(x, sizeM.X - x), MathF.Min(y, sizeM.Y - y));
        if (x == offM) return new Vector2(0f, y);
        if (sizeM.X - x == offM) return new Vector2(sizeM.X, y);
        return y == offM ? new Vector2(x, 0f) : new Vector2(x, sizeM.Y);
    }

    /// <summary>
    /// Where a straight comes on to the map and goes off it, as shares of its length (Liang–Barsky), or false where no
    /// share of it stands on the map.
    /// </summary>
    static bool Crossing(Vector2 fromM, Vector2 toM, Vector2 sizeM, out float enters, out float leaves)
    {
        var (low, high) = (0.0, 1.0);
        var (acrossM, downM) = ((double)toM.X - fromM.X, (double)toM.Y - fromM.Y);
        ReadOnlySpan<(double Toward, double RoomM)> sides =
        [
            (-acrossM, fromM.X), (acrossM, (double)sizeM.X - fromM.X), (-downM, fromM.Y), (downM, (double)sizeM.Y - fromM.Y),
        ];
        foreach (var (toward, roomM) in sides)
        {
            if (toward == 0.0)
            {
                if (roomM < 0.0) low = 2.0;

                continue;
            }

            var share = roomM / toward;
            if (toward < 0.0) low = Math.Max(low, share);
            else high = Math.Min(high, share);
        }

        (enters, leaves) = ((float)low, (float)high);
        return low <= high;
    }
}
