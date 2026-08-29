using System.Numerics;
using TrafficSimulation.App.Debug;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.Debug;

/// <summary>
/// Where the marks down a debug line stand. The whole claim is that the answer is about the ground and
/// not about the line: cut the same street anywhere, run it either way, offset it into the next lane, and
/// the marks land on the same stones.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class MarkCombTests
{
    const float PitchM = 1.5f;

    /// <summary>Where a run's first mark actually falls, in the world rather than along the run.</summary>
    static Vector2 FirstMarkAt(Vector2 fromM, Vector2 direction)
    {
        var unit = Vector2.Normalize(direction);
        return fromM + (unit * PathMarks.FirstMarkM(fromM, unit, PitchM));
    }

    /// <summary>
    /// Whether two marks stand on the same comb, which is a whole number of pitches apart and not the
    /// same place: a run cut further down the street picks up a later tooth of the one comb.
    /// </summary>
    static void SameComb(float aM, float bM) =>
        Assert.Equal(0f, (float)Math.IEEERemainder(aM - bM, PitchM), 4);

    /// <summary>The one property the comb exists for: a line's own start does not move a single mark.</summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(0.4f)]
    [InlineData(7.3f)]
    public void WhereALineStartsDoesNotMoveItsMarks(float cutM)
    {
        var alongM = new Vector2(1f, 0f);

        var whole = FirstMarkAt(new Vector2(120f, 40f), alongM);
        var cut = FirstMarkAt(new Vector2(120f + cutM, 40f), alongM);

        SameComb(whole.X, cut.X);
    }

    /// <summary>Which is what makes the marks of two lanes of one carriageway stack square across it.</summary>
    [Fact]
    public void TwoLanesOfOneStreetMarkTheSameStones()
    {
        var alongM = new Vector2(0.6f, 0.8f);
        var acrossM = Heading.RightOf(Vector2.Normalize(alongM)) * 3.25f;

        var near = FirstMarkAt(new Vector2(120f, 40f), alongM);
        var far = FirstMarkAt(new Vector2(120f, 40f) + acrossM, alongM);

        // Square across the street: the two marks differ by the offset between the lanes and by nothing
        // along them.
        Assert.Equal(0f, Vector2.Dot(far - near - acrossM, Vector2.Normalize(alongM)), 4);
    }

    /// <summary>
    /// And the two directions of one line mark the same stones as each other, because the comb of a
    /// bearing and the comb of its reverse are one comb. It is why a stretch walked both ways can be
    /// ticked rather than chevronned without the two passes fighting.
    /// </summary>
    [Fact]
    public void TheTwoDirectionsOfOneLineMarkTheSameStones()
    {
        var alongM = new Vector2(-1f, 0f);

        var there = FirstMarkAt(new Vector2(120f, 40f), alongM);
        var back = FirstMarkAt(new Vector2(112f, 40f), -alongM);

        SameComb(there.X, back.X);
    }

    /// <summary>A mark stands ahead of the run's start and no more than one pitch ahead of it, either side of the origin.</summary>
    [Theory]
    [InlineData(120f, 1f)]
    [InlineData(120f, -1f)]
    [InlineData(-40f, 1f)]
    [InlineData(-40f, -1f)]
    public void TheFirstMarkIsWithinOnePitchOfTheStart(float atM, float towards)
    {
        var firstM = PathMarks.FirstMarkM(new Vector2(atM, 0f), new Vector2(towards, 0f), PitchM);

        Assert.InRange(firstM, 0f, PitchM);
    }
}
