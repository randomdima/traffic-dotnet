using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

internal static partial class ShellFill
{
    /// <summary>
    /// <b>One ring's corners thinned</b>, in place, to however few of them still stand for the same line
    /// within the budget. What comes back is how many are left; they are at the front of the span.
    /// </summary>
    static int Thinned(Span<Vector2> ring, float thriftM, float turnRad) =>
        ring.Length < 4 || thriftM <= 0f ? ring.Length : Peucker(ring, thriftM, turnRad);

    /// <summary>
    /// <b>An open line's corners thinned</b>, in place: both its ends kept, and every corner between them kept
    /// only where the straight past it does not already stand for it within the budget. What comes back is
    /// how many are left; they are at the front of the span.
    /// </summary>
    public static int ThinnedLine(Span<Vector2> line, float thriftM)
    {
        if (line.Length < 3 || thriftM <= 0f) return line.Length;

        var keep = new bool[line.Length];
        keep[0] = keep[^1] = true;

        var pending = new Stack<(int From, int Onto)>();
        pending.Push((0, line.Length - 1));
        Split(line, keep, pending, thriftM, 0f, []);

        var kept = 0;
        for (var at = 0; at < line.Length; at++)
        {
            if (keep[at]) line[kept++] = line[at];
        }

        return kept;
    }

    /// <summary>
    /// <b>Douglas–Peucker over a closed ring.</b> The corner furthest from the first one splits it into two
    /// open chains, and each is kept only where some corner of it stands further than the budget off the
    /// straight between its ends — recursively, so what survives is the furthest corner of every stretch the
    /// straight does not already stand for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Split before it is walked, because a ring has no ends.</b> Run from any single corner the first
    /// straight is the whole ring's chord and the recursion has nothing to bound; the furthest corner from
    /// the first is the one split that is never degenerate.
    /// </para>
    /// <para>
    /// <b>How far each stretch turns is worked out once for the ring and not once per stretch.</b> The
    /// recursion asks the question of every stretch it looks at, and a stretch is a run of the same corners
    /// the one before it was — so what is carried is the turn up to each corner, and a stretch's own is the
    /// difference between its two ends.
    /// </para>
    /// </remarks>
    static int Peucker(Span<Vector2> ring, float thriftM, float turnRad)
    {
        var keep = new bool[ring.Length];
        keep[0] = true;

        var farthest = 0;
        var farthestM2 = -1.0;
        for (var at = 1; at < ring.Length; at++)
        {
            var offM2 = Vector2.DistanceSquared(ring[0], ring[at]);
            if (offM2 <= farthestM2) continue;

            farthestM2 = offM2;
            farthest = at;
        }

        keep[farthest] = true;
        var pending = new Stack<(int From, int Onto)>();
        pending.Push((0, farthest));
        pending.Push((farthest, ring.Length));
        Split(ring, keep, pending, thriftM, turnRad, TurnedUpTo(ring));

        var kept = 0;
        for (var at = 0; at < ring.Length; at++)
        {
            if (keep[at]) ring[kept++] = ring[at];
        }

        return kept;
    }

    /// <summary>
    /// <b>How far the line has turned by each corner of a ring</b>, so the turn across any stretch of it is
    /// the difference between its two ends. Unsigned, an S-bend counting as the sum of its two ways.
    /// </summary>
    /// <remarks>
    /// <b>Unsigned because it is asked how much of a bend a straight would be standing for</b>, and a
    /// stretch that turns one way and back stands for neither. Split at its furthest corner it comes back as
    /// two stretches that each turn one way, which is the shape the question has an answer about.
    /// </remarks>
    static float[] TurnedUpTo(ReadOnlySpan<Vector2> ring)
    {
        var turned = new float[ring.Length];
        var previous = Vector2.Zero;
        for (var at = 1; at < ring.Length; at++)
        {
            turned[at] = turned[at - 1];

            var stepM = ring[at] - ring[at - 1];
            var lengthM = stepM.Length();
            if (lengthM <= 0f) continue;

            var along = stepM / lengthM;
            if (previous != Vector2.Zero)
            {
                turned[at] += MathF.Abs(
                    MathF.Atan2(Spline.Cross(previous, along), Vector2.Dot(previous, along)));
            }

            previous = along;
        }

        return turned;
    }

    /// <summary>
    /// One stretch of a ring kept where it does not stand for itself: the corner furthest off the straight
    /// between the ends, kept where it stands further than the budget <b>or where the stretch turns more
    /// than one chord may stand for</b>, and then the two stretches either side of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A stretch's <c>Onto</c> is one past its last corner, and the ring's length names the first corner
    /// again — which is how the second chain closes without the ring being copied.
    /// </para>
    /// <para>
    /// <b>The two budgets answer different questions and a tight bend is where they part.</b> How far a
    /// corner stands off the straight is what the shape loses; how far the stretch turns is what the
    /// <em>direction</em> loses, and on a bend tight enough the second runs out while the first has hardly
    /// been spent — a quarter turn of a fifth of a metre bows four centimetres off its own chord, so a
    /// budget in metres alone hands the whole corner back and the line is cut square across it.
    /// </para>
    /// <para>
    /// <b>Worked off a stack of its own and not off the call stack.</b> The cut is made at the furthest
    /// corner rather than at the middle, so nothing bounds how deep the splitting goes but the ring
    /// itself: a stretch that gives up one corner at a time goes as deep as it is long, and a town's
    /// boundary is tens of thousands of corners read at a sag. <b>It is the one failure that takes the
    /// process with it</b> rather than the answer, so it is not left to how the shape comes out.
    /// </para>
    /// </remarks>
    static void Split(
        Span<Vector2> ring, bool[] keep, Stack<(int From, int Onto)> pending, float thriftM, float turnRad,
        float[] turnedUpTo)
    {
        while (pending.Count > 0)
        {
            var (from, onto) = pending.Pop();
            if (onto - from < 2) continue;

            var fromM = ring[from];
            var ontoM = ring[onto % ring.Length];

            var farthest = -1;
            var farthestM = 0.0;
            for (var at = from + 1; at < onto; at++)
            {
                var offM = OffTheLineM(fromM, ontoM, ring[at]);
                if (offM <= farthestM) continue;

                farthestM = offM;
                farthest = at;
            }

            if (farthest < 0) continue;
            if (farthestM <= thriftM
                && (turnRad <= 0f || turnedUpTo[onto - 1] - turnedUpTo[from] <= turnRad)) continue;

            keep[farthest] = true;
            pending.Push((from, farthest));
            pending.Push((farthest, onto));
        }
    }

    /// <summary>How far a point stands off the straight between two others, or off the nearer of them where those two stand at one place.</summary>
    static double OffTheLineM(Vector2 fromM, Vector2 ontoM, Vector2 pointM)
    {
        var runM = ontoM - fromM;
        var lengthM = (double)runM.Length();
        if (lengthM <= 0.0) return Vector2.Distance(fromM, pointM);

        var across = (((double)pointM.X - fromM.X) * runM.Y) - (((double)pointM.Y - fromM.Y) * runM.X);
        return Math.Abs(across) / lengthM;
    }
}
