using TrafficSimulation.Multiplayer.Server;
using Xunit;

namespace TrafficSimulation.Tests.Multiplayer.Server;

/// <summary>MP-12, MP-15: <b>a player's colour is never a grey, a white or a black</b>, and one vivid already is kept.</summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P8)]
public class SeatColourTests
{
    const float LeastSaturation = 0.5f;
    const float LeastValue = 0.6f;

    [Theory]
    [InlineData(0x80_80_80u)]
    [InlineData(0xFF_FF_FFu)]
    [InlineData(0x00_00_00u)]
    public void AColourWithoutHueOrLightIsRaisedToTheLeast(uint asked)
    {
        var (saturation, value) = SaturationAndValue(SeatColour.Vivid(asked, LeastSaturation, LeastValue));

        Assert.True(saturation >= LeastSaturation - 0.01f && value >= LeastValue - 0.01f, $"{saturation}, {value}");
    }

    [Fact]
    public void AVividColourIsKept() => Assert.Equal(0x20_C0_FFu, SeatColour.Vivid(0x20_C0_FF, LeastSaturation, LeastValue));

    static (float Saturation, float Value) SaturationAndValue(uint rgb)
    {
        var r = (rgb >> 16) & 0xFF;
        var g = (rgb >> 8) & 0xFF;
        var b = rgb & 0xFF;
        var most = Math.Max(r, Math.Max(g, b)) / 255f;
        var least = Math.Min(r, Math.Min(g, b)) / 255f;
        return (most <= 0f ? 0f : (most - least) / most, most);
    }
}
