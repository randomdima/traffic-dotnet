using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Person;

/// <summary>
/// PER-13's grant read as the permission it is: <b>a walker walks while what it has been granted is more
/// than it needs to come to rest in</b>. A roster of one and no world at all — the question is arithmetic
/// over three fields.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class HeldByTheBookTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>What a walker needs to come to rest in from its own pace, which is what the ask is sized by.</summary>
    static float StopsFromPaceM =>
        Config.PersonWalkSpeedMps * Config.PersonWalkSpeedMps / (2f * Config.PersonFootGripMps2);

    static PersonFleet OneWalkerGranted(float authorityM)
    {
        var people = new PersonFleet(1);
        var person = people.Add(
            default, System.Numerics.Vector2.Zero, headingRad: 0f, massKg: 80f,
            radiusM: Config.PersonDiameterM * 0.5f, variant: 0, draw: default, reckless: false);

        people.Walking[person] = true;
        people.AuthorityM[person] = authorityM;
        return people;
    }

    /// <summary>Clear pavement in front is a walk, whatever the body is doing.</summary>
    [Fact]
    public void GroundEnoughToStopInIsAWalk()
    {
        var people = OneWalkerGranted(StopsFromPaceM * 2f);

        Assert.False(people.IsHeldByTheBook(person: 0, StopsFromPaceM));
    }

    /// <summary>
    /// <b>And a grant too short to stop in is a stand</b>, which is the whole of the rule: read against
    /// nothing instead, a body with a centimetre of grant sets off at full pace and comes to rest a whole
    /// stopping distance inside the gap it keeps — where the grant is below nothing and stays there,
    /// because feet have no reverse.
    /// </summary>
    [Fact]
    public void GroundTooShortToStopInIsAStand()
    {
        var people = OneWalkerGranted(StopsFromPaceM * 0.5f);

        Assert.True(people.IsHeldByTheBook(person: 0, StopsFromPaceM));
    }

    /// <summary>
    /// <b>At rest the bar is nothing</b>, so any ground at all is a stride. It is what gets a pair already
    /// inside one another's gap out of it: held to the pace's own stopping distance both would stand for
    /// ever, and the creep is the only thing that breaks one.
    /// </summary>
    [Fact]
    public void AtRestAnyGroundAtAllIsAStride()
    {
        var people = OneWalkerGranted(StopsFromPaceM * 0.1f);

        Assert.False(people.IsHeldByTheBook(person: 0, stopsInM: 0f));
    }

    /// <summary>And no ground is a stand however slowly the body is going.</summary>
    [Fact]
    public void NoGroundIsAStandEvenAtRest()
    {
        var people = OneWalkerGranted(-0.2f);

        Assert.True(people.IsHeldByTheBook(person: 0, stopsInM: 0f));
    }

    /// <summary>
    /// The kerb answers for itself (PER-15): a walker waiting out a red stands where the kerb put it rather
    /// than where the pavement ran out, and it may still walk back to the stand-off while it waits.
    /// </summary>
    [Fact]
    public void AWalkerHeldAtAKerbIsNotHeldByTheBook()
    {
        var people = OneWalkerGranted(-0.2f);
        people.HeldAtTheKerb[0] = true;

        Assert.False(people.IsHeldByTheBook(person: 0, StopsFromPaceM));
    }
}
