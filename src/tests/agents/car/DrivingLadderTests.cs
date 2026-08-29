using TrafficSimulation.Agents.Car.Maneuvers;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// The escalation ladder as arithmetic: which rung answers which state, which rungs are skipped, and
/// the one property the whole thing rests on — <b>it never stops early</b>.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class DrivingLadderTests
{
    /// <summary>A car with every door open to it: nothing in the way, a place held, a route, and ground it may stand on.</summary>
    static LadderState Ordinary => new(
        ObstructionHasPriority: false, SomethingToBackAwayFrom: false, RoomBehindM: 0f, BackOffsLeft: 2,
        InItsOwnBay: false, AtItsOwnBay: false, HoldsAPlace: true, OnARoute: true, ReroutesLeft: 3,
        AStraightCanSaveIt: false, SomewhereLegalToStop: true);

    /// <summary>
    /// <b>Waiting is the correct answer when the obstruction has priority</b>, and nothing below the top
    /// rung may pre-empt it: the car is handed the place it is already standing short of and waits there.
    /// </summary>
    [Fact]
    public void TheFirstRungHoldsAtALineWhenSomebodyElseHasPriority()
    {
        var rung = 0;
        var state = Ordinary with { ObstructionHasPriority = true };
        Assert.Equal(Maneuver.HoldAtALine, DrivingLadder.Next(state, ref rung));
    }

    /// <summary>
    /// <b>A rung that answers a jam is not offered to a car that has none.</b> A clock cannot tell
    /// "cannot go forward" from "waiting to", so the back-off checks for something to back away from —
    /// the fault this fixes was cars reversing away from empty intersections while yielding correctly.
    /// </summary>
    [Fact]
    public void TheBackOffIsSkippedWithNothingToBackAwayFrom()
    {
        var rung = 0;
        var taken = DrivingLadder.Next(Ordinary, ref rung);
        Assert.NotEqual(Maneuver.BackOff, taken);
    }

    /// <summary>And it is skipped again where there is nothing behind the car to back into.</summary>
    [Fact]
    public void TheBackOffIsSkippedWithNoRoomBehind()
    {
        var rung = 0;
        var state = Ordinary with { SomethingToBackAwayFrom = true, RoomBehindM = 0f };
        Assert.NotEqual(Maneuver.BackOff, DrivingLadder.Next(state, ref rung));
    }

    /// <summary>A jammed car with room behind it takes the cheapest change of state there is.</summary>
    [Fact]
    public void AJammedCarWithRoomBehindItBacksOff()
    {
        var rung = 0;
        var state = Ordinary with { SomethingToBackAwayFrom = true, RoomBehindM = 4f };
        Assert.Equal(Maneuver.BackOff, DrivingLadder.Next(state, ref rung));
    }

    /// <summary>
    /// <b>Rung 1′ comes before the back-off, and only inside the bay</b>: the bay is the one piece of
    /// road the car is entitled to hold, and reversing further would leave it by the back.
    /// </summary>
    [Fact]
    public void ACarStuckLeavingItsBayRetreatsIntoItFirst()
    {
        var rung = 0;
        var state = Ordinary with { InItsOwnBay = true, SomethingToBackAwayFrom = true, RoomBehindM = 4f };
        Assert.Equal(Maneuver.LeaveTheBay, DrivingLadder.Next(state, ref rung));
    }

    /// <summary>
    /// <b>The second back-off is a rung of its own and not a repeat</b>: what it buys is the fuse
    /// between the two, and a jam that has had another watchdog's worth of time to change is a
    /// different jam.
    /// </summary>
    [Fact]
    public void TheLadderOffersTwoBackOffsWithSomethingElseBetweenThem()
    {
        var rung = 0;
        var state = Ordinary with
        {
            SomethingToBackAwayFrom = true, RoomBehindM = 4f, AtItsOwnBay = true,
        };

        Assert.Equal(Maneuver.BackOff, DrivingLadder.Next(state, ref rung));
        Assert.Equal(Maneuver.SquareUpInTheBay, DrivingLadder.Next(state, ref rung));
        Assert.Equal(Maneuver.BackOff, DrivingLadder.Next(state, ref rung));
    }

    /// <summary>The back-off's attempt count is spent for the whole jam rather than per rung.</summary>
    [Fact]
    public void TheBackOffIsSkippedOnceItsAttemptsAreSpent()
    {
        var rung = 0;
        var state = Ordinary with { SomethingToBackAwayFrom = true, RoomBehindM = 4f, BackOffsLeft = 0 };
        Assert.NotEqual(Maneuver.BackOff, DrivingLadder.Next(state, ref rung));
    }

    /// <summary>
    /// The order the rungs are climbed in, for a car with nothing to back away from: the destination
    /// first, then the road, then the ground, then settling, then the car itself.
    /// </summary>
    [Fact]
    public void TheRungsAreClimbedInTheOrderTheBriefNames()
    {
        var rung = 0;
        var state = Ordinary with { AStraightCanSaveIt = true };

        Assert.Equal(Maneuver.GiveUpThePlace, DrivingLadder.Next(state, ref rung));
        Assert.Equal(Maneuver.Reroute, DrivingLadder.Next(state, ref rung));
        Assert.Equal(Maneuver.ReturnToLegalGround, DrivingLadder.Next(state, ref rung));
        Assert.Equal(Maneuver.SettleForHere, DrivingLadder.Next(state, ref rung));
        Assert.Equal(Maneuver.AbandonTheCar, DrivingLadder.Next(state, ref rung));
    }

    /// <summary>
    /// <b>The ladder never stops early.</b> A car every rung refuses still gets an answer, and the
    /// answer is the last rung — anything else is a stuck agent for the rest of the run.
    /// </summary>
    [Fact]
    public void ACarEveryRungRefusesEndsAtTheLastOne()
    {
        var state = new LadderState(
            ObstructionHasPriority: false, SomethingToBackAwayFrom: false, RoomBehindM: 0f, BackOffsLeft: 0,
            InItsOwnBay: false, AtItsOwnBay: false, HoldsAPlace: false, OnARoute: false, ReroutesLeft: 0,
            AStraightCanSaveIt: false, SomewhereLegalToStop: false);

        var rung = 0;
        Assert.Equal(Maneuver.AbandonTheCar, DrivingLadder.Next(state, ref rung));

        // And once the ladder has been walked to its end it stays there rather than starting again.
        Assert.Equal(Maneuver.AbandonTheCar, DrivingLadder.Next(state, ref rung));
    }

    /// <summary>Every rung of the ladder names a manoeuvre or nothing, and never a planned entry that is not 1′.</summary>
    [Fact]
    public void NoRungNamesAPlannedEntryOtherThanTheTwoTheBriefAllows()
    {
        for (var rung = 0; rung < DrivingLadder.Rungs; rung++)
        {
            for (var flags = 0; flags < 4; flags++)
            {
                var state = Ordinary with
                {
                    InItsOwnBay = (flags & 1) != 0,
                    AtItsOwnBay = (flags & 2) != 0,
                    SomethingToBackAwayFrom = true,
                    RoomBehindM = 4f,
                };

                var named = DrivingLadder.At(rung, state);
                Assert.True(
                    named == Maneuver.None || Maneuvers.IsReactive(named)
                    || named is Maneuver.LeaveTheBay or Maneuver.SquareUpInTheBay,
                    $"rung {rung} named {Maneuvers.Code(named)}, which is neither a recovery nor one of the two the ladder may take");
            }
        }
    }
}
