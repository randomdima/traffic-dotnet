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

    /// <summary>Rule 2 as the rule actually reads: nothing, and not nearly nothing.</summary>
    [Theory]
    [InlineData(1, false)]
    [InlineData(1_000, false)]
    [InlineData(100, true)]
    [InlineData(1_000, true)]
    public void AStepAllocatesNothing(int bodyCount, bool packed)
    {
        var sample = SolverProbe.Sample(SimConfig.Shipped(), bodyCount, packed);

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
        var apart = SolverProbe.Sample(SimConfig.Shipped(), bodyCount: 1_000, packed: false);
        var packed = SolverProbe.Sample(SimConfig.Shipped(), bodyCount: 1_000, packed: true);

        Assert.Equal(0, apart.ContactPoints);
        Assert.True(packed.ContactPoints > 0, "the packed rig solved no contact points, so it measured an empty world");
    }

    [Fact]
    public void TheFigureIsFlatInTheSizeOfTheTown()
    {
        var config = SimConfig.Shipped();

        var few = SolverProbe.AllocatedBytesPerStep(config, bodyCount: 1);
        var many = SolverProbe.AllocatedBytesPerStep(config, bodyCount: 1_000);

        Assert.Equal(few, many);
    }
}
