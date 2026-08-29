using System.Numerics;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Physics;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Person;

/// <summary>
/// CAR-13: <b>the habit is drawn once and it is drawn at the rate it was asked for</b>. A share that comes
/// out at nothing is a feature no run ever exercises, and one that comes out at everything is a different
/// town — and both look identical from the outside until somebody counts.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class RecklessDrawTests
{
    const int Roster = 20_000;

    /// <summary>The two ends of it, which are the only two answers that need no arithmetic to check.</summary>
    [Theory]
    [InlineData(0f, 0)]
    [InlineData(1f, Roster)]
    public void TheShareIsKeptAtItsEnds(float share, int expected) => Assert.Equal(expected, Drawn(share));

    /// <summary>
    /// And the shipped rate in between. <b>Asked of a roster big enough that the tolerance is about the
    /// draw and not about the sample</b>: a town of four hundred draws one in a hundred as anything from
    /// one to eight, which is a claim about a coin rather than about this code.
    /// </summary>
    [Fact]
    public void TheShippedShareIsKept()
    {
        var drawn = Drawn(0.01f);

        Assert.InRange(drawn, Roster / 200, Roster / 50);
    }

    /// <summary>
    /// <b>The same seed draws the same people</b> (SIM-2), which is what makes a reckless driver something
    /// a run can be taken again to look at rather than something that happened once.
    /// </summary>
    [Fact]
    public void TheSameSeedDrawsTheSamePeople()
    {
        var first = Fleet(0.01f, seed: 7);
        var again = Fleet(0.01f, seed: 7);

        for (var person = 0; person < Roster; person++)
        {
            Assert.Equal(first.Reckless[person], again.Reckless[person]);
        }
    }

    /// <summary>
    /// <b>And the draw spends nothing of the person's own stream.</b> The habit is on a stream of its own
    /// precisely so that adding it moved no walk and no dwell in any town; a person made at one share must
    /// therefore draw exactly what the same person made at another share draws.
    /// </summary>
    [Fact]
    public void TheDrawMovesNothingElseTheSeedDecides()
    {
        var none = Fleet(0f, seed: 3);
        var all = Fleet(1f, seed: 3);

        for (var person = 0; person < Roster; person++)
        {
            Assert.Equal(none.Draw[person].NextFloat(), all.Draw[person].NextFloat());
        }
    }

    /// <summary>How many of a roster this size come out reckless at this share.</summary>
    static int Drawn(float share)
    {
        var people = Fleet(share, seed: 1);
        var count = 0;
        for (var person = 0; person < Roster; person++)
        {
            if (people.Reckless[person]) count++;
        }

        return count;
    }

    /// <summary>A roster of bodiless people: the draw is a stream of its own and wants no world at all.</summary>
    static PersonFleet Fleet(float share, ulong seed)
    {
        var people = new PersonFleet(Roster);
        for (var person = 0; person < Roster; person++)
        {
            people.Add(
                BodyId.None, Vector2.Zero, 0f, 70f, 0.25f, 0, new Rng(seed, (ulong)person),
                PersonFleet.DrawsReckless(seed, (ulong)person, share));
        }

        return people;
    }
}
