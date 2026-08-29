using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Bench;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Evacuator;

/// <summary>
/// <b>EVA-3 to EVA-7 on a shipped town, end to end</b>: a car is wrecked, an evacuator comes, gets it onto
/// the bar and drags it away — and the coupling holds while it does.
/// </summary>
/// <remarks>
/// <para>
/// <b>Asked of the probe rather than of a run of its own</b> (<see cref="RecoveryProbe"/>), so the gate and
/// the instrument cannot disagree about what a recovery is: the columns a person reads when they wonder why
/// the town feels wrong are the same numbers that fail this.
/// </para>
/// <para>
/// <b>What is asserted is the tow and not the arrival</b>, and the difference is the honest one. Whether a
/// dense city's geometry lets a nine-metre articulated pair get all the way home is a reading about that
/// city (EVA-8), and the probe's own row is where it belongs; what must hold everywhere is that a recovery
/// that begins is a recovery that actually drags something, on a bar that stays a bar.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
public class RecoveryEndToEndTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>One staged recovery per map, taken once however many claims are asked of it.</summary>
    static RecoveryRow Of(string map) => Runs.GetOrAdd(map, at => RecoveryProbe.Sample(at, Config));

    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, RecoveryRow> Runs = new();

    /// <summary>
    /// <b>The fixture town runs the whole of it</b> (EVA-6, EVA-7): fetched, towed, set down in a slot and
    /// mended. It is the one map small enough that a tow's geometry is never the answer, which is what makes
    /// it the map this claim can be made of.
    /// </summary>
    [Fact]
    public void TheFixtureTownFetchesTowsYardsAndMendsAWreck()
    {
        var row = Of(Towns.Fixture);

        Assert.True(row.Hitched > 0, $"nothing was hitched — nearest {row.NearestM:F1} m, doing {row.DoingThere} there");
        Assert.True(row.Yarded > 0, $"a wreck was towed {row.TowedM:F1} m and never set down — ended in {row.EndedIn}");
        Assert.True(row.Restored > 0, $"a wreck was yarded at {row.YardedInS:F1} s and never mended");
        Assert.True(
            row.RestoredInS - row.YardedInS >= Config.Evacuator.RepairS - 1f,
            $"a wreck was mended in {row.RestoredInS - row.YardedInS:F1} s of a {Config.Evacuator.RepairS:F0} s repair");
    }

    /// <summary>
    /// <b>EVA-5: a tow drags something, and the arm stays an arm.</b> A recovery that reports a hitch and no
    /// distance is a coupling that did not couple; one that reports metres of stretch is a rope.
    /// </summary>
    /// <remarks>
    /// <b>Read off the same staged recovery as the claim above</b>, and asked of that one map for the reason
    /// the row itself gives: no shipped city hitches a wreck inside the probe's own four hundred seconds, so
    /// a sweep of every map spent almost all of a run producing seven rows with nothing in them and returned
    /// before asserting anything of any of them. What a city's recovery comes to is the probe's row and the
    /// claim printed under it (<c>--bench recovery</c>), which is where a reading about a city belongs.
    /// </remarks>
    [Fact]
    public void AStagedWreckIsHitchedAndDragged()
    {
        var row = Of(Towns.Fixture);

        Assert.True(
            row.TowedM > Config.Car.LengthM,
            $"a wreck was hitched at {row.ReachedInS:F1} s and dragged {row.TowedM:F1} m — "
            + $"nearest {row.NearestM:F1} m, doing {row.DoingThere} there, ended in {row.EndedIn}");

        // A quarter of the arm's own reach: past that the picture and the physics have visibly parted company,
        // since the fork is drawn at the reach and the wreck is wherever the coupling has actually left it.
        var reachM = CarBuild.Of(Config, CarCatalog.Shared.Variants[CarCatalog.Shared.Evacuator]).TowReachM;
        Assert.True(
            row.WorstStretchM <= reachM * 0.25f,
            $"the arm stretched {row.WorstStretchM:F2} m off a reach of {reachM:F2} m");
    }
}
