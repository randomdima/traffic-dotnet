using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>What a car crossing a junction holds of its own join</b>: the runs of that join the other ways
/// through the box are driven over it at, laid ahead of the road the car's own reservation has reached.
/// </summary>
internal sealed partial class TownWorld
{
    /// <summary>
    /// <b>The ground a car crossing a junction has committed to on its own join</b>, laid into the road's
    /// book from the car's own field — the runs of that join the other ways through the box are driven over
    /// it at (<see cref="JunctionCrossings.OwnRuns"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Its own join and nothing else</b> (TER-5c). A crossing used to write a stretch onto every join it
    /// was driven over, so that the traffic on those joins met it in its own book; that is a car reserving
    /// several ways at once, none of which it will ever be on, and a box washed over by whoever merely aimed
    /// at it. What the runs are for is the same car coming the other way — it reads them where they lie
    /// (<see cref="WhereTheGroundIsCrossed"/>), which is the same fact asked from the other end.
    /// </para>
    /// <para>
    /// <b>They are laid where the car's own reservation has not reached yet</b>, and that is the whole of
    /// why the claim exists: a car's road ahead is a braking distance and no more, which does not reach the
    /// place two lines meet until it is nearly on top of the junction. Under the body the same ground is the
    /// car's own reservation (<see cref="AskForTheGround"/>), which carries its length and its swing.
    /// </para>
    /// <para>
    /// Re-laid from the car every tick for the same reason a claim is: nothing has to be released, a
    /// crossing cannot outlive the car making it, and a car wrecked or taken over by a hand is out of the
    /// book on the next rebuild without anything having had to notice.
    /// </para>
    /// </remarks>
    void PlaceTheCrossing(int car)
    {
        var crossing = Cars.Crossing[car];
        if (crossing < 0) return;

        if (!Cars.Driven[car] || Cars.Broken[car])
        {
            Cars.Crossing[car] = CarFleet.NoMovement;
            return;
        }

        LayTheCrossing(car, crossing);
    }

    /// <summary>
    /// The runs themselves, which the rebuild lays and a car taking a crossing up mid-walk lays again:
    /// <b>the stretches of its own join the other ways through the box are driven over it at</b>, which is
    /// what refuses them before this car's own road has reached that far.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Only the crossings the car has still to reach</b>, and their near edges walk up with the tail. A
    /// run is where this car's line is driven over by somebody else's, read from this car's own end, so the
    /// metres of it behind the body are the crossing spent — and the box empties behind the car as it works
    /// through rather than at the far side. Held instead from the first crossing point to the last for as
    /// long as the movement was, one car turning across a wide box shut every metre of its own way through
    /// it, whether or not anything crossed there.
    /// </para>
    /// <para>
    /// <b>What is laid is the run less the road</b>, which is what keeps one body to one metre of one way.
    /// The car's own reservation on this join is a stretch of the same book carrying its length, its swing
    /// and where it comes to rest; a claim over the same metres is that car laid over itself — two occupants
    /// to every walk of the way and two washes to the overlay — and it holds nothing the reservation was not
    /// holding already. The two are read as one set (<c>Spoken</c>), so what another movement is refused by
    /// is their union and does not turn on where the seam between them falls.
    /// </para>
    /// <para>
    /// <b>Which is the ground ahead of the car's own road and nothing else</b>: what it is committed to and
    /// has not reached. <b>Behind the body there is nothing to claim</b>, because the reservation already
    /// begins a margin behind the tail (<see cref="SimConfig.CarTailMarginM"/>) — the width the book's
    /// one-dimensional reading of a swinging body threw away, carried on every way the car is on rather than
    /// added back on this one. Released at the bare tail instead, Odesa's soak wrecks cars.
    /// </para>
    /// <para>
    /// <b>A car whose line no longer takes this join claims the runs whole</b>, since there is no metre of
    /// its own to measure them against — which is the conservative way round for a body still holding a
    /// movement it has come off, and the whole of what such a body holds on that join
    /// (<see cref="LieUnderTheJoins"/>).
    /// </para>
    /// <para>
    /// <b>A body that is not driving its movement claims it all the same</b>, and that is not the same claim
    /// as a driver's: one shoved off its line or under a hand is a body whose ground nothing else can work
    /// out — its own line says one thing and its pose another — so what it holds is the movement it is on,
    /// whole, until something puts it back on a line or takes it off the road. Dropped instead, on the
    /// grounds that a body off its line holds the ground it lies on, Odesa's soak wrecks two cars a minute:
    /// the ground it lies on is a projection, and a body far enough off its line falls outside the band of
    /// every join it is actually in.
    /// </para>
    /// </remarks>
    void LayTheCrossing(int car, int crossing)
    {
        var onto = TheSlotOnto(car, crossing);

        // What the car's own road holds of this join, in the join's own metres.
        var roadFromM = PastOnTheCrossing(car, crossing);
        var roadToM = onto < 0 ? float.NegativeInfinity : Cars.ReserveToM[car] - Cars.LaneEndsOf(car)[onto];

        foreach (ref readonly var run in _roads.Crossings.OwnRuns(crossing))
        {
            if (run.ToM <= roadFromM) continue;

            Claim(MathF.Max(run.FromM, roadToM), run.ToM);
        }

        void Claim(float fromM, float toM)
        {
            if (toM > fromM) _occupancy.Add(_occupancy.WayOfTurn(crossing), fromM, toM, 0f, car, LaneUse.Claimed);
        }
    }

    /// <summary>
    /// <b>How far behind this car a crossing point has to fall before it is behind it</b>: where its own
    /// ground begins on the join it is crossing, in that join's own metres, or negative infinity where its
    /// line does not take that join at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the near edge of the reservation and nothing worked out a second time</b>
    /// (<see cref="AskForTheGround"/>) — a body's length and the margin it keeps behind its tail. The tail
    /// and not the nose, because a section is a place the <em>body</em> goes over: a car whose bumper has
    /// cleared a crossing point is still lying across it. The margin is what the book's one-dimensional
    /// reading of a body owes the width it threw away; released at the bare tail, Odesa's soak wrecks cars
    /// (<see cref="SimConfig.CarTailMarginM"/>).
    /// </para>
    /// <para>
    /// A car that is not under way has asked for no ground, so its stretch stands at its line's own origin
    /// and every crossing point on the join is in front of it — which is what makes such a body claim the
    /// runs whole (<see cref="LayTheCrossing"/>).
    /// </para>
    /// </remarks>
    float PastOnTheCrossing(int car, int crossing)
    {
        var onto = TheSlotOnto(car, crossing);
        return onto < 0 ? float.NegativeInfinity : Cars.ReserveFromM[car] - Cars.LaneEndsOf(car)[onto];
    }

    /// <summary>
    /// Which lane of this car's chain leads onto <paramref name="crossing"/>, or -1 where its line does not
    /// take that join — <b>the one place the chain is asked which movement the car is on</b>, so the metre
    /// the crossing is measured from cannot be worked out two ways.
    /// </summary>
    int TheSlotOnto(int car, int crossing)
    {
        var ahead = LaneAheadSlot(car, Cars.ProgressM[car]);
        if (ahead + 1 >= Cars.Line[car].LaneCount) return -1;

        var chain = Cars.ChainOf(car);
        return _roads.TurnSlot(chain[ahead], chain[ahead + 1]) == crossing ? ahead : -1;
    }
}
