namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// <b>How open a joint may be and still be one line.</b> Four tolerances, read by everything that lays a
/// chain of arcs, cuts one against another, or asks whether two ends are the same place or point the same
/// way.
/// </summary>
/// <remarks>
/// <para>
/// <b>They are facts about the arithmetic and not about any shape.</b> They lived on the town's kerb band
/// while that was the largest thing being cut and joined, which made every reader of them look like a
/// reader of kerbs; the band is gone and the tolerances are not, because what they measure is what
/// offsetting, projecting and bisecting a line cost in the last bits of a float.
/// </para>
/// <para>
/// <b>Not on <c>SimConfig</c>, because none of them is a figure about the town.</b> A number somebody could
/// retune to lay a different town belongs there; a number that says what two spellings of one distance
/// disagree by belongs beside the arithmetic that disagrees.
/// </para>
/// </remarks>
internal static class LineTolerance
{
    /// <summary>
    /// A millimetre: <b>what two computations of one distance disagree by</b>. Offsetting a chain and
    /// measuring back to what it was offset from are different arithmetic, so a line laid exactly the offset
    /// out reads a hair inside it; and two wrapping lines cut where they cross are cut by two bisections of
    /// their own, so the ends that meet at a node are two points and not one point twice.
    /// </summary>
    public const float RoundingM = 0.001f;

    /// <summary>
    /// How near two pieces have to end and start to be one line: a centimetre, which is well under anything
    /// a reader could see.
    /// </summary>
    public const float JoinedM = 0.01f;

    /// <summary>
    /// <b>How far the two ends that meet at a crossing can stand apart</b>, which is what anybody wanting
    /// them as one place has to allow.
    /// </summary>
    /// <remarks>
    /// A line is cut a rounding late, and a line meeting another <em>tangentially</em> runs √(2·R·ε) past
    /// the point they cross before it is a rounding inside it (<see cref="RoundingM"/>) — a tenth of a metre
    /// at the radius a junction's corner is turned on. It is the bound and not a measurement: the ends that
    /// actually meet at a right angle stand a millimetre apart.
    /// </remarks>
    public const float OnePlaceM = 0.15f;

    /// <summary>
    /// <b>How far a joint may read off and still be one line carrying straight on</b>: a hundredth of a
    /// radian, which is half a degree. Below it there is no corner there to round, and a sum of headings is
    /// at its own last bits.
    /// </summary>
    public const float StraightOnRad = 0.01f;
}
