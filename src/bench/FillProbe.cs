using System.Diagnostics;
using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.App.Render;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>A town's driven ground cut into triangles</b> (<see cref="ShellFill"/>), read on the four things that
/// say whether the cut is the right one: how long the cutting takes, how many triangles it comes to, what
/// shape those triangles are, and how much of the shell it lost.
/// </summary>
/// <remarks>
/// <para>
/// <b>The count is what is being read and the rest is beside it.</b> Every triangulation of the same corners
/// is <c>n − 2</c> triangles — by ears, by monotone pieces, by Delaunay, by any partition into convex pieces
/// at all — so <b>no choice of cutting shows up in that figure</b> and the only thing that does is how many
/// corners the boundary was read as. That is what the sag and the thrift are.
/// </para>
/// <para>
/// <b>Read the count against the loss and never on its own.</b> Corners can always be dropped and the count
/// always falls with them; what says whether a reading is a better answer or just a coarser one is how much
/// of the shell the boundary it lays stopped covering. Two cuts are only comparable at about the same loss.
/// </para>
/// <para>
/// <b>Shape is the secondary reading here and says so.</b> A ground vertex carries its texture coordinate as
/// its own position over a period, so the coordinate is affine in the position and a barycentric across any
/// triangle whatever is exact: a sliver textures the same as an equilateral. What the shape reading is for is
/// the mesh being sane to hold — what a corner's edge count and a longest side do to the fill and to
/// everything that reads it — and not what the ground looks like.
/// </para>
/// </remarks>
internal static class FillProbe
{
    /// <summary>How many times each way is cut, the fastest of them being the reading.</summary>
    /// <remarks>
    /// The fastest rather than the mean: a slower run measured something else on the machine, and the
    /// question here is what the cutting costs rather than what the box was doing at the time.
    /// </remarks>
    const int Runs = 3;

    /// <summary>How many places each piece of the true boundary is measured against the cut's own.</summary>
    const int Sampled = 32;

    /// <summary>The cell the cut's own edges are binned at for that measurement.</summary>
    const float CellM = 4f;

    /// <summary>How many cells out the search for a near edge widens before the place counts as missed.</summary>
    /// <remarks>
    /// <b>A place with no cut edge anywhere near it is the reading that matters most, so it is counted and
    /// never scored.</b> A search that gave up and called the distance nothing would report a fill that had
    /// stopped covering a whole ring as the most faithful one in the table.
    /// </remarks>
    const int MostReach = 32;

    /// <summary>Under this a joint between two of a ring's pieces counts as shut.</summary>
    const float ShutM = 0.001f;

    /// <summary>Below this a triangle is a sliver rather than a triangle; an equilateral one is one.</summary>
    const double Sliver = 0.1;

    /// <summary>The angle a triangle's smallest is counted against, in degrees.</summary>
    const double SharpDeg = 30.0;

    public static void Run(string map, SimConfig config)
    {
        var plan = Maps.Plan(map, config);
        var shell = plan.Paving(config).Perimeter(config);

        var boundaryM = 0f;
        var pieces = 0;
        var trueM2 = 0.0;
        var outers = 0;
        var outerM2 = 0.0;
        var holes = 0;
        var holesM2 = 0.0;
        foreach (var ring in shell.Chains)
        {
            boundaryM += Spline.TotalLengthM(ring);
            pieces += ring.Length;

            var ringM2 = Spline.EnclosedM2(ring);
            trueM2 += ringM2;
            if (ringM2 >= 0.0)
            {
                outers++;
                outerM2 += ringM2;
            }
            else
            {
                holes++;
                holesM2 -= ringM2;
            }
        }

        Console.WriteLine($"fill — {plan.Name}, seed {plan.Seed}");
        Console.WriteLine();
        Console.WriteLine(
            $"  boundary     {shell.Chains.Length} rings, {boundaryM / 1000f:F1} km, {pieces} pieces, " +
            $"{shell.Loose.Length} runs left open by the merge");
        Console.WriteLine(
            $"  rings        {outers} outer over {outerM2:N0} m², {holes} holes cutting {holesM2:N0} m² back out");
        Console.WriteLine($"  encloses     {trueM2:N0} m² read off the arcs themselves");

        var (open, worstJointM) = Joints(shell);
        Console.WriteLine(
            $"  joints       {open} of {pieces} left open by the merge, worst {worstJointM:F3} m, " +
            $"each closed by the straight a fill draws across it");
        var kerbM = config.Road.KerbWidthM;
        Console.WriteLine(
            $"  line         sag {GroundMesh.ChordSagM:F3} m, " +
            $"{GroundMesh.ChordTurnRad * 180f / MathF.PI:F0}° a chord, unthinned — what the kerb is struck from");
        Console.WriteLine(
            $"  cut          that line thinned by {kerbM * 0.5f * GroundMesh.HiddenShare:F3} m, " +
            $"{GroundMesh.HiddenShare:P0} of the {kerbM * 0.5f:F3} m the kerb hides it under");

        Written(Read(shell, kerbM), trueM2);
    }

    /// <summary>The whole reading, in the order the four questions are asked.</summary>
    static void Written(in Reading reading, double trueM2)
    {
        var netM2 = trueM2 - reading.FilledM2;

        // The zoom at which the furthest stray is one pixel, which is what turns a tolerance into a
        // decision: over it the boundary reads as the shape, under it the scallops are on the glass.
        var onePixelAt = reading.StrayM > 0f ? $"{1f / reading.StrayM:F0} px/m" : "—";

        Console.WriteLine();
        Console.WriteLine(
            $"  built        {reading.Corners} corners, {reading.Triangles} triangles, {reading.Ms:F1} ms, " +
            $"{(reading.Ms > 0.0 ? reading.Triangles / reading.Ms : 0.0):F0} tri/ms");
        Console.WriteLine(
            $"  shaped       quality {reading.Quality:F3} of 1.0 at 60-60-60, worst {reading.LeastQuality:F3}, " +
            $"smallest angle {reading.MeanLeastDeg:F1}° mean and {reading.LeastDeg:F2}° worst");
        Console.WriteLine(
            $"               {Share(reading.Sharp, reading.Triangles)} under {SharpDeg:F0}°, " +
            $"{Share(reading.Slivers, reading.Triangles)} slivers under {Sliver:F1}, " +
            $"{reading.MostEdges} edges at the busiest corner, longest side {reading.LongestM:F1} m");
        Console.WriteLine(
            $"  lost         {reading.FilledM2:N0} m² filled, net {netM2:F1} m² ({Portion(netM2, trueM2)}), " +
            $"gone {reading.GoneM2:F1} m² ({Portion(reading.GoneM2, trueM2)})");
        Console.WriteLine(
            $"               stray {reading.StrayM:F3} m worst and {reading.MeanStrayM:F3} m mean, " +
            $"one pixel at {onePixelAt}, {reading.MissedM:F1} m missed, {reading.BackwardsM2:F1} m² backwards");
    }

    /// <summary>
    /// <b>How many of the shell's joints are open</b>, and by how much at the worst of them.
    /// </summary>
    /// <remarks>
    /// <b>It belongs beside the loss table because every one of them is a term in it.</b> A fill closes a
    /// joint with the straight it draws from one piece's last station to the next piece's first, and so
    /// covers ground the pieces themselves do not bound. That ground is real and is the fill's to cover —
    /// what it must not do is go unaccounted for, which is what it did while the area the rows are read
    /// against was a sum over pieces alone. The walk leaves a handful now
    /// (<see cref="ArcRings.Tightened"/>), and this is where a town that grew more would say so.
    /// </remarks>
    static (int Open, float WorstM) Joints(BandShell shell)
    {
        var open = 0;
        var worstM = 0f;

        foreach (var ring in shell.Chains)
        {
            for (var at = 0; at < ring.Length; at++)
            {
                var gapM = Vector2.Distance(ring[at].EndM, ring[(at + 1) % ring.Length].StartM);
                if (gapM > ShutM) open++;

                worstM = MathF.Max(worstM, gapM);
            }
        }

        return (open, worstM);
    }

    /// <summary>What the cut came to, on every one of the four readings.</summary>
    readonly record struct Reading(
        int Corners,
        int Triangles,
        double Ms,
        double Quality,
        double LeastQuality,
        double MeanLeastDeg,
        double LeastDeg,
        int Sharp,
        int Slivers,
        int MostEdges,
        float LongestM,
        double FilledM2,

        /// <summary>The ground covered by triangles laid inside out, which is ground covered twice.</summary>
        double BackwardsM2,
        double GoneM2,
        float StrayM,
        float MeanStrayM,

        /// <summary>How much of the true boundary had no edge of the cut anywhere near it.</summary>
        float MissedM);

    static Reading Read(BandShell shell, float kerbM)
    {
        // The town's own two-stage read (GroundMesh) and never a cut of this probe's own, so what is
        // measured here is what is drawn. Warmed once and then taken at its fastest: the first cut of a run
        // pays for every list the fill grows and every page the allocator hands it, which is a cost of being
        // first and not of the cut.
        var (pointsM, triangles) = ShellFill.Of(
            GroundMesh.Filled(GroundMesh.Line(shell.Chains), kerbM));

        var ms = double.MaxValue;
        for (var run = 0; run < Runs; run++)
        {
            var clock = Stopwatch.StartNew();
            (pointsM, triangles) = ShellFill.Of(
                GroundMesh.Filled(GroundMesh.Line(shell.Chains), kerbM));
            clock.Stop();
            ms = Math.Min(ms, clock.Elapsed.TotalMilliseconds);
        }

        var cut = triangles.Length / 3;
        var quality = 0.0;
        var leastQuality = double.MaxValue;
        var meanLeastDeg = 0.0;
        var leastDeg = double.MaxValue;
        var sharp = 0;
        var slivers = 0;
        var longestM = 0f;
        var filledM2 = 0.0;
        var backwardsM2 = 0.0;

        for (var at = 0; at + 2 < triangles.Length; at += 3)
        {
            var a = pointsM[triangles[at]];
            var b = pointsM[triangles[at + 1]];
            var c = pointsM[triangles[at + 2]];

            var turnM2 = TurnM2(a, b, c);
            filledM2 += turnM2;
            if (turnM2 < 0.0) backwardsM2 -= turnM2;
            longestM = MathF.Max(longestM, LongestSideM(a, b, c));

            var shapely = Quality(a, b, c);
            quality += shapely;
            leastQuality = Math.Min(leastQuality, shapely);
            if (shapely < Sliver) slivers++;

            var smallestDeg = Smallest(a, b, c) * 180.0 / Math.PI;
            meanLeastDeg += smallestDeg;
            leastDeg = Math.Min(leastDeg, smallestDeg);
            if (smallestDeg < SharpDeg) sharp++;
        }

        var mostEdges = 0;
        foreach (var edges in EdgesAtCorners(pointsM.Length, triangles)) mostEdges = Math.Max(mostEdges, edges);

        var (strayM, meanStrayM, goneM2, missedM) = Strayed(shell, pointsM, triangles);

        return new Reading(
            pointsM.Length, cut, ms,
            cut == 0 ? 0.0 : quality / cut, cut == 0 ? 0.0 : leastQuality,
            cut == 0 ? 0.0 : meanLeastDeg / cut, cut == 0 ? 0.0 : leastDeg,
            sharp, slivers, mostEdges, longestM,
            filledM2, backwardsM2, goneM2, strayM, meanStrayM, missedM);
    }

    /// <summary>
    /// <b>How far the cut's own boundary strays from the boundary it is of</b>: the furthest and the mean
    /// over the shell's arcs, and the ground between the two boundaries that the mean integrates to.
    /// </summary>
    /// <remarks>
    /// <b>Measured and not derived from whatever tolerance was asked for.</b> A sag says what one chord does
    /// to one arc; a thinning says what one corner does to its own neighbours, and both compound down a run
    /// of them. The figure a reader wants is what came out, and the two knobs in these tables do not mean the
    /// same thing to begin with.
    /// </remarks>
    static (float StrayM, float MeanStrayM, double GoneM2, float MissedM) Strayed(
        BandShell shell, Vector2[] pointsM, int[] triangles)
    {
        var grid = new Dictionary<(int X, int Y), List<(Vector2 From, Vector2 To)>>();
        foreach (var (one, other) in Boundary(triangles))
        {
            var fromM = pointsM[one];
            var toM = pointsM[other];
            var least = Cell(Vector2.Min(fromM, toM));
            var most = Cell(Vector2.Max(fromM, toM));

            for (var x = least.X; x <= most.X; x++)
            {
                for (var y = least.Y; y <= most.Y; y++)
                {
                    if (!grid.TryGetValue((x, y), out var holding)) grid[(x, y)] = holding = [];
                    holding.Add((fromM, toM));
                }
            }
        }

        var strayM = 0f;
        var goneM2 = 0.0;
        var walkedM = 0.0;
        var missedM = 0.0;

        foreach (var ring in shell.Chains)
        {
            foreach (var arc in ring)
            {
                var stepM = MathF.Abs(arc.LengthM) / Sampled;
                for (var step = 0; step < Sampled; step++)
                {
                    // The middle of each step rather than its start, so the stations are not every one of
                    // them a chord end where a cut boundary passes through the true one by construction.
                    var onM = arc.PointAtM(arc.LengthM * (step + 0.5f) / Sampled);
                    var offM = OffM(grid, onM);
                    walkedM += stepM;

                    if (offM == float.MaxValue)
                    {
                        missedM += stepM;
                        continue;
                    }

                    strayM = MathF.Max(strayM, offM);
                    goneM2 += offM * stepM;
                }
            }
        }

        return (strayM, walkedM > 0.0 ? (float)(goneM2 / walkedM) : 0f, goneM2, (float)missedM);
    }

    /// <summary>
    /// How far one place stands off the nearest edge of the cut, searching outward until the ring searched
    /// is wider than the answer it found — or <see cref="float.MaxValue"/> where there is no edge to find.
    /// </summary>
    /// <remarks>
    /// <b>The widening is what makes the answer the nearest one and not merely a near one.</b> An edge is
    /// binned by the cells its own box covers, so a hit found within <c>r</c> cells is only the nearest
    /// once <c>r</c> cells is further than that hit: until then a closer edge can still be sitting one ring
    /// further out.
    /// </remarks>
    static float OffM(Dictionary<(int X, int Y), List<(Vector2 From, Vector2 To)>> grid, Vector2 pointM)
    {
        var at = Cell(pointM);

        for (var reach = 1; reach <= MostReach; reach++)
        {
            var offM = Nearest(grid, pointM, at, reach);
            if (offM <= (reach - 1) * CellM || (reach == MostReach && offM < float.MaxValue)) return offM;
        }

        return float.MaxValue;
    }

    static float Nearest(
        Dictionary<(int X, int Y), List<(Vector2 From, Vector2 To)>> grid, Vector2 pointM, (int X, int Y) at,
        int reach)
    {
        var offM = float.MaxValue;
        for (var x = at.X - reach; x <= at.X + reach; x++)
        {
            for (var y = at.Y - reach; y <= at.Y + reach; y++)
            {
                if (!grid.TryGetValue((x, y), out var holding)) continue;

                foreach (var (fromM, toM) in holding) offM = MathF.Min(offM, OffSegmentM(fromM, toM, pointM));
            }
        }

        return offM;
    }

    static float OffSegmentM(Vector2 fromM, Vector2 toM, Vector2 pointM)
    {
        var runM = toM - fromM;
        var lengthM2 = runM.LengthSquared();
        if (lengthM2 <= 0f) return Vector2.Distance(fromM, pointM);

        var along = Math.Clamp(Vector2.Dot(pointM - fromM, runM) / lengthM2, 0f, 1f);
        return Vector2.Distance(fromM + (runM * along), pointM);
    }

    static (int X, int Y) Cell(Vector2 pointM) =>
        ((int)MathF.Floor(pointM.X / CellM), (int)MathF.Floor(pointM.Y / CellM));

    /// <summary>The edges only one triangle uses, which is the outline of whatever the fill covered.</summary>
    static List<(int One, int Other)> Boundary(int[] triangles)
    {
        var times = new Dictionary<(int, int), int>(triangles.Length);
        for (var at = 0; at + 2 < triangles.Length; at += 3)
        {
            for (var side = 0; side < 3; side++)
            {
                var one = triangles[at + side];
                var other = triangles[at + ((side + 1) % 3)];
                var key = (Math.Min(one, other), Math.Max(one, other));
                times[key] = times.TryGetValue(key, out var seen) ? seen + 1 : 1;
            }
        }

        var edges = new List<(int, int)>();
        foreach (var (key, seen) in times)
        {
            if (seen == 1) edges.Add(key);
        }

        return edges;
    }

    static string Share(int of, int all) => all == 0 ? "—" : $"{100.0 * of / all:F1}%";

    static string Portion(double of, double all) => all == 0.0 ? "—" : $"{100.0 * of / all:F3}%";

    /// <summary>How many distinct edges meet at each corner, counted off the triangles alone.</summary>
    static int[] EdgesAtCorners(int corners, int[] triangles)
    {
        var edgesAt = new int[corners];
        var seen = new HashSet<(int, int)>(triangles.Length);

        for (var at = 0; at + 2 < triangles.Length; at += 3)
        {
            for (var side = 0; side < 3; side++)
            {
                var one = triangles[at + side];
                var other = triangles[at + ((side + 1) % 3)];
                if (!seen.Add((Math.Min(one, other), Math.Max(one, other)))) continue;

                edgesAt[one]++;
                edgesAt[other]++;
            }
        }

        return edgesAt;
    }

    /// <summary>
    /// <b>How near a triangle is to the equilateral one</b>: <c>4√3·A / (a² + b² + c²)</c>, which is one for
    /// 60-60-60, falls off with either kind of badness a triangle has, and is nothing for three points on a
    /// line.
    /// </summary>
    /// <remarks>
    /// <b>A shape and not a size</b> — the ratio is scale-free, so a metre-wide triangle and a
    /// hundred-metre one of the same proportions read the same, which is what lets a column of it be
    /// compared across cuts that lay very differently sized triangles.
    /// </remarks>
    static double Quality(Vector2 a, Vector2 b, Vector2 c)
    {
        var sidesM2 = (double)Vector2.DistanceSquared(a, b)
            + Vector2.DistanceSquared(b, c)
            + Vector2.DistanceSquared(c, a);

        return sidesM2 <= 0.0 ? 0.0 : 4.0 * Math.Sqrt(3.0) * AreaM2(a, b, c) / sidesM2;
    }

    static double AreaM2(Vector2 a, Vector2 b, Vector2 c) => Math.Abs(TurnM2(a, b, c));

    /// <summary>
    /// A triangle's area <b>signed by which way its corners are given</b> — positive for one wound the way
    /// a shell's own outside is wound, and so negative for one laid inside out.
    /// </summary>
    /// <remarks>
    /// <b>Summed rather than the unsigned area, because it is the sum that means anything.</b> A fill that
    /// laid a triangle backwards, or laid the same ground twice, comes to more than the shell it is of; taken
    /// as unsigned areas both faults read as extra ground covered and neither can be told from a cut that is
    /// simply generous.
    /// </remarks>
    static double TurnM2(Vector2 a, Vector2 b, Vector2 c) =>
        ((((double)b.X - a.X) * (c.Y - a.Y)) - (((double)b.Y - a.Y) * (c.X - a.X))) * 0.5;

    /// <summary>The smallest of a triangle's three angles, which is what says whether it is a sliver.</summary>
    static double Smallest(Vector2 a, Vector2 b, Vector2 c) =>
        Math.Min(At(a, b, c), Math.Min(At(b, c, a), At(c, a, b)));

    static double At(Vector2 corner, Vector2 one, Vector2 other)
    {
        var toOne = one - corner;
        var toOther = other - corner;
        var across = Math.Abs(((double)toOne.X * toOther.Y) - ((double)toOne.Y * toOther.X));
        return Math.Atan2(across, Vector2.Dot(toOne, toOther));
    }

    static float LongestSideM(Vector2 a, Vector2 b, Vector2 c) =>
        MathF.Max(Vector2.Distance(a, b), MathF.Max(Vector2.Distance(b, c), Vector2.Distance(c, a)));
}
