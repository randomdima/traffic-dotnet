using System.Numerics;
using System.Runtime.CompilerServices;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.Agents.Person.Actions;

/// <summary>
/// <b>Rejoin</b> (PER-25): a walker walking its route that stands off the pavement altogether — in the carriageway, on
/// a verge — walking the straight back onto the way it walks, over every way that straight crosses. Done once its
/// body stands on any of the pavement again.
/// </summary>
internal sealed class WalkingBack(WalkingGround ground, PersonActions actions)
{
    /// <summary>Where each walker walking back decided to walk back to, when it stepped off the pavement.</summary>
    readonly Vector2[] _backToM = new Vector2[ground.People.Capacity];

    PersonFleet People => ground.People;

    /// <summary>
    /// <b>Begun or ended by where the walker stands</b>: a walker walking its route off the pavement altogether is
    /// getting back onto it — to where it decides then, and nowhere else after — and one on it, or hopping off the end
    /// of it onto its goal, walks it.
    /// </summary>
    /// <param name="under">The ways the world read the walker's body on (TER-4c.2).</param>
    public void Consider(int person, ReadOnlySpan<WayCover> under)
    {
        if (People.Action[person] is not (PersonAction.Walk or PersonAction.Rejoin) || !People.Walking[person]) return;

        var onIt = People.OnWay[person] != PersonFleet.NoWay || ground.IsHopping(person)
                   || People.CurrentRouteWay(person) == PersonFleet.NoWay || StandsOnThePavement(under);
        if (!onIt && People.Action[person] != PersonAction.Rejoin) _backToM[person] = BackOntoItsWayM(person);

        actions.Enter(person, onIt ? PersonAction.Walk : PersonAction.Rejoin);
    }

    /// <summary>The ground the walk back covers: what it stands on, and the stretch of its way it steps back onto.</summary>
    /// <param name="under">The ways the world read the walker's body on (TER-4c.2).</param>
    public Span<LineWay> WaysOf(int person, ReadOnlySpan<WayCover> under, Span<LineWay> into) =>
        into[..ground.TheStraightAhead(
            person, _backToM[person], ground.WayOf(People.CurrentRouteWay(person)), People.OnWayM[person], People.RadiusM[person],
            ground.PlansAheadM, under, into)];

    /// <summary>Aimed down the straight back, no further than it was granted.</summary>
    public void Aim(int person) => ground.AimAlongTheStraight(person, _backToM[person]);

    /// <summary>
    /// <b>Where a walker off the ground of its way walks back onto it</b>: the nearest of it, abeam of where the body
    /// stands — the shortest way back, over as little of anybody else's ground as there is.
    /// </summary>
    /// <remarks>
    /// <b>Not a stride down it</b>: aimed there from a corner, the straight back crossed the paint of the zebra beside
    /// the one it walks, and the two lights held it by turns for the rest of the run.
    /// </remarks>
    Vector2 BackOntoItsWayM(int person) =>
        Spline.SampleAt(ground.Walking.WayArcs(People.CurrentRouteWay(person)), People.OnWayM[person]).PositionM;

    /// <summary>
    /// <b>Whether a walker's body stands on any of the pavement</b>, its own way or not — as the world read its body on
    /// its ways (TER-4c.2). One between the two lanes of a pavement, or shoved across the corner of one, walks its
    /// route: what it steps over getting back onto its way is ground its body stands on, and where it is going is its
    /// plan's (PER-26). <b>Off the pavement altogether</b> it walks straight back onto its way.
    /// </summary>
    bool StandsOnThePavement(ReadOnlySpan<WayCover> under)
    {
        foreach (ref readonly var cover in under)
        {
            if (!ground.Ways.IsDriven(cover.Way)) return true;
        }

        return false;
    }
}
