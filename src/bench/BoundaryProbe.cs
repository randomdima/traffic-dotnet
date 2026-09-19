using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Statics;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>A town's own boundary, and that boundary moved off itself</b> (<see cref="ArcOutset"/>): how many rings
/// each came to, <b>how well those rings join up</b>, and — where the move left a run open — <b>what the two
/// ends of the hole are</b>. The move is read at one example distance, written out hole by hole, then
/// <b>swept across the range the figure is offered over</b> (<see cref="Swept"/>), and then at each of the
/// figures the town's own layers are struck at (<see cref="GroundRings"/>), which is a count of what closed
/// and nothing more.
/// </summary>
/// <remarks>
/// <para>
/// <b>The boundary of a shape moved off itself is closed</b>, so an open run is a defect and this is the
/// reading that says which kind. A hole whose two ends stand on top of one another is a walk that failed to
/// pair them; a hole a metre wide is a length of boundary the construction never made; an end standing other
/// than the distance asked for off the original is a corner solved wrong.
/// </para>
/// <para>
/// <b>And a ring that closed is not therefore a ring that joins.</b> The joints reading walks each ring and
/// measures the gap at every hand-over from one piece to the next — a gap past <see cref="ArcRings.WeldM"/>
/// being the merge failing its own tolerance, and one past a centimetre being a notch a frame could show in
/// anything filled or laid along the ring. <b>A gap is a hole only where neither piece covers it</b>: two
/// that overrun each other stand as far apart as two that fall short, and only one of the two is a break in
/// what is drawn. It is a reading and does not gate.
/// </para>
/// <para>
/// <b>It gates, and it is the only thing that does at this scale.</b> The boundary of a shape moved off
/// itself is closed — that is a claim and not a reading, so a run left open exits non-zero. It is asked of
/// a shipped city because a city is what has thousands of the corner the construction fails at, and a city
/// is content rather than engine (VER): the suite asks the same question of towns it lays for itself
/// (<c>PerimeterOutsetTests</c>, <c>BandOutsetTests</c>) and this is asked deliberately.
/// </para>
/// </remarks>
internal static class BoundaryProbe
{
    /// <summary>How far the boundary is moved, an example and a wide one: the move with corners to fail at.</summary>
    const float MovedM = 5f;

    /// <summary>
    /// And the radius its corners are asked to come back at: <b>an example and a wide one, as the distance
    /// is</b>. Every line the town lays is rounded at one figure of its own (TER-3c.10) and this is not it —
    /// what this reads is how the construction behaves at half the distance moved, which is a corner fill
    /// the size of a car and shows in a frame.
    /// </summary>
    const float RoundedM = MovedM * 0.5f;

    /// <summary>How many holes are written out one by one before the rest are left to the summary.</summary>
    const int Listed = 12;

    /// <summary>How far round the place the boundary is written out, which is a little past the move.</summary>
    const float ReachM = 7f;

    /// <summary>How many places a piece is measured at to say how near it comes (<see cref="Beyond"/>).</summary>
    const int Steps = 32;

    /// <summary>
    /// <b>How wide a joint has to be before a frame shows it</b>: a centimetre, which is under a pixel until
    /// a metre covers a hundred of them and is a twentieth of the narrowest thing drawn beside a road.
    /// </summary>
    const float SeenM = 0.01f;

    public static bool Outset(string map, SimConfig config, Vector2? askedM = null)
    {
        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        var shell = plan.Paving(config).Perimeter(config);
        var (rings, loose) = shell.Outset(MovedM, RoundedM);

        Console.WriteLine(
            $"outset — {plan.Name}, seed {plan.Seed}, moved {MovedM:F1} m, rounded {RoundedM:F1} m");
        Console.WriteLine();
        Console.WriteLine($"  boundary     {shell.Chains.Length} rings, {Km(shell.Chains):F1} km, " +
                          $"{Pieces(shell.Chains)} pieces, {shell.Loose.Length} runs left open by the merge");
        var halfKerbM = config.Road.KerbWidthM * 0.5f;
        Console.WriteLine($"  hooks        {Tightest(shell.Chains):F2} m of radius at its tightest, " +
                          $"{Hooked(shell.Chains, halfKerbM)} pieces turning in tighter than the " +
                          $"{halfKerbM:F2} m the kerb struck along them reaches");
        Console.WriteLine($"  moved        {rings.Length} rings, {Km(rings):F1} km, {Pieces(rings)} pieces");
        var notches = Notched(rings, out var worstDeg, out var worstAtM);
        Console.WriteLine($"  rounded      every corner asked for {RoundedM:F1} m of radius, " +
                          $"{notches} joins still turn in on the town, " +
                          $"worst {worstDeg:F1} deg at {worstAtM.X:F1}, {worstAtM.Y:F1}");
        var kinks = Kinked(rings, out var kinkDeg, out var kinkAtM);
        Console.WriteLine($"  cornered     {kinks} joins turn a corner either way, " +
                          $"worst {kinkDeg:F1} deg at {kinkAtM.X:F1}, {kinkAtM.Y:F1} — " +
                          $"the corners the town turns away at are the distance's until the radius passes it");
        Console.WriteLine($"  tightest     {Tightest(rings):F2} m of radius, against the {MovedM:F1} m it was moved");
        Console.WriteLine($"  open         {loose.Length} runs, {Km(loose):F1} km, {Pieces(loose)} pieces");
        Console.WriteLine($"  looped       {Looped(shell, rings)} pieces turn round without standing the distance off");
        Joints("  joints      ", shell.Chains);
        Swept(shell);

        // <b>And the layers the town is actually drawn from</b> (<see cref="GroundRings"/>, TER-3c.3), at
        // their own figures rather than at this probe's example of one. Each is the same move asked at a
        // different distance and then cut back against the boundary, so a run left open in one is the defect
        // the lines above report — and it is the one a frame shows.
        var layers = plan.Paving(config).Rings(config);
        foreach (var layer in layers.Layers)
        {
            Layer(shell, layer);
            Joints($"  {layer.Named + " joints",-12}", layer.Rings);
        }

        Joints("  face joints ", layers.WalkEdge);

        if (askedM is not null)
        {
            Console.WriteLine();
            Around(shell, rings, loose, layers, askedM.Value);
        }

        // <b>The bands are read and not gated on</b>, unlike the move above them. What gates is the example
        // distance, which is the widest move and so the one with the corners to fail at.
        if (loose.Length == 0) return true;

        Console.WriteLine();
        Console.WriteLine(
            $"  {"run",4}{"pieces",8}{"m",10}{"gap m",10}{"paired",8}{"off m",9}{"off m",9}{"mid m",9}   where");
        for (var run = 0; run < loose.Length; run++)
        {
            if (run == Listed)
            {
                Console.WriteLine($"  … and {loose.Length - Listed} more");
                break;
            }

            var tailM = loose[run][^1].EndM;
            var headM = loose[run][0].StartM;
            var (pairedWith, apartM) = Nearest(loose, run, tailM);
            // <b>What stands at the middle of the hole says which kind it is.</b> A middle the distance
            // moved off the boundary is a length of offset the construction never made; a middle nearer
            // than that is a place the offset really does touch itself, and the hole is the walk's.
            var midM = pairedWith < 0 ? tailM : (tailM + loose[pairedWith][0].StartM) * 0.5f;
            Console.WriteLine(
                $"  {run,4}{loose[run].Length,8}{Spline.TotalLengthM(loose[run]),10:F1}{apartM,10:F3}" +
                $"{pairedWith,8}{Off(shell, tailM),9:F3}{Off(shell, headM),9:F3}{Off(shell, midM),9:F3}" +
                $"   {tailM.X:F1}, {tailM.Y:F1}");
        }

        if (askedM is null)
        {
            Console.WriteLine();
            Around(shell, rings, loose, layers, loose[0][^1].EndM);
        }

        Console.WriteLine();
        Console.WriteLine($"  holes under a weld  {Under(loose, ArcRings.WeldM)} of {loose.Length}");
        Console.WriteLine($"  holes under a metre {Under(loose, 1f)} of {loose.Length}");
        Console.WriteLine($"  ends off the move   {Strayed(shell, loose)} of {loose.Length * 2}");
        return false;
    }

    /// <summary>
    /// <b>The same move at a run of distances and a run of radii</b>, each read for the corners left in it —
    /// <b>the reading that says whether the rounding is a figure or a coin toss</b>. A construction that
    /// behaves takes fewer corners out as it is asked for less, and <b>reads down a column as well as
    /// across</b>: the radius is a radius and not a share, so a column is one answer to one question and the
    /// distance moved should barely move it.
    /// </summary>
    static void Swept(BandShell shell)
    {
        Console.WriteLine();
        Console.WriteLine(
            "  swept        corners left and the sharpest of them in degrees, " +
            "by how far it was moved (down) and the radius asked for (across), both in m");
        Console.Write($"  {"m",8}");
        foreach (var roundedM in Rounds) Console.Write($"{roundedM,20:F1}");
        Console.WriteLine();

        foreach (var outwardM in SweptM)
        {
            Console.Write($"  {outwardM,8:F1}");
            foreach (var roundedM in Rounds)
            {
                var (swept, sweptLoose) = shell.Outset(outwardM, roundedM);
                var left = Kinked(swept, out var sharpestDeg, out _);
                var open = sweptLoose.Length == 0 ? "" : $" +{sweptLoose.Length} open";
                Console.Write($"{$"{left} at {sharpestDeg:F0}{open}",20}");
            }

            Console.WriteLine();
        }
    }

    /// <summary>
    /// How far the sweep moves the boundary: <b>the two a pavement is struck at, this probe's own, and the
    /// ends of the range the debug dial offers</b> (OBS-2w) — a ladder rather than a step, because what the
    /// reading is for is how the figure behaves across its range and not a curve to be plotted.
    /// </summary>
    static readonly float[] SweptM = [0f, 0.5f, 1f, 2f, 3f, 5f, 8f, 12f];

    /// <summary>
    /// The radii it asks for: none, the pavement's own, and two that pass the distances above them — which
    /// is <b>the half of the range the town does not use and a reader turns the dial into</b> (OBS-2w),
    /// where the corners the town turns away at are cut round as well.
    /// </summary>
    static readonly float[] Rounds = [0f, 0.5f, 2f, 8f];

    /// <summary>
    /// <b>How well a set of closed rings actually joins up</b>: at every joint, the gap between the piece
    /// that arrives and the piece that leaves, and at the last joint of a ring the gap back to its own start.
    /// </summary>
    /// <remarks>
    /// <b>The walk hands a ring back with its pieces made to meet</b> (<see cref="ArcRings.Tightened"/>), so
    /// this is the reading that says it did: what is left is the piece too short to be re-struck between its
    /// own moved ends, and a line drawn piece by piece breaks by exactly this much at exactly these places.
    /// </remarks>
    static void Joints(string named, ReadOnlySpan<ArcSeg[]> rings)
    {
        var joints = 0;
        var pastRounding = 0;
        var pastSeen = 0;
        var pastWeld = 0;
        var worstM = 0f;
        var worstAtM = Vector2.Zero;
        var broken = new List<(float GapM, Vector2 AtM)>();
        foreach (var ring in rings)
        {
            for (var piece = 0; piece < ring.Length; piece++)
            {
                ref readonly var arriving = ref ring[piece];
                ref readonly var leaving = ref ring[(piece + 1) % ring.Length];
                var endM = arriving.EndM;
                var startM = leaving.StartM;
                var gapM = Vector2.Distance(endM, startM);
                joints++;
                if (gapM > LineTolerance.RoundingM) pastRounding++;
                if (gapM > SeenM) pastSeen++;
                if (gapM > ArcRings.WeldM) pastWeld++;
                if (gapM > worstM) (worstM, worstAtM) = (gapM, endM);
                if (gapM > SeenM && Broken(arriving, leaving)) broken.Add((gapM, endM));
            }
        }

        Console.WriteLine(
            $"{named} {joints} joints, {pastRounding} past a rounding, {pastSeen} past a centimetre, " +
            $"{pastWeld} past a weld, worst {worstM * 1000f:F1} mm at {worstAtM.X:F1}, {worstAtM.Y:F1}");
        Console.WriteLine(
            $"{new string(' ', named.Length)} {broken.Count} of them a hole a line drawn down the ring shows");

        // Written out where they are, because a figure without the place to point `--at` at is a figure
        // nobody can go and look at.
        broken.Sort((one, other) => other.GapM.CompareTo(one.GapM));
        for (var at = 0; at < broken.Count && at < Listed; at++)
        {
            Console.WriteLine(
                $"    {broken[at].GapM * 1000f,8:F1} mm at {broken[at].AtM.X:F2}, {broken[at].AtM.Y:F2}");
        }
    }

    /// <summary>
    /// <b>Whether the gap at one joint is a hole in the line and not an overlap</b>: the middle of the gap
    /// is measured against both pieces that meet there, and it is a hole where neither of them passes within
    /// a hair of it.
    /// </summary>
    /// <remarks>
    /// <b>The distance between two ends does not say which.</b> A piece that stops short of where the next
    /// one starts and a piece that runs a hand's breadth past it read the same apart, and only one of them
    /// leaves the frame showing anything: what is drawn down the ring covers the overlap twice and the hole
    /// not at all.
    /// </remarks>
    static bool Broken(in ArcSeg arriving, in ArcSeg leaving)
    {
        var midM = (arriving.EndM + leaving.StartM) * 0.5f;
        return Off(arriving, midM) > SeenM && Off(leaving, midM) > SeenM;
    }

    /// <summary>How far a place stands off one piece, measured to the piece and not to the circle it lies on.</summary>
    static float Off(in ArcSeg piece, Vector2 pointM)
    {
        Span<ArcSeg> one = stackalloc ArcSeg[1];
        one[0] = piece;
        var alongM = Spline.ProjectM(one, pointM, piece.LengthM * 0.5f, piece.LengthM);
        return Vector2.Distance(piece.PointAtM(alongM), pointM);
    }

    /// <summary>
    /// <b>One of the layers the town is drawn from</b>, in a line: what it came to and where its holes are.
    /// </summary>
    /// <remarks>
    /// <b>The place is written out and the hole is not measured.</b> What a run left open by one of these is
    /// is the same question the table above answers, at a different distance — so what this owes a reader is
    /// enough to go and ask it with <c>--at</c>, and not a second copy of it.
    /// </remarks>
    static void Layer(BandShell shell, GroundLayer layer)
    {
        Console.Write(
            $"  {layer.Named,-12} {layer.Rings.Length} rings at {layer.OutwardM:F2} m, " +
            $"{Km(layer.Rings):F1} km, {layer.Loose.Length} open");
        for (var run = 0; run < layer.Loose.Length && run < Listed; run++)
        {
            var endM = layer.Loose[run][^1].EndM;
            Console.Write($"{(run == 0 ? " at " : ", ")}{endM.X:F1}, {endM.Y:F1} ({Off(shell, endM):F2} m off)");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// <b>One place written out four times over</b>: the boundary it was cut out of, what the answer holds
    /// there, the candidates the cut was given, and what the band the town is actually drawn from holds.
    /// <c>--at X Y</c> names the place; without one it is the first hole, because that is the place a reader
    /// wants when a run would not close.
    /// </summary>
    /// <remarks>
    /// <b>The readings together are what say which kind of defect a place has.</b> The lengths the
    /// boundary's pieces come in and the angles they meet at decide whether a corner closes inside its own
    /// neighbours; how far each candidate's two ends stand off the shape says whether the cut was given the
    /// right lines at all; what the answer holds says what it did with them; and <b>the band says which of
    /// its two edges a hole in the concrete is on</b> — an end standing the band's own width off the
    /// boundary is on the offset and an end standing on it is on the boundary itself, which are two
    /// different faults.
    /// </remarks>
    static void Around(BandShell shell, ArcSeg[][] rings, ArcSeg[][] loose, GroundRings layers, Vector2 atM)
    {
        var kerb = layers.Walk;
        Console.WriteLine($"  at {atM.X:F3}, {atM.Y:F3}");
        Console.WriteLine();
        Console.WriteLine($"  the boundary within {ReachM:F0} m of it");
        Console.WriteLine($"  {"piece",7}{"m",9}{"turn deg",10}{"radius m",10}{"off m",10}   from");
        foreach (var ring in shell.Chains)
        {
            for (var at = 0; at < ring.Length; at++)
            {
                if (Beyond(ring[at], atM, out var offM)) continue;

                var leaving = Heading.Unit(ring[at].HeadingAtRad(ring[at].LengthM));
                var taking = ring[(at + 1) % ring.Length].StartUnit;
                var turnRad = MathF.Atan2(
                    (leaving.X * taking.Y) - (leaving.Y * taking.X), Vector2.Dot(leaving, taking));
                Console.WriteLine(
                    $"  {at,7}{ring[at].LengthM,9:F3}{turnRad * 180f / MathF.PI,10:F2}{Radius(ring[at]),10:F1}" +
                    $"{offM,10:F4}   {ring[at].StartM.X:F3}, {ring[at].StartM.Y:F3}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("  what the answer holds there");
        Console.WriteLine($"  {"ring",7}{"m",9}{"radius m",10}{"off m",10}{"stands",9}{"stands",9}   from   to");
        Kept(shell, rings, atM, "");
        Kept(shell, loose, atM, "open");

        Console.WriteLine();
        Console.WriteLine($"  the candidates the cut was given within {ReachM:F0} m of it");
        Console.WriteLine($"  {"m",16}{"radius m",10}{"off m",10}{"stands",9}{"stands",9}   from   to");
        foreach (var ring in ArcOutset.Moved(shell.Chains, MovedM))
        {
            foreach (var piece in ring)
            {
                if (Beyond(piece, atM, out var offM)) continue;

                Console.WriteLine(
                    $"  {piece.LengthM,16:F3}{Radius(piece),10:F1}{offM,10:F4}" +
                    $"{Off(shell, piece.StartM),9:F3}{Off(shell, piece.EndM),9:F3}" +
                    $"   {piece.StartM.X:F3}, {piece.StartM.Y:F3}   {piece.EndM.X:F3}, {piece.EndM.Y:F3}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"  what the kerb holds there, the band being {kerb.OutwardM:F2} m wide");
        Console.WriteLine($"  {"ring",7}{"m",9}{"radius m",10}{"off m",10}{"stands",9}{"stands",9}   from   to");
        Kept(shell, kerb.Rings, atM, "");
        Kept(shell, kerb.Loose, atM, "open");
    }

    /// <summary>
    /// <b>The tightest the answer turns anywhere</b>, in metres of radius. <b>Nothing in an offset is
    /// obliged to turn tighter than the distance it was moved</b> — the corner of a shape comes back as the
    /// arc of that distance about it — so a figure under the move is a place the smoothing left sharper
    /// than the offset already was, and the reading a kerb laid along this line would be held to.
    /// </summary>
    static float Tightest(ReadOnlySpan<ArcSeg[]> rings)
    {
        var radiusM = float.PositiveInfinity;
        foreach (var ring in rings)
        {
            foreach (var piece in ring)
            {
                radiusM = MathF.Min(radiusM, MathF.Abs(Radius(piece)));
            }
        }

        return radiusM;
    }

    /// <summary>
    /// <b>How many joins of the answer still turn in on the town</b> — the notches a fold cut left that the
    /// smoothing did not take out, each a place the line turns through a corner rather than a radius.
    /// </summary>
    /// <remarks>
    /// <b>It is the half of the work the distance cannot do</b>: a corner turning the other way is the offset
    /// of a corner of the town and is an arc of the distance moved already, until the radius asked for passes
    /// that distance. At no rounding it counts every notch the offset has; the figure is how many are left.
    /// </remarks>
    static int Notched(ArcSeg[][] rings) => Notched(rings, out _, out _);

    /// <summary>
    /// The same count with <b>the sharpest of them and where it stands</b>, since how many notches are left
    /// says nothing about whether a frame shows one: a degree of kink at the grain of the ring and a right
    /// angle in the middle of a pavement are both one notch.
    /// </summary>
    static int Notched(ArcSeg[][] rings, out float worstDeg, out Vector2 worstAtM) =>
        Cornered(rings, inward: true, out worstDeg, out worstAtM);

    /// <summary>
    /// <b>Every join that turns a corner, whichever way it turns</b> — which is what a radius past the
    /// distance moved is asked to leave none of, the hand a fold cut and the hand the town turns away at
    /// alike.
    /// </summary>
    static int Kinked(ArcSeg[][] rings, out float worstDeg, out Vector2 worstAtM) =>
        Cornered(rings, inward: false, out worstDeg, out worstAtM);

    static int Cornered(ArcSeg[][] rings, bool inward, out float worstDeg, out Vector2 worstAtM)
    {
        var count = 0;
        worstDeg = 0f;
        worstAtM = Vector2.Zero;
        foreach (var ring in rings)
        {
            if (ring.Length < 2) continue;

            for (var at = 0; at < ring.Length; at++)
            {
                var onto = ring[(at + 1) % ring.Length];
                var leaving = Heading.Unit(ring[at].HeadingAtRad(ring[at].LengthM));
                var taking = onto.StartUnit;
                var turnRad = MathF.Atan2(
                    (leaving.X * taking.Y) - (leaving.Y * taking.X), Vector2.Dot(leaving, taking));
                var sharpRad = inward ? -turnRad : MathF.Abs(turnRad);

                if (sharpRad <= NotchRad) continue;

                count++;
                if (sharpRad * 180f / MathF.PI <= worstDeg) continue;

                worstDeg = sharpRad * 180f / MathF.PI;
                worstAtM = ring[at].EndM;
            }
        }

        return count;
    }

    /// <summary>What a join may turn in by and still be the arithmetic rather than a notch: a degree.</summary>
    const float NotchRad = MathF.PI / 180f;

    /// <summary>
    /// <b>How many pieces of the answer turn most of the way round without standing the distance off the
    /// town</b>, which is the reading that catches a loop: <b>an arc nothing asked for</b>, drawn as a ring
    /// hanging off the line with no feature under it.
    /// </summary>
    /// <remarks>
    /// <b>Turning far is not the fault and never was.</b> A shape may turn all the way round without a
    /// corner in it — a town's roundabout island is exactly that, and its inset is the same circle five
    /// metres smaller, written in as many pieces as the island's own boundary happened to be. What separates
    /// that from a loop is the one thing every piece of an offset owes: <b>it stands the distance off the
    /// shape it was taken from</b>, and the arc a rounding invents does not, which is what the earlier
    /// reading kept mistaking the island for.
    /// </remarks>
    static int Looped(BandShell shell, ArcSeg[][] rings)
    {
        var count = 0;
        foreach (var ring in rings)
        {
            foreach (var piece in ring)
            {
                var turnRad = MathF.Abs(piece.LengthM * piece.Curvature);
                if (turnRad <= MathF.PI) continue;

                var offM = Off(shell, piece.PointAtM(piece.LengthM * 0.5f));
                if (MathF.Abs(offM - MovedM) <= StandsM) continue;

                count++;
                Console.WriteLine(
                    $"               {turnRad * 180f / MathF.PI,6:F0} deg over {piece.LengthM:F1} m at " +
                    $"radius {Radius(piece):F1}, standing {offM:F2} m off the town, " +
                    $"from {piece.StartM.X:F1}, {piece.StartM.Y:F1}");
            }
        }

        return count;
    }

    /// <summary>How far off the move a piece of the answer may stand and still be one: a quarter of a metre, which a rounding is never inside.</summary>
    const float StandsM = 0.25f;

    /// <summary>The stretches of one set of runs that come within the reach of a place.</summary>
    static void Kept(BandShell shell, ArcSeg[][] runs, Vector2 atM, string what)
    {
        for (var run = 0; run < runs.Length; run++)
        {
            foreach (var piece in runs[run])
            {
                if (Beyond(piece, atM, out var offM)) continue;

                Console.WriteLine(
                    $"  {$"{what}{run}",7}{piece.LengthM,9:F3}{Radius(piece),10:F1}{offM,10:F4}" +
                    $"{Off(shell, piece.StartM),9:F3}{Off(shell, piece.EndM),9:F3}" +
                    $"   {piece.StartM.X:F3}, {piece.StartM.Y:F3}   {piece.EndM.X:F3}, {piece.EndM.Y:F3}");
            }
        }
    }

    /// <summary>
    /// Whether one piece passes further than the reach from a place, and how near it comes. <b>Sampled and
    /// not projected</b>: a piece that curves round the very place being asked about is every distance from
    /// it at once, which is the one question a projection cannot answer — and an arc curving round a place
    /// is exactly what a reader looking at a circle in the picture is trying to find.
    /// </summary>
    static bool Beyond(in ArcSeg piece, Vector2 atM, out float offM)
    {
        offM = float.MaxValue;
        for (var step = 0; step <= Steps; step++)
        {
            var alongM = piece.LengthM * step / Steps;
            offM = MathF.Min(offM, Vector2.Distance(piece.PointAtM(alongM), atM));
        }

        return offM > ReachM;
    }

    static float Radius(in ArcSeg piece) =>
        MathF.Abs(piece.Curvature) < 1e-6f ? float.PositiveInfinity : 1f / piece.Curvature;

    /// <summary>
    /// <b>How many pieces of a boundary turn tighter than a stroke laid along it reaches</b> (TER-3d) — the
    /// hooks a merge leaves where a movement's ribbon folds through itself, and the one shape a constant
    /// width cannot be laid along: the edge on the inside of the turn reaches the middle of it before it has
    /// run the half-width out, and everything past that comes back on the far side of the line.
    /// </summary>
    /// <remarks>
    /// <b>Counted either way round</b>, a stroke standing half its width to both sides of its line: which
    /// side folds is which way the piece turns, and both are the same fault.
    /// </remarks>
    static int Hooked(ReadOnlySpan<ArcSeg[]> rings, float reachM)
    {
        var count = 0;
        for (var ring = 0; ring < rings.Length; ring++)
        {
            foreach (var piece in rings[ring])
            {
                if (MathF.Abs(piece.Curvature) * reachM < 1f) continue;

                count++;
                Console.WriteLine(
                    $"               ring {ring}, radius {Radius(piece):F2} over {piece.LengthM:F2} m, " +
                    $"from {piece.StartM.X:F1}, {piece.StartM.Y:F1}");
            }
        }

        return count;
    }

    /// <summary>
    /// The run whose head stands nearest one run's tail, and how far off that is. <b>Its own head counts</b>:
    /// a ring broken in one place comes back as the one run whose two ends are the two sides of that hole.
    /// </summary>
    static (int Run, float ApartM) Nearest(ArcSeg[][] loose, int run, Vector2 tailM)
    {
        var best = -1;
        var apartM = float.MaxValue;
        for (var other = 0; other < loose.Length; other++)
        {
            var offM = Vector2.Distance(loose[other][0].StartM, tailM);
            if (offM >= apartM) continue;

            apartM = offM;
            best = other;
        }

        return (best, apartM);
    }

    /// <summary>How many holes are narrower than one figure, which is what separates a pairing from a gap.</summary>
    static int Under(ArcSeg[][] loose, float withinM)
    {
        var count = 0;
        for (var run = 0; run < loose.Length; run++)
        {
            if (Nearest(loose, run, loose[run][^1].EndM).ApartM <= withinM) count++;
        }

        return count;
    }

    /// <summary>How many open ends stand other than the distance asked for off the boundary they came from.</summary>
    static int Strayed(BandShell shell, ArcSeg[][] loose)
    {
        var count = 0;
        foreach (var run in loose)
        {
            if (MathF.Abs(Off(shell, run[0].StartM) - MovedM) > LineTolerance.JoinedM) count++;
            if (MathF.Abs(Off(shell, run[^1].EndM) - MovedM) > LineTolerance.JoinedM) count++;
        }

        return count;
    }

    /// <summary>
    /// How far a place stands off the nearest piece of the boundary it was taken from. <b>Clamped to the
    /// piece</b>: a projection that ran off the end is the circle the piece lies on and not the piece, and
    /// read unclamped it puts a place a centimetre nearer the shape than it stands.
    /// </summary>
    static float Off(BandShell shell, Vector2 pointM)
    {
        var offM = float.MaxValue;
        foreach (var ring in shell.Chains)
        {
            foreach (var piece in ring)
            {
                var alongM = Spline.ProjectM([piece], pointM, piece.LengthM * 0.5f, piece.LengthM);
                var onM = piece.PointAtM(Math.Clamp(alongM, 0f, piece.LengthM));
                offM = MathF.Min(offM, Vector2.Distance(onM, pointM));
            }
        }

        return offM;
    }

    static float Km(ReadOnlySpan<ArcSeg[]> chains)
    {
        var lengthM = 0f;
        foreach (var chain in chains) lengthM += Spline.TotalLengthM(chain);

        return lengthM / 1000f;
    }

    static int Pieces(ReadOnlySpan<ArcSeg[]> chains)
    {
        var pieces = 0;
        foreach (var chain in chains) pieces += chain.Length;

        return pieces;
    }
}
