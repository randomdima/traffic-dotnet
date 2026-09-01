using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Town;

/// <summary>What a crossing does to a car approaching it: which one is being met, and whose ground its paint is.</summary>
internal sealed partial class TownWorld
{
    /// <summary>
    /// <b>The whole of what a driver owes somebody on a crossing</b>: a stop short of the paint while
    /// anyone is on it or has been refused it at the kerb (TER-4c.1, TER-5e).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Paint is not a speed limit.</b> A crossing whose band the book has granted this car the road
    /// over takes nothing off it, and the car drives over it at whatever the rest of the road affords;
    /// what slows a car at a zebra is the ground being somebody else's, which is one mechanism and not a
    /// second (SIM-7).
    /// </para>
    /// <para>
    /// <b>It is discharged here and by no manoeuvre of its own.</b> The answer is a term of the speed
    /// profile, taken every tick into the same minimum the corners and the grant are taken into, so a car
    /// stopping at a zebra is running its line on the road the zebra left it (`P-4`). An entry named off
    /// the term that won would have imposed nothing the profile was not already imposing, and a
    /// <em>reactive</em> one would have entry conditions a person on foot cannot satisfy — refused, and a
    /// refused reactive manoeuvre goes to the ladder, which answers a pedestrian by reversing away from
    /// them.
    /// </para>
    /// <para>
    /// One crossing at a time: the nearest ahead is the one being approached. Asked as "is there paint
    /// ahead" it is never false for long, since a junction paints its far arm too.
    /// </para>
    /// </remarks>
    /// <param name="stopShortOfM">
    /// Where the profile is already being asked to stop, so a queue that would leave this car standing
    /// on the paint stops it before the paint instead.
    /// </param>
    void CrossingAhead(int car, int ahead, float progressM, float stopShortOfM, out float stopAtM, out float atM)
    {
        stopAtM = float.PositiveInfinity;
        atM = float.PositiveInfinity;

        ref readonly var build = ref Cars.BuildOf(car);
        var noseM = progressM + build.NoseAheadOfAxleM;
        var centreM = progressM + build.CentreAheadOfAxleM;
        var tailM = progressM - build.TailBehindAxleM;
        var reachM = SightM(car);

        // Both arms of the turn, not only the one the car is on: a junction paints its far arm too, and
        // read off the lane being left alone, the crossing about to be driven over belongs to nobody.
        var lanes = Cars.Line[car].LaneCount;
        for (var step = 0; step < 2 && ahead + step < lanes; step++)
        {
            LookAtTheCrossingsOn(
                car, ahead + step, progressM, stopShortOfM, noseM, centreM, tailM, reachM, ref stopAtM, ref atM);
        }
    }

    /// <summary>One lane of the chain's own crossings, weighed against the car standing where it is.</summary>
    void LookAtTheCrossingsOn(
        int car, int slotOfLane, float progressM, float stopShortOfM, float noseM, float centreM, float tailM,
        float reachM, ref float stopAtM, ref float atM)
    {
        var lane = Cars.ChainOf(car)[slotOfLane];
        var painted = _furniture.CrossingsOn(lane);
        for (var slot = painted.From; slot < painted.To; slot++)
        {
            var crossing = painted.CrossingAt(slot);
            var halfDepthM = _plan.Crosswalks.DepthM[crossing] * 0.5f;
            var onLineM = OnTheLineM(car, slotOfLane, painted.AlongM(slot));
            var nearEdgeM = onLineM - halfDepthM;
            var farEdgeM = onLineM + halfDepthM;
            var aheadM = nearEdgeM - noseM;

            // Behind it entirely — the tail is past the far edge — or too far ahead to be this car's
            // business yet. A crossing stays this car's business until the body is off it and not only
            // up to it: what is under the car is what says it has nowhere to swerve to
            // (<see cref="DriveScene.ClearOfThePaint"/>).
            if (tailM > farEdgeM || aheadM > reachM) continue;

            // One manoeuvre, one crossing: the nearest ahead is the one being approached — or the one
            // under the car, whose distance is negative and therefore nearer than any of them.
            if (aheadM >= atM) continue;

            atM = MathF.Max(0f, aheadM);

            // Somebody on it or stepping onto it, somebody with the right of way waiting at it, or a queue
            // that would leave this car standing on it — and only while the body has not yet started
            // across, because a light governs the traffic outside a crossing and never the body inside one.
            //
            // <b>The stop is short of the ground a body on the paint holds and not short of the
            // paintwork</b>: a band reaches a stride either side of the crossing (`PER-15`), so a car
            // stopped at the paint is standing on the very ground it stopped to give up, and whoever it
            // gave way to is refused by it for as long as it stands there (TER-5e).
            //
            // <b>Only the courtesy is what a reckless driver drops</b> (CAR-13). Giving way to somebody
            // still on the kerb is a stop owed to a person who has not started; a body already on the paint
            // is a body, and no habit of the driver's makes it anything else.
            var wouldRestOnIt = stopShortOfM + noseM < farEdgeM + Cars.BuildOf(car).LengthM;
            stopAtM = centreM < nearEdgeM
                      && (wouldRestOnIt || AnybodyOnTheCrossing(lane, painted.AlongM(slot))
                          || (!Cars.BlueLight[car] && !RecklessAtTheWheel(car)
                              && GivingWayAtTheKerb(lane, painted.AlongM(slot))))
                ? MathF.Max(
                    0f, aheadM - Cars.BuildOf(car).CrossingStandOffM - (PaintClaimM(crossing) - halfDepthM))
                : float.PositiveInfinity;
        }
    }

    /// <summary>
    /// The same thing owed by a car under its own geometry (TER-5e): the same stop short of the paint, for
    /// a car swinging out of a bay, into one, or round an obstruction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The lane says which crossings there are and the template says where they are.</b> A template is
    /// laid over no lane and its metres are its own, so a distance along the lane under the car is not a
    /// distance along the shape being driven; the paint is projected onto that shape instead, which is the
    /// same measurement the town made to put the paint on the lane in the first place.
    /// </para>
    /// <para>
    /// <b>The stop, and no bar and no light.</b> A template claims no movement and the ground it runs over
    /// belongs to no lane, so there is no approach for a light to govern — what is left is the body on the
    /// paint, which is owed a stop by a car under its own geometry exactly as by one on its route.
    /// </para>
    /// <para>
    /// <b>Asked of the book first and the geometry only after.</b> Nobody is on a crossing nearly all of
    /// the time, and that answer is a walk of one way's occupants; the projection is what costs something,
    /// so it is taken only where the book has already said somebody is there — and where the paint is
    /// under the car at all, which is what says the shape has nowhere to swerve to.
    /// </para>
    /// </remarks>
    /// <param name="leadM">Where the leading edge of the body stands along the line, in whichever gear it is being driven.</param>
    void CrossingOnTheTemplate(
        int car, ReadOnlySpan<ArcSeg> line, float leadM, float reachM, out float stopAtM, out float atM)
    {
        stopAtM = float.PositiveInfinity;
        atM = float.PositiveInfinity;

        var lane = _roads.NearestLane(Cars.PositionM[car], out _);
        if (lane < 0) return;

        var painted = _furniture.CrossingsOn(lane);
        for (var slot = painted.From; slot < painted.To; slot++)
        {
            var occupied = AnybodyOnTheCrossing(lane, painted.AlongM(slot));
            var crossing = painted.CrossingAt(slot);
            var centreM = _plan.Crosswalks.CentreM[crossing];
            var halfDepthM = _plan.Crosswalks.DepthM[crossing] * 0.5f;
            var onLineM = Spline.ProjectM(line, centreM, leadM, reachM + halfDepthM);

            // The paint has to be on the shape and not merely inside the window searched for it: a
            // projection that never reached the crossing comes back at the end of the window, which is a
            // place on the line and not a place the car is about to drive over.
            var offM = (Spline.SampleAt(line, onLineM).PositionM - centreM).Length();
            if (offM > _furniture.CrossingSpanM(crossing) * 0.5f) continue;

            // A body already over the paint drives on. Stopping on a crossing is the one thing worse than
            // not having stopped short of it, and it is the same reading the route's own entry takes.
            var aheadM = onLineM - halfDepthM - leadM;
            if (aheadM < 0f || aheadM > reachM) continue;

            atM = MathF.Min(atM, aheadM);
            if (occupied)
            {
                stopAtM = MathF.Min(
                    stopAtM,
                    MathF.Max(
                        0f, aheadM - Cars.BuildOf(car).CrossingStandOffM - (PaintClaimM(crossing) - halfDepthM)));
            }
        }
    }

    /// <summary>
    /// Whether anybody is on this crossing or about to step onto it, <b>read off the road's own book</b>:
    /// a walker crossing lays the band of every lane its paint is laid across, and this asks the one it is
    /// driving down whether that band is spoken for.
    /// </summary>
    /// <remarks>
    /// <b>The question belongs to the body that answers it</b>, which is why it is not a search of the
    /// ground any more. Asked as "is there anybody within a stride of this paint" it was a query of the
    /// proximity index per crossing per approaching car per tick, and it said yes for anybody merely
    /// walking down the pavement past a zebra — which is a car stopped for somebody who was never going to
    /// cross.
    /// </remarks>
    bool AnybodyOnTheCrossing(int lane, float alongM)
    {
        var way = _occupancy.WayOfLane(lane);
        return _occupancy.AnybodyCrossing(way, alongM, alongM);
    }

    /// <summary>
    /// <b>Whether this car owes the paint a stop to somebody who has not stepped onto it yet</b> — an
    /// uncontrolled crossing with a walker at its kerb, refused the band of this very lane (TER-5e).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The stop is what hands the ground back.</b> A body stopped short of a crossing holds none of it
    /// (TER-4c.1), so the band the walker was refused is free on the next tick and the walker takes it: the
    /// right of way is spent by the traffic giving up ground rather than by anybody being ordered off it,
    /// which is one mechanism and not a second (SIM-7).
    /// </para>
    /// <para>
    /// <b>And it is bounded by the road a car needs to stop in</b>, because the ask is (TER-4c.1): a car too
    /// close to stop keeps the paint, the band stays refused, and the wait lasts another moment. Nobody is
    /// waved in front of a car that could not have stopped for them.
    /// </para>
    /// <para>
    /// <b>Nothing here asks whether the crossing is lit, and nothing needs to.</b> A walker held by a red
    /// asks for no ground at all (`PER-7.3`), so at a lit crossing the only body this can find is one that
    /// began on green and is still on the paint — which is exactly the body a driver owes a stop to
    /// whatever the lamps say (TLT-2a), and the same yield the stop above already carries. A test on the lamps
    /// here would be a second gate on a phase that has already decided (SIM-7).
    /// </para>
    /// </remarks>
    bool GivingWayAtTheKerb(int lane, float alongM)
    {
        if (!_occupancy.AnybodyWaitingFor(_occupancy.WayOfLane(lane), alongM, alongM)) return false;

        GaveWayAtAKerb++;
        return true;
    }
}
