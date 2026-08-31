using TrafficSimulation.Bench;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Ambulance;

/// <summary>
/// <b>AMB-5 to AMB-8 on a shipped town, end to end</b>: somebody is knocked down, an ambulance comes,
/// gets them aboard and puts them through a hospital's door. The one claim the whole slice exists to
/// make, and the one nothing used to assert.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is here because every cheaper tier passed while the rescue did not work at all.</b> The unit
/// tests had the stretcher, the roster and the priority right; the town tier had an ambulance answering a
/// call with its light on. Neither of them followed one casualty all the way to a door, and for as long as
/// that was true a shipped city could deliver nobody and no test said so.
/// </para>
/// <para>
/// <b>Asked of the probe rather than of a run of its own</b> (<see cref="RescueProbe"/>), so the gate and
/// the instrument cannot disagree about what a rescue is: the columns a person reads when they wonder why
/// the town feels wrong are the same numbers that fail this.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
public class RescueEndToEndTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>One staged rescue per map, taken once however many claims are asked of it.</summary>
    static RescueRow Of(string map) => Runs.GetOrAdd(map, at => RescueProbe.Sample(at, Config));

    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, RescueRow> Runs = new();

    /// <summary>
    /// <b>Every town that stands an ambulance delivers its casualty.</b> A map with no ambulance on it is
    /// a map with no bay near its hospitals (AMB-2) and is a real state, so it is skipped rather than
    /// failed — and skipped loudly, since a suite where every row skipped would pass while proving
    /// nothing.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void AStagedCasualtyIsCollectedAndDelivered(string map)
    {
        var row = Of(map);
        if (row.Ambulances == 0)
        {
            Assert.Equal(0, row.Hospitals * row.Ambulances);
            return;
        }

        Assert.True(
            row.Collected > 0,
            $"{map}: no ambulance got the casualty aboard — nearest {row.NearestM:F1} m, nearest at rest "
            + $"{row.NearestAtRestM:F1} m, {row.InReachS:F1} s within the crew's reach, doing "
            + $"{row.DoingThere} there, the crew's clock reached {row.MostLoadedS:F1} s");

        Assert.True(
            row.Delivered > 0,
            $"{map}: the casualty was collected in {row.ReachedInS:F1} s and never delivered — "
            + $"{row.GivenUp} calls given up, {row.DoorsFull} doors found full");
    }

    /// <summary>
    /// <b>And a rescue that takes longer than the errand's own bound is a rescue that did not work</b>
    /// (AMB-9). A delivery inside the leg bound is the honest reading of "an ambulance came": past it the
    /// call has been given up and re-taken at least once, which is a town that gets there eventually
    /// rather than a town with a rescue in it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void TheCasualtyIsReachedInsideOneLegsBound(string map)
    {
        var row = Of(map);
        if (row.Ambulances == 0) return;

        Assert.True(
            row.ReachedInS <= Config.AmbulanceGiveUpS,
            $"{map}: the casualty was reached in {row.ReachedInS:F1} s, and a leg is given up at "
            + $"{Config.AmbulanceGiveUpS:F1} s");
    }

    public static TheoryData<string> Maps => Towns.EveryTown();
}
