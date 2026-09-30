using System.Numerics;
using TrafficSimulation.App.Debug;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.Debug;

/// <summary>
/// Which of the lines crossing one stone of the town's grid actually carries a mark
/// (<see cref="MarkClaims"/>). The grid says where a mark may fall; this says that what stands there is
/// one mark and not the blot a junction's fan of movements would otherwise leave.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P9)]
public class MarkClaimTests
{
    const float ApartM = 1f;

    static readonly WorldGrid Grid = new(8f);

    static readonly Vector2 CentreM = new(120f, 40f);

    static MarkClaims Cleared()
    {
        var claims = new MarkClaims();
        claims.Clear(Grid, CentreM, new Vector2(60f, 40f), ApartM);
        return claims;
    }

    /// <summary>The one property it exists for: nothing saying what a mark says stands within the spacing of it.</summary>
    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0.9f, 0f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(0f, -0.99f)]
    public void NothingSayingTheSameThingStandsWithinTheSpacing(float acrossM, float downM)
    {
        var claims = Cleared();
        var alongM = new Vector2(0.6f, 0.8f);

        Assert.True(claims.Take(CentreM, alongM));
        Assert.False(claims.Take(CentreM + new Vector2(acrossM, downM), alongM));
    }

    /// <summary>And past the spacing it stands, however the first one was laid.</summary>
    [Theory]
    [InlineData(1.01f, 0f)]
    [InlineData(0f, 1.01f)]
    [InlineData(-0.8f, -0.8f)]
    public void PastTheSpacingAMarkStands(float acrossM, float downM)
    {
        var claims = Cleared();
        var alongM = new Vector2(0.6f, 0.8f);

        Assert.True(claims.Take(CentreM, alongM));
        Assert.True(claims.Take(CentreM + new Vector2(acrossM, downM), alongM));
    }

    /// <summary>
    /// <b>But a mark saying the opposite thing stands on the same stone</b>: a bay's two lanes are one piece
    /// of ground, and their chevrons crossing is the whole of what tells the pair apart.
    /// </summary>
    [Fact]
    public void AMarkSayingTheOppositeThingStandsOnTheSameStone()
    {
        var claims = Cleared();
        var alongM = new Vector2(0.6f, 0.8f);

        Assert.True(claims.Take(CentreM, alongM));
        Assert.True(claims.Take(CentreM, -alongM));
    }

    /// <summary>
    /// <b>A pass with no spacing to keep refuses nothing</b> — which is what an agent's own two pieces of
    /// route ask for, being one line and no fan, and what a framing too far out to draw a mark at all
    /// leaves the layer holding.
    /// </summary>
    [Fact]
    public void APassWithNoSpacingRefusesNothing()
    {
        var alongM = new Vector2(0.6f, 0.8f);

        Assert.True(MarkClaims.None.Take(CentreM, alongM));
        Assert.True(MarkClaims.None.Take(CentreM, alongM));
    }

    /// <summary>
    /// And <b>what a pass refused is forgotten when it is cleared</b>, the layer being laid again whenever
    /// the camera moves and every mark of the new pass being drawn from nothing.
    /// </summary>
    [Fact]
    public void ClearingAPassForgetsWhatItRefused()
    {
        var claims = Cleared();
        var alongM = new Vector2(0.6f, 0.8f);

        Assert.True(claims.Take(CentreM, alongM));
        claims.Clear(Grid, CentreM, new Vector2(60f, 40f), ApartM);

        Assert.True(claims.Take(CentreM, alongM));
    }
}
