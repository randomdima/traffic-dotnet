using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>That every junction's ground is one closed outline</b> (<see cref="Paving.Boxes"/>): the line the
/// picture lays a box off once, so that the arms' sections stop at their cuts instead of running to the
/// node and crossing one another there.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P5)]
public class PavedBoxTests
{
    public static TheoryData<string> Maps => Towns.EveryMapWithAFootway();

    /// <summary>
    /// <b>Every junction three or more roads meet at has a box.</b> An outline that crosses itself is no box
    /// and the arms meet as they always did, so a junction without one is an outline that went wrong — and
    /// the way it went wrong at every crossroads was a step back along the kerb where two runs overlap at
    /// their weld, walked out, back and out again (<c>Boxes.TakeBackTheSteps</c>).
    /// </summary>
    /// <remarks>
    /// A junction one of whose arms is carried over water is not asked: a deck carries its own pavement
    /// and no run of the town's wraps it, so there is no kerb for the box's edge to be walked along.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryJunctionOfThreeOrMoreArmsHasABox(string map)
    {
        var plan = Towns.Of(map);
        var paving = plan.Paving(SimConfig.Shipped());
        var arms = new int[plan.Junctions.Count];
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            if (plan.Roads.SegmentsOf(road).Length == 0) continue;

            arms[plan.Roads.FromJunction[road]]++;
            arms[plan.Roads.ToJunction[road]]++;
        }

        var carried = new HashSet<int>();
        for (var bridge = 0; bridge < plan.Bridges.Count; bridge++)
        {
            var road = plan.Bridges.Road[bridge];
            if (road < 0) continue;

            carried.Add(plan.Roads.FromJunction[road]);
            carried.Add(plan.Roads.ToJunction[road]);
        }

        var boxed = new HashSet<int>();
        foreach (var box in paving.Boxes) boxed.Add(box.Junction);

        for (var junction = 0; junction < arms.Length; junction++)
        {
            if (arms[junction] < 3 || carried.Contains(junction)) continue;

            Assert.True(
                boxed.Contains(junction),
                $"{map}: junction {junction} at {plan.Junctions.CentreM[junction]} has {arms[junction]} arms and no box");
        }
    }

    /// <summary>
    /// <b>No piece of a box's outline runs back along the piece before it.</b> Two runs that carry on from
    /// one another overlap by up to a place at their weld, and the step between their kerbs is along the
    /// kerb rather than across it; laid into the outline, that stretch stands three times over and the
    /// outline crosses itself a hair off the line.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NoPieceOfAnOutlineRunsBackAlongTheOneBeforeIt(string map)
    {
        var plan = Towns.Of(map);
        var paving = plan.Paving(SimConfig.Shipped());
        foreach (var box in paving.Boxes)
        {
            var outline = box.Outline;
            for (var at = 0; at < outline.Length; at++)
            {
                var previous = outline[(at + outline.Length - 1) % outline.Length];
                var back = Vector2.Dot(outline[at].StartUnit, Heading.Unit(previous.HeadingAtRad(previous.LengthM)));
                Assert.True(
                    back > -0.95f,
                    $"{map}: junction {box.Junction}'s outline turns back on itself at {outline[at].StartM}, piece {at} of {outline.Length}");
            }
        }
    }
}
