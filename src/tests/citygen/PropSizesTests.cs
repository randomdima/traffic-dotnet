using TrafficSimulation.CityGen;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>How a look is drawn</b> (<see cref="PropSizes"/>): as often as its share against the rest of its kind (GEN-6e),
/// among the looks no wider than a clump's heart (GEN-6g), or among the crowned looks a street is lined with (GEN-6f).
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class PropSizesTests
{
    /// <summary>
    /// A weed, a bush and a tree of the wild set, the tree on its trunk; a pot and a lime of the planted, the lime on its
    /// trunk; a bin. The weed's share is three to the bush's one.
    /// </summary>
    static readonly PropSizes Looks = new(
        [PropKind.WildNature, PropKind.WildNature, PropKind.WildNature, PropKind.UrbanNature, PropKind.UrbanNature, PropKind.UrbanFurniture],
        [4.2f, 1.6f, 0.8f, 1f, 3.4f, 0.5f],
        [0.5f, 1.6f, 0.8f, 1f, 0.5f, 0.5f],
        [1f, 1f, 3f, 1f, 1f, 1f]);

    /// <summary>
    /// <b>A look is drawn as often as its share says</b> (GEN-6e): of a weed at three, a bush and a tree at one each, the
    /// first three fifths of the draws are the weed's, the next fifth the bush's and the last the tree's.
    /// </summary>
    [Theory]
    [InlineData(0f, 2)]
    [InlineData(0.59f, 2)]
    [InlineData(0.61f, 1)]
    [InlineData(0.79f, 1)]
    [InlineData(0.81f, 0)]
    [InlineData(0.999f, 0)]
    public void ALookIsDrawnAsOftenAsItsShareSays(float u, int look) => Assert.Equal(look, Looks.Draw(PropKind.WildNature, u));

    /// <summary>
    /// <b>A clump's member is no wider than its heart</b> (GEN-6g): round a bush, the whole of the draw is the weed's and
    /// the bush's, in their shares, and never the tree's.
    /// </summary>
    [Theory]
    [InlineData(0f, 2)]
    [InlineData(0.74f, 2)]
    [InlineData(0.76f, 1)]
    [InlineData(0.999f, 1)]
    public void AClumpsMemberIsNoWiderThanItsHeart(float u, int look) =>
        Assert.Equal(look, Looks.DrawNoWider(PropKind.WildNature, u, 1.6f));

    /// <summary><b>Round a heart narrower than any look nothing grows</b>: the draw is none rather than the narrowest look.</summary>
    [Fact]
    public void RoundAHeartNarrowerThanAnyLookNothingGrows() => Assert.Equal(-1, Looks.DrawNoWider(PropKind.WildNature, 0.5f, 0.5f));

    /// <summary><b>A street is lined with a crowned look</b> (GEN-6f): of the planted set, the lime on its trunk and never the pot.</summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(0.999f)]
    public void AStreetIsLinedWithACrownedLook(float u) => Assert.Equal(4, Looks.DrawCrown(PropKind.UrbanNature, u));

    /// <summary><b>A kind with no crowned look lines nothing</b>: the furniture's bin stands as its whole girth.</summary>
    [Fact]
    public void AKindWithNoCrownedLookLinesNothing() => Assert.Equal(-1, Looks.DrawCrown(PropKind.UrbanFurniture, 0.5f));
}
