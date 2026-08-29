using System.Numerics;
using TrafficSimulation.Agents.TrafficLight.Body;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.Agents.TrafficLight;

/// <summary>
/// The cycle table and the heads, asked of every shipped map with no town stood up. <b>The table's
/// shape is the safety argument</b> (TLT-4), so what is asserted here is that the shape holds across a
/// whole cycle and not that some sampled instant looked right.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class SignalTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    public static TheoryData<string> Maps => Towns.EveryShippedMap();

    /// <summary>A whole cycle, finely enough that no phase edge is stepped over.</summary>
    static IEnumerable<float> WholeCycle()
    {
        for (var step = 0; step <= 600; step++) yield return Config.Signals.CycleS * step / 600f;
    }

    [Fact]
    public void OneAxisIsGreenAtATimeAndTheAmberIsTheEndOfItsOwnGreen()
    {
        var amberS = 0f;
        var greenS = 0f;
        var stepS = Config.Signals.CycleS / 600f;

        foreach (var atS in WholeCycle())
        {
            var first = SignalCycle.ForAxis(Config, 0, 0f, atS);
            var second = SignalCycle.ForAxis(Config, 1, 0f, atS);

            Assert.True(first == SignalColour.Red || second == SignalColour.Red,
                $"both axes show {first} and {second} at {atS:F2} s");
            Assert.False(first == SignalColour.Red && second == SignalColour.Red,
                $"there is no all-red phase, and at {atS:F2} s both axes are red");

            if (first == SignalColour.Amber) amberS += stepS;
            if (first == SignalColour.Green) greenS += stepS;
        }

        // Half the cycle an axis, of which the last stretch is amber — time taken out of the green
        // rather than added to it.
        Assert.Equal(Config.Signals.AmberTailS, amberS, 1);
        Assert.Equal((Config.Signals.CycleS / 2f) - Config.Signals.AmberTailS, greenS, 1);
    }

    /// <summary>A crossing is green exactly while its own road is fully red — amber included — and never amber itself.</summary>
    [Fact]
    public void ACrossingIsTheNegationOfItsOwnRoad()
    {
        foreach (var atS in WholeCycle())
        {
            for (var axis = 0; axis < SignalCycle.Axes; axis++)
            {
                var road = SignalCycle.ForAxis(Config, axis, 3.25f, atS);
                var crossing = SignalCycle.ForCrossing(Config, axis, 3.25f, atS);

                Assert.NotEqual(SignalColour.Amber, crossing);
                Assert.Equal(road == SignalColour.Red, crossing == SignalColour.Green);
            }
        }
    }

    /// <summary>
    /// <b>No two conflicting arms are ever green together</b>, over a whole cycle and over every lit
    /// junction of every shipped map — and both ends of one road always agree, because they are one
    /// axis rather than two directions that happen to be timed alike.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NoLitJunctionEverShowsTwoConflictingGreens(string map)
    {
        var plan = Towns.Of(map);
        var roads = RoadGraph.Build(plan, Config);
        var signals = SignalService.Build(plan, roads, Config);

        var lit = 0;
        for (var junction = 0; junction < roads.NodeCount; junction++)
        {
            if (!signals.Lit(junction)) continue;

            lit++;
            var arms = roads.LanesIn(junction);
            foreach (var atS in WholeCycle())
            {
                foreach (var arm in arms)
                {
                    foreach (var other in arms)
                    {
                        if (signals.AxisOfLane(arm) == signals.AxisOfLane(other)) continue;

                        var here = signals.ForApproach(arm, atS);
                        var there = signals.ForApproach(other, atS);
                        Assert.True(here == SignalColour.Red || there == SignalColour.Red,
                            $"{map}: junction {junction} shows {here} and {there} on conflicting arms at {atS:F2} s");
                    }
                }
            }

            // Two arms of one road arrive from opposite ends, and a road whose two ends disagreed would
            // green one half of a street against the other.
            foreach (var arm in arms)
            {
                foreach (var other in arms)
                {
                    if (roads.LaneRoad[arm] != roads.LaneRoad[other]) continue;

                    Assert.Equal(signals.AxisOfLane(arm), signals.AxisOfLane(other));
                }
            }
        }

        // Every junction the map lights that admits movements to conflict, and no other (TLT-3). The
        // crossing scenario map lights nothing on purpose, and every town has places where a road is
        // merely cut — a dead end, a mid-block crossing — whose crossings are the give-way rule at the
        // kerb rather than a bundle (TER-5e).
        var conflicting = 0;
        for (var junction = 0; junction < roads.NodeCount; junction++)
        {
            if (junction < plan.Junctions.Count && plan.Junctions.Lit[junction] && roads.LanesIn(junction).Length >= 3)
            {
                conflicting++;
            }
        }

        Assert.Equal(conflicting, lit);
    }

    /// <summary>
    /// TLT-3's placement: a crossing on a lit junction is governed, an approach at an unlit junction is
    /// shown green because there is nothing there to obey, and the offsets are the plan's own.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void OnlyALitJunctionGovernsAnythingAndItsOffsetIsTheTownsOwn(string map)
    {
        var plan = Towns.Of(map);
        var roads = RoadGraph.Build(plan, Config);
        var signals = SignalService.Build(plan, roads, Config);

        for (var junction = 0; junction < roads.NodeCount; junction++)
        {
            if (signals.Lit(junction)) continue;

            foreach (var arm in roads.LanesIn(junction))
            {
                Assert.Equal(SignalService.NoAxis, signals.AxisOfLane(arm));
                Assert.Equal(SignalColour.Green, signals.ForApproach(arm, 4.75f));
            }
        }

        for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
        {
            var junction = plan.Crosswalks.Junction[crossing];
            Assert.Equal(signals.Lit(junction), signals.CrossingIsLit(crossing));

            // An unlit crossing shows red: nothing is telling a walker it may go, and a green there
            // would be a permission with no bundle behind it.
            if (!signals.CrossingIsLit(crossing)) Assert.Equal(SignalColour.Red, signals.ForCrossing(crossing, 4.75f));
        }

        var offsets = plan.Junctions.PhaseOffsetS;
        foreach (var offsetS in offsets) Assert.True(float.IsFinite(offsetS), $"{map} carries a phase offset of {offsetS}");
    }

    /// <summary>
    /// The heads: one per painted bar at a lit junction, two per lit crossing, all of them apart from
    /// one another, and every one of them showing exactly one lamp at every moment of a cycle.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryHeadIsPlacedOnceAndAlwaysShowsExactlyOneLamp(string map)
    {
        var plan = Towns.Of(map);
        var roads = RoadGraph.Build(plan, Config);
        var signals = SignalService.Build(plan, roads, Config);
        var heads = SignalHeads.Place(plan, roads, signals, Config);

        var litCrossings = 0;
        for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
        {
            if (signals.CrossingIsLit(crossing)) litCrossings++;
        }

        var carHeads = heads.Heads.Count(head => head.ForCars);
        var walkHeads = heads.Count - carHeads;
        Assert.Equal(litCrossings * 2, walkHeads);

        // Every painted bar at a lit junction carries one head, and nothing else does.
        var governedBars = 0;
        for (var bar = 0; bar < plan.StopLines.Count; bar++)
        {
            if (signals.Lit(plan.StopLines.Junction[bar])) governedBars++;
        }

        Assert.Equal(governedBars, carHeads);

        // No head stands on top of another. A pair a centimetre apart is two bundles claiming one arm,
        // which reads on a picture as one head and in the table as two.
        var places = new HashSet<(int X, int Y)>();
        foreach (var head in heads.Heads)
        {
            Assert.True(places.Add(((int)MathF.Round(head.CentreM.X * 10f), (int)MathF.Round(head.CentreM.Y * 10f))),
                $"{map}: two heads stand at {head.CentreM}");
        }

        foreach (var head in heads.Heads)
        {
            foreach (var atS in WholeCycle())
            {
                var colour = head.ForCars
                    ? signals.ForApproach(head.Subject, atS)
                    : signals.ForCrossing(head.Subject, atS);

                if (!head.ForCars) Assert.NotEqual(SignalColour.Amber, colour);
            }
        }
    }

    /// <summary>
    /// <b>Every walker shown a green is being crossed by nobody.</b> Which arms those are is asked of
    /// the ground rather than of a bearing: an arm is over the paint if its own driven line runs through
    /// the crossing's rectangle, which is the only definition that survives a junction whose arms are
    /// not square to one another.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void AGreenCrossingIsDrivenOverByNobody(string map)
    {
        var plan = Towns.Of(map);
        var roads = RoadGraph.Build(plan, Config);
        var signals = SignalService.Build(plan, roads, Config);

        for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
        {
            if (!signals.CrossingIsLit(crossing)) continue;

            var junction = plan.Crosswalks.Junction[crossing];
            foreach (var arm in roads.LanesIn(junction))
            {
                if (!RunsOverThePaint(plan, roads, arm, crossing)) continue;

                foreach (var atS in WholeCycle())
                {
                    if (signals.ForCrossing(crossing, atS) != SignalColour.Green) continue;

                    Assert.Equal(SignalColour.Red, signals.ForApproach(arm, atS));
                }
            }
        }
    }

    /// <summary>Whether a lane's own line passes through a crossing's rectangle on its way to the junction.</summary>
    static bool RunsOverThePaint(CityPlan plan, RoadGraph roads, int lane, int crossing)
    {
        var centreM = plan.Crosswalks.CentreM[crossing];
        var along = Vector2.Normalize(plan.Crosswalks.Axis[crossing]);
        var halfDepthM = plan.Crosswalks.DepthM[crossing] * 0.5f;
        var halfSpanM = plan.Crosswalks.SpanM[crossing] * 0.5f;

        var arcs = roads.ArcsOf(lane);
        var lengthM = roads.LaneLengthM[lane];
        for (var atM = MathF.Max(0f, lengthM - 40f); atM <= lengthM; atM += 0.25f)
        {
            var offset = Spline.SampleAt(arcs, atM).PositionM - centreM;
            var down = MathF.Abs(Vector2.Dot(offset, along));
            var across = MathF.Abs((offset.X * -along.Y) + (offset.Y * along.X));
            if (down <= halfDepthM && across <= halfSpanM) return true;
        }

        return false;
    }
}
