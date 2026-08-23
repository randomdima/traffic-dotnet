using TrafficSimulation.Agents.Car.Maneuvers;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// The chain the planner hands a leg: that it is walked in order, that a step carries its own subject,
/// and that it is <b>bounded rather than grown</b> — a chain that does not fit is a plan that has stopped
/// being a skeleton.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class DrivePlanTests
{
    [Fact]
    public void TheStepsComeBackInTheOrderTheyWerePutIn()
    {
        var plan = new DrivePlan(cars: 2);
        plan.Add(car: 0, Maneuver.LeaveTheBay, subject: 7);
        plan.Add(car: 0, Maneuver.RunTheLine);
        plan.Add(car: 0, Maneuver.ParkInTheBay, subject: 12);

        Assert.Equal(3, plan.Left(0));
        Assert.Equal(new PlannedStep(Maneuver.LeaveTheBay, 7), plan.Take(0));
        Assert.Equal(new PlannedStep(Maneuver.RunTheLine, PlannedStep.NoSubject), plan.Take(0));
        Assert.Equal(new PlannedStep(Maneuver.ParkInTheBay, 12), plan.Take(0));
        Assert.Equal(0, plan.Left(0));
    }

    /// <summary><b>A spent chain answers nothing</b>, and the director reads that as `P-4` — the plan re-derived from where the car is.</summary>
    [Fact]
    public void ASpentChainHandsBackNothingRatherThanRepeating()
    {
        var plan = new DrivePlan(cars: 1);
        plan.Add(car: 0, Maneuver.RunTheLine);

        Assert.Equal(Maneuver.RunTheLine, plan.Take(0).Id);
        Assert.Equal(Maneuver.None, plan.Take(0).Id);
        Assert.Equal(Maneuver.None, plan.Take(0).Id);
    }

    /// <summary>One car's chain is not another's, which is the whole reason it is arrays and not a list.</summary>
    [Fact]
    public void EachCarWalksItsOwnChain()
    {
        var plan = new DrivePlan(cars: 3);
        plan.Add(car: 0, Maneuver.LeaveTheBay, subject: 1);
        plan.Add(car: 2, Maneuver.ParkInTheBay, subject: 2);

        Assert.Equal(Maneuver.LeaveTheBay, plan.Take(0).Id);
        Assert.Equal(0, plan.Left(1));
        Assert.Equal(new PlannedStep(Maneuver.ParkInTheBay, 2), plan.Take(2));
    }

    /// <summary>
    /// A reactive entry that hands a leg back needs the bay the plan was aiming at, and searching the
    /// chain for it beats carrying a second copy of it beside the chain.
    /// </summary>
    [Fact]
    public void TheSubjectOfAStepCanBeFoundWhereverInTheChainItStands()
    {
        var plan = new DrivePlan(cars: 1);
        plan.Add(car: 0, Maneuver.LeaveTheBay, subject: 4);
        plan.Add(car: 0, Maneuver.RunTheLine);
        plan.Add(car: 0, Maneuver.ParkInTheBay, subject: 9);

        Assert.Equal(9, plan.SubjectFor(0, Maneuver.ParkInTheBay));
        Assert.Equal(4, plan.SubjectFor(0, Maneuver.LeaveTheBay));
        Assert.Equal(PlannedStep.NoSubject, plan.SubjectFor(0, Maneuver.TurnAround));
    }

    /// <summary><b>Refused past the bound rather than grown.</b> The chain is a skeleton and a long one is a replan.</summary>
    [Fact]
    public void TheChainIsRefusedPastItsBound()
    {
        var plan = new DrivePlan(cars: 1);
        for (var step = 0; step < DrivePlan.StepsPerLeg; step++)
        {
            Assert.True(plan.Add(0, Maneuver.RunTheLine));
        }

        Assert.False(plan.Add(0, Maneuver.RunTheLine));
        Assert.Equal(DrivePlan.StepsPerLeg, plan.Left(0));
    }

    /// <summary>Clearing is what every replan starts with, and it takes the cursor with it.</summary>
    [Fact]
    public void ClearingDropsTheCursorAsWellAsTheSteps()
    {
        var plan = new DrivePlan(cars: 1);
        plan.Add(0, Maneuver.LeaveTheBay, 3);
        plan.Take(0);
        plan.Clear(0);
        plan.Add(0, Maneuver.RunTheLine);

        Assert.Equal(1, plan.Left(0));
        Assert.Equal(Maneuver.RunTheLine, plan.Next(0).Id);
    }
}
