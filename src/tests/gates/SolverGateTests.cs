using System.Collections.Concurrent;
using TrafficSimulation.Bench;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Gates;

/// <summary>
/// What the solver allocates per step, and whether it grows with the town or with the contacts. This is
/// the gate the incumbent package failed and the reason this engine writes its own: <c>SOL-20</c> asks for
/// <em>nothing</em>, on a standing town, <b>including across contact churn as bodies touch and separate</b>.
/// </summary>
/// <remarks>
/// The packed rows are the ones with teeth. A world whose bodies never meet allocates nothing in almost
/// any solver, so a gate taken only on those would have passed the incumbent too; what finds a growing
/// array is a contact set that turns over, which is what the packed rows keep doing for nine hundred
/// steps.
/// </remarks>
[Trait(Tier.Key, Tier.Perf)]
[Collection(Simulation.SolverCollection.Name)]
public class SolverGateTests
{
    static SolverGateTests() => SolverProbe.WarmTheProcess(SimConfig.Shipped());

    static readonly ConcurrentDictionary<(int BodyCount, bool Packed), SolverProbe.StepSample> Taken = new();

    /// <summary>
    /// One rig per <c>(bodyCount, packed)</c>, taken once and read by whoever asks. <b>Four rigs answer
    /// eight questions here</b>, and the packed thousand is nine hundred steps of a churning contact set:
    /// taken per test it was twenty-two seconds of the gate tier spent measuring the same world twice.
    /// </summary>
    /// <remarks>
    /// Sharing the sample is what the tests mean anyway — the byte count and the contact count below are
    /// two readings of one measurement, and a run in which they came off different worlds could report
    /// zero bytes over a rig that solved nothing and call it a gate.
    /// </remarks>
    static SolverProbe.StepSample Sample(int bodyCount, bool packed) =>
        Taken.GetOrAdd((bodyCount, packed), rig => SolverProbe.Sample(SimConfig.Shipped(), rig.BodyCount, rig.Packed));

    /// <summary>Rule 2 as the rule actually reads: nothing, and not nearly nothing.</summary>
    [Theory]
    [InlineData(1, false)]
    [InlineData(1_000, false)]
    [InlineData(100, true)]
    [InlineData(1_000, true)]
    public void AStepAllocatesNothing(int bodyCount, bool packed)
    {
        var sample = Sample(bodyCount, packed);

        Assert.Equal(0d, sample.BytesPerStep);
        Assert.Equal(0, sample.Gen0Collections);
    }

    /// <summary>
    /// And the gate above has something to speak for. Zero bytes over a world that found no contacts is
    /// zero bytes about nothing, so the packed rows have to be shown to be solving contacts at all.
    /// </summary>
    [Fact]
    public void ThePackedRowsActuallySolveContacts()
    {
        var apart = Sample(bodyCount: 1_000, packed: false);
        var packed = Sample(bodyCount: 1_000, packed: true);

        Assert.Equal(0, apart.ContactPoints);
        Assert.True(packed.ContactPoints > 0, "the packed rig solved no contact points, so it measured an empty world");
    }

    [Fact]
    public void TheFigureIsFlatInTheSizeOfTheTown() =>
        Assert.Equal(Sample(bodyCount: 1, packed: false).BytesPerStep, Sample(bodyCount: 1_000, packed: false).BytesPerStep);
}
