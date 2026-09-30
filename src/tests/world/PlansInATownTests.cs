using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>A driver's plan as a running town keeps it from one tick to the next</b> (TER-4c.1).
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P3)]
public class PlansInATownTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// <b>A plan nothing cut never draws back</b> (TER-4c.1): while nothing holds a car short, the far end of what it
    /// plans moves on with it or stands. What it will do on the road ahead — a bend it will slow for among it — was
    /// already in the plan that reached there, so the car slowing for it takes nothing back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing cut it for as long as a full brake takes to come off the pedal</b>: a car held short by a light
    /// goes on slowing for a moment after the light lets it go, and that is the light's doing.
    /// </para>
    /// <para>
    /// <b>And its front moved on</b>: a car read further back along its line than it was is where it is being read
    /// and not what it plans, and its plan follows its front.
    /// </para>
    /// <para>Read in the frame each plan was laid in: a car taking its next lane re-bases its line on that lane's nought.</para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void APlanNothingCutNeverDrawsBack(string map)
    {
        using var world = new TownWorld(Towns.Of(map), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        var cars = world.Cars;
        var was = new Laid[cars.Count];
        var uncutS = new float[cars.Count];

        for (var tick = 0; tick < TicksWatched; tick++)
        {
            var frame = new Laid[cars.Count];
            for (var car = 0; car < frame.Length; car++) frame[car] = FrameOf(cars, car);
            if (was.Length < frame.Length)
            {
                Array.Resize(ref was, frame.Length);
                Array.Resize(ref uncutS, frame.Length);
            }

            loop.Advance();
            for (var car = 0; car < frame.Length; car++)
            {
                var uncut = float.IsPositiveInfinity(cars.AuthorityM[car]) && cars.ClaimToM[car] > cars.ClaimFromM[car]
                                                                          && !cars.Pass[car].Any;
                var now = frame[car] with { FromM = cars.ClaimFromM[car], ToM = cars.ClaimToM[car] };
                ref readonly var build = ref cars.BuildOf(car);
                var releasedS = build.BrakingMps2 / build.PedalRateMps3;

                if (uncut && uncutS[car] >= releasedS
                    && InOneFrame(was[car], now, out var shiftM) && now.FromM >= was[car].FromM - shiftM)
                {
                    Assert.True(
                        now.ToM >= was[car].ToM - shiftM - Tolerance,
                        $"{map}: tick {tick}, car {car} drew its plan back {was[car].ToM - shiftM - now.ToM:0.00} m with "
                        + $"nothing cutting it, doing {cars.AlongMps[car]:0.0} m/s");
                }

                uncutS[car] = uncut ? uncutS[car] + Config.TickSeconds : 0f;
                was[car] = now;
            }
        }
    }

    /// <summary>
    /// A car's plan and the line it was laid on: whether that is a piece of a manoeuvre, the first lane its metres
    /// count from, and the next lane's nought.
    /// </summary>
    readonly record struct Laid(bool Manoeuvre, int First, int Next, float NextStartM, float FromM = 0f, float ToM = 0f);

    static Laid FrameOf(CarFleet cars, int car)
    {
        var lanes = cars.Line[car].LaneCount;
        return new Laid(
            lanes == 0, lanes > 0 ? cars.ChainOf(car)[0] : -1, lanes > 1 ? cars.ChainOf(car)[1] : -1,
            lanes > 1 ? cars.LaneStartsOf(car)[1] : 0f);
    }

    /// <summary>Whether two plans were laid on one line, and how far the second's metres are moved on from the first's.</summary>
    static bool InOneFrame(in Laid was, in Laid now, out float shiftM)
    {
        shiftM = 0f;
        if (was.Manoeuvre || was.Manoeuvre != now.Manoeuvre) return false;
        if (was.First == now.First) return true;

        shiftM = was.NextStartM;
        return now.First >= 0 && now.First == was.Next;
    }

    /// <summary>Ground on a way is metres, and a plan is arithmetic on floats: a centimetre is not a finding.</summary>
    const float Tolerance = 1e-2f;

    public static TheoryData<string> Maps => Towns.EveryTown();

    /// <summary>A minute of town: long enough for every car to have come up to a bend and a junction.</summary>
    const int TicksWatched = 3_600;
}
