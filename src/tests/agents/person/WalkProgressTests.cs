using TrafficSimulation.Agents.Person.Body;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Person;

/// <summary>
/// PER-25's give-up clock, asked of the clock itself: what counts as getting somewhere, and what a body
/// that is not getting anywhere costs before its leg is taken away.
/// </summary>
/// <remarks>
/// <b>The engine-free half of the rule.</b> What the town feeds this is a distance that shrinks as the
/// walk goes well (<c>TownWorld.RemainingOnTheWalkM</c>); what the clock owes in return is that a walk
/// going well never runs it out and a body going nowhere always does.
/// </remarks>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P4)]
public class WalkProgressTests
{
    const float PatienceS = 20f;

    const float DecisionS = 0.5f;

    /// <summary>A body's own width, which is what the town says counts as having got somewhere.</summary>
    const float ByM = 1f;

    [Fact]
    public void AWalkerClosingOnItsLegIsNeverStuck()
    {
        var progress = new WalkProgress(1);
        progress.Restart(0);

        for (var remainingM = 100f; remainingM > 0f; remainingM -= 3f)
        {
            progress.Note(0, remainingM, ByM, DecisionS);
            Assert.False(progress.IsStuck(0, PatienceS), $"a walker with {remainingM:F0} m left was called stuck");
        }
    }

    [Fact]
    public void AWalkerThatCloseOnNothingRunsThePatienceOut()
    {
        var progress = new WalkProgress(1);
        progress.Restart(0);

        for (var spentS = 0f; spentS <= PatienceS; spentS += DecisionS) progress.Note(0, 40f, ByM, DecisionS);

        Assert.True(progress.IsStuck(0, PatienceS));
    }

    /// <summary>
    /// <b>And rocking where it stands is not closing on anything.</b> A body being leaned on wanders by a
    /// fraction of itself, so a clock kept to the millimetre takes each wander for progress and never runs
    /// out for the one case it exists to end — a walker held against something that will not move.
    /// </summary>
    [Fact]
    public void AWalkerRockingWhereItStandsRunsThePatienceOut()
    {
        var progress = new WalkProgress(1);
        progress.Restart(0);

        var wander = 0;
        for (var spentS = 0f; spentS <= PatienceS; spentS += DecisionS)
        {
            progress.Note(0, 40f - (ByM * 0.1f * (wander++ % 4)), ByM, DecisionS);
        }

        Assert.True(progress.IsStuck(0, PatienceS));
    }

    /// <summary>A leg laid again is a fresh one, and the time the last one spent getting nowhere is not its.</summary>
    [Fact]
    public void ARestartGivesTheLegItsPatienceBack()
    {
        var progress = new WalkProgress(1);
        progress.Restart(0);

        for (var spentS = 0f; spentS <= PatienceS; spentS += DecisionS) progress.Note(0, 40f, ByM, DecisionS);
        Assert.True(progress.IsStuck(0, PatienceS));

        progress.Restart(0);
        Assert.False(progress.IsStuck(0, PatienceS));
    }
}
