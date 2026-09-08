using System.Numerics;
using TrafficSimulation.Agents.Ambulance;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Ambulance;

/// <summary>
/// <b>No vehicle is left standing with its crew in the street</b> (SRV-3). A hand out is a vehicle stopped,
/// so a hand that never comes back is a hospital, a station or a depot one vehicle short for the rest of the
/// run — which is the one way this whole errand can quietly take a town apart.
/// </summary>
/// <remarks>
/// <para>
/// <b>The casualty is staged and the tail is not.</b> Whether a town knocks one of its own people down
/// inside ten minutes is a fact about how crowded it is, so a run that waited for one asserted nothing on a
/// town with room in it — the errand is ordered here the way the probe orders it, and <em>what the town does
/// with the order</em> is what is watched: the scene nothing clears, the pavement that will not give a body
/// back, the call given up while somebody was out. The ceiling asserted is every bound this can legitimately
/// spend, added up.
/// </para>
/// <para>
/// <b>A class of its own because it is ten minutes of a city and the only claim that needs them.</b> The
/// cases of one class are run one after another, so left beside <see cref="CrewOnFootTests"/>'s staged
/// rescue this one stood the whole slice's answer behind it on a machine with fifteen idle cores.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P2)]
public class CrewRecallTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// Half again the longest a hand may honestly be out for, so a breach has room to show — <b>derived from
    /// that ceiling and not written down</b>, because a flat ten minutes was two thirds of it spent watching
    /// an errand that had already come back.
    /// </summary>
    static int Ticks => WarmupTicks + (int)MathF.Ceiling(CeilingS * 1.5f / Config.TickSeconds);

    /// <summary>Long enough for everybody's first dwell to be up, so there is somebody in the street to stage.</summary>
    const int WarmupTicks = 600;

    /// <summary>
    /// Every leg a hand can be out for, end to end: the longest errand bound any of the three can be holding
    /// it for, the recall after that, and the one decision it takes to notice either is spent — every one of
    /// these clocks is read on the vehicle's own decision and can only be found over on the decision after it
    /// went over. <b>Derived and not written down</b>, so moving a figure moves the ceiling with it rather
    /// than turning this into a test of what the figures used to be.
    /// </summary>
    /// <remarks>
    /// The last term is the tick this is counted at. The bounds are clocks the vehicle reads on its own
    /// decision; what is measured is the number of ticks a hand was seen out, which is up to one tick longer
    /// than the span itself. Without it the case fails on where the ticks happen to fall (VER-12).
    /// </remarks>
    static float CeilingS =>
        MathF.Max(
            Config.AmbulanceGiveUpS,
            MathF.Max(Config.EvacuatorGiveUpS, MathF.Max(Config.PatrolGiveUpS, Config.PoliceClosureLifeS)))
        + Config.ServiceRecallS
        + Config.Sim.AgentDecisionIntervalS
        + Config.TickSeconds;

    [Fact]
    public void NoHandIsLeftInTheStreetLongerThanEveryBoundTogether()
    {
        using var world = new TownWorld(Towns.Of(Towns.City), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        loop.Advance(WarmupTicks);
        var casualty = Towns.NearestWalkerToARoad(world);
        Assert.True(casualty >= 0, "the town had nobody to knock down");
        world.Apply(new BodyTag(BodyKind.Person, casualty), DamageOutcome.Wounded);

        var outS = new float[world.Cars.Count];
        var everOut = false;
        var longestS = 0f;

        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance(1);
            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (world.HandOutOf(car) < 0)
                {
                    outS[car] = 0f;
                    continue;
                }

                everOut = true;
                outS[car] += Config.TickSeconds;
                longestS = MathF.Max(longestS, outS[car]);
            }
        }

        Assert.True(everOut, "no crew was ever out on this run, so it says nothing about getting one back");
        Assert.True(
            longestS <= CeilingS,
            $"a vehicle stood {longestS:F1} s with its crew in the street, against {CeilingS:F1} s of bounds "
            + "— the errand's and the recall's together (SRV-3, AMB-9)");
    }
}
