using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>The outer shell of the driven ground</b> (<see cref="LaneShell"/>), asked the one thing a perimeter
/// is: that it goes round the town rather than in and out of it.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P4)]
public class LaneShellTests
{
    /// <summary>
    /// A hand-over past this has gone somewhere and come back. A right angle and a half: no junction of a
    /// town turns a corner that sharp, and what really does reverse — the back of a car park, the end of a
    /// one-way street's band — crosses its own straight square on rather than along it.
    /// </summary>
    const float SpikeRad = MathF.PI * 0.75f;

    /// <summary>
    /// <b>No corner the shell solved doubles back on itself.</b> A stretch is carried to where its own line
    /// crosses the one taking over, and whatever the corner came out as, whichever of its two ends runs
    /// against its own line's travel is cut back to the foot of the other — so where there was a crossing to
    /// carry to, the ring leaves one line and arrives at the next without running back down either. Carried
    /// to the point of one line <em>nearest</em> the other's stop instead, every skew junction in a town came
    /// back with a spike out of its corner.
    /// </summary>
    /// <remarks>
    /// <b>Asked of a whole town as well as of the fixture</b> (<see cref="Towns.City"/>): the fixture has no
    /// junction skew enough to spike, so asked of it alone the reading was clean while a city's was
    /// two thousand and a half.
    /// </remarks>
    [Theory]
    [InlineData(Towns.Fixture)]
    [InlineData(Towns.City)]
    public void NoHandOverDoublesBackOnItself(string map)
    {
        var config = SimConfig.Shipped();
        var reading = Towns.Of(map).Paving(config).Perimeter(config).Reading;

        foreach (var handed in reading.Handovers)
        {
            if (MathF.Abs(handed.TurnRad) <= SpikeRad) continue;

            // The three ways a ring may turn back on itself without the corner having been got wrong: it
            // turns at a point; it turns onto the line it was already on, which is the cap on the end of a
            // band and is drawn back down its own arcs (GEN-4b); or the two lines offered no corner to
            // carry to and what is drawn is the straight between two stops rather than a solved crossing.
            Assert.True(
                handed.Line == handed.OtherLine
                || handed.LengthM <= Kerbs.OnePlaceM
                || handed.Corner != ShellCorner.Crossed,
                $"line {handed.Line} hands over to {handed.OtherLine} at {handed.FromM} through a turn of "
                + $"{handed.TurnRad * 180f / MathF.PI:F0}° over {handed.LengthM:F2} m");
        }
    }

    /// <summary>
    /// <b>Every line the outside runs along is in a ring that was kept.</b> A run that will not close is
    /// thrown away whole (<see cref="LaneShell.Chains"/>), so one stretch with nowhere to carry the outside
    /// on to costs every lane its run walked — which is a length of the town with no perimeter drawn on it
    /// at all, and no reading but this one says so.
    /// </summary>
    [Theory]
    [InlineData(Towns.Fixture)]
    [InlineData(Towns.City)]
    public void EveryLineTheOutsideRunsAlongIsInARingThatShut(string map)
    {
        var config = SimConfig.Shipped();
        var reading = Towns.Of(map).Paving(config).Perimeter(config).Reading;
        var lost = reading.Lost();

        Assert.True(
            lost.Length == 0,
            $"{lost.Length} of {reading.Lines} driven lines are the outside somewhere and in no ring that "
            + $"shut, the first of them line {(lost.Length > 0 ? lost[0] : -1)}");
    }
}
