using TrafficSimulation.App.Render;
using TrafficSimulation.Runtime;
using Xunit;

namespace TrafficSimulation.Tests.Render;

/// <summary>
/// The dark line a prop's picture is ringed with as it is packed (<see cref="SheetOutline"/>): over the fringe beside its
/// solid part, and nowhere else.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P9)]
public class SheetOutlineTests
{
    static readonly Texel Solid = new(200, 40, 40, 255);
    static readonly Texel Clear = new(0, 0, 0, 0);

    [Fact]
    public void TheClearTexelBesideASolidOneTurnsBlack()
    {
        Texel[] pixels = [Solid, Clear, Clear, Clear];
        SheetOutline.Ring(pixels, 4, 1, 1f);
        Assert.Equal(new Texel(0, 0, 0, 255), pixels[1]);
    }

    [Fact]
    public void TheSolidPartIsLeftAsItIs()
    {
        Texel[] pixels = [Solid, Clear, Clear, Clear];
        SheetOutline.Ring(pixels, 4, 1, 1f);
        Assert.Equal(Solid, pixels[0]);
    }

    [Fact]
    public void ATexelPastTheLineAndItsFadeIsLeftClear()
    {
        Texel[] pixels = [Solid, Clear, Clear, Clear];
        SheetOutline.Ring(pixels, 4, 1, 1f);
        Assert.Equal(Clear, pixels[2]);
    }

    [Fact]
    public void AFaintPictureIsNotRinged()
    {
        var faint = new Texel(40, 120, 40, 100);
        Texel[] pixels = [faint, Clear];
        SheetOutline.Ring(pixels, 2, 1, 1f);
        Assert.Equal([faint, Clear], pixels);
    }
}
