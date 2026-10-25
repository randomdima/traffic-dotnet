using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The air over the town (<see cref="Wind"/>) turns slowly — over minutes, and not from one second to the next — which way
/// and how hard apart, and carries what it holds by the air of every moment since.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P9)]
public class WindTests
{
    static readonly WindFigures Figures = new();

    static float BearingDeg(Wind wind, float elapsedS)
    {
        var mps = wind.AtMps(elapsedS);
        return float.RadiansToDegrees(MathF.Atan2(mps.Y, mps.X));
    }

    static float TurnedDeg(float fromDeg, float toDeg) => MathF.Abs(((toDeg - fromDeg + 540f) % 360f) - 180f);

    /// <summary>
    /// Over any second the wind's bearing moves by under two degrees, and over a quarter of its swing's period it has moved
    /// by a good part of the swing.
    /// </summary>
    [Fact]
    public void TheWindTurnsOverMinutesAndNotOverSeconds()
    {
        var wind = new Wind(Figures, seed: 7);
        var mostInASecondDeg = 0f;
        var mostInAQuarterDeg = 0f;
        for (var at = 0f; at < Figures.TurnPeriodS; at += 1f)
        {
            mostInASecondDeg = MathF.Max(mostInASecondDeg, TurnedDeg(BearingDeg(wind, at), BearingDeg(wind, at + 1f)));
            mostInAQuarterDeg = MathF.Max(mostInAQuarterDeg, TurnedDeg(BearingDeg(wind, at), BearingDeg(wind, at + (Figures.TurnPeriodS / 4f))));
        }

        Assert.True(mostInASecondDeg < 2f,$"it turned {mostInASecondDeg:F2}° in a second");
        Assert.True(mostInAQuarterDeg > Figures.TurnDeg / 2f, $"it turned at most {mostInAQuarterDeg:F1}° in a quarter period");
    }

    /// <summary>Which way the wind blows is moved by none of the gust's figures, and how hard by none of the turn's.</summary>
    [Fact]
    public void WhichWayAndHowHardAnswerToNothingOfEachOther()
    {
        var wind = new Wind(Figures, seed: 7);
        var gustier = new Wind(new WindFigures { GustShare = Figures.GustShare * 2f, GustPeriodS = Figures.GustPeriodS / 3f }, seed: 7);
        var swingier = new Wind(new WindFigures { TurnDeg = Figures.TurnDeg / 2f, TurnPeriodS = Figures.TurnPeriodS / 5f }, seed: 7);
        for (var at = 0f; at < Figures.TurnPeriodS; at += 7f)
        {
            Assert.Equal(wind.BearingRad(at), gustier.BearingRad(at));
            Assert.Equal(wind.SpeedMps(at), swingier.SpeedMps(at));
        }
    }

    /// <summary>
    /// How far the air has carried since each moment back is its velocity over the time since, to a centimetre, so a puff
    /// is carried by the air it was out in and not by the air of now for as long.
    /// </summary>
    [Fact]
    public void TheCarryBackToAMomentIsTheAirSinceIt()
    {
        var wind = new Wind(Figures, seed: 7);
        const float NowS = 400f;
        const float StepS = 0.5f;
        const int Fine = 1000;
        Span<AirBack> back = stackalloc AirBack[32];
        wind.Back(NowS, StepS, back);
        for (var moment = 1; moment < back.Length; moment++)
        {
            var sinceS = moment * StepS;
            var sinceM = System.Numerics.Vector2.Zero;
            for (var slice = 0; slice < Fine; slice++) sinceM += wind.AtMps(NowS - ((slice + 0.5f) * sinceS / Fine)) * (sinceS / Fine);
            Assert.True((back[moment].SinceM - sinceM).Length() < 0.01f, $"{sinceS} s back it carried {back[moment].SinceM}, the air {sinceM}");
            Assert.Equal(wind.AtMps(NowS - sinceS), back[moment].Mps);
        }
    }

    /// <summary>Two towns of different seeds blow from different ways.</summary>
    [Fact]
    public void TwoTownsBlowTheirOwnWays()
    {
        Assert.NotEqual(BearingDeg(new Wind(Figures, seed: 1), 0f), BearingDeg(new Wind(Figures, seed: 2), 0f));
    }
}
