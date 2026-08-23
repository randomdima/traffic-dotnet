using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Town;

/// <summary>What a crossing does to a car approaching it: which one is being met, at what pace, and whether anybody is standing on the paint.</summary>
internal sealed partial class TownWorld
{
    /// <summary>
    /// `P-12`, and the whole of what a driver owes somebody on a crossing: the pace it is approached at,
    /// and a stop short of the paint while anyone is on it or stepping onto it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The yield is discharged here and never handed to `E-1`: handing it to a manoeuvre whose entry
    /// conditions a person on foot cannot satisfy gets it refused, and a refused reactive manoeuvre goes
    /// to the ladder — which answers a pedestrian by reversing away from them.
    /// </para>
    /// <para>
    /// One manoeuvre, one crossing, named on entry. Asked as "is there paint ahead" it is never true for
    /// long, since a junction paints its far arm too: only the crossing on the arm being approached
    /// counts.
    /// </para>
    /// <para>
    /// The pace exemption is read off the pedestrian side of the signal table, so what a driver may do
    /// and what the people on the kerb have been told can never disagree. It lifts the pace and nothing
    /// else — somebody on the paint anyway is still stopped for.
    /// </para>
    /// </remarks>
    /// <param name="stopShortOfM">
    /// Where the profile is already being asked to stop, so a queue that would leave this car standing
    /// on the paint stops it before the paint instead.
    /// </param>
    void CrossingAhead(int car, int ahead, float progressM, float stopShortOfM,
        out float stopAtM, out float atM, out float paceMps)
    {
        stopAtM = float.PositiveInfinity;
        atM = float.PositiveInfinity;
        paceMps = float.PositiveInfinity;

        var noseM = progressM + _config.CarNoseAheadOfAxleM;
        var centreM = progressM + _config.Car.WheelbaseM * 0.5f;
        var tailM = progressM - (_config.Car.LengthM - _config.Car.WheelbaseM) * 0.5f;
        var reachM = SightM();

        // Both arms of the turn, not only the one the car is on: a junction paints its far arm too, and
        // read off the lane being left alone, the crossing about to be driven over belongs to nobody.
        var lanes = Cars.Line[car].LaneCount;
        for (var step = 0; step < 2 && ahead + step < lanes; step++)
        {
            LookAtTheCrossingsOn(
                car, ahead + step, progressM, stopShortOfM, noseM, centreM, tailM, reachM,
                ref stopAtM, ref atM, ref paceMps);
        }
    }

    /// <summary>One lane of the chain's own crossings, weighed against the car standing where it is.</summary>
    void LookAtTheCrossingsOn(
        int car, int slotOfLane, float progressM, float stopShortOfM, float noseM, float centreM, float tailM,
        float reachM, ref float stopAtM, ref float atM, ref float paceMps)
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
            // business yet. The pace is held until the body is off the paint and not only up to it: a
            // car that accelerates the moment its nose is over a zebra has not slowed for it.
            if (tailM > farEdgeM || aheadM > reachM) continue;

            // One manoeuvre, one crossing: the nearest ahead is the one being approached — or the one
            // under the car, whose distance is negative and therefore nearer than any of them.
            if (aheadM >= atM) continue;

            atM = MathF.Max(0f, aheadM);
            var kerbsHeld = _signals.CrossingIsLit(crossing)
                            && _signals.ForCrossing(crossing, _elapsedS) != SignalColour.Green;

            paceMps = kerbsHeld ? float.PositiveInfinity : _config.CarCrossingPaceMps;

            // Somebody on it or stepping onto it, or a queue that would leave this car standing on it —
            // and only while the body has not yet started across, because a light governs the traffic
            // outside a crossing and never the body inside one.
            var wouldRestOnIt = stopShortOfM + noseM < farEdgeM + _config.Car.LengthM;
            stopAtM = centreM < nearEdgeM && (wouldRestOnIt || AnybodyOnTheCrossing(lane, painted.AlongM(slot)))
                ? MathF.Max(0f, aheadM - _config.CarCrossingStandOffM)
                : float.PositiveInfinity;
        }
    }

    /// <summary>
    /// `P-12` owed by a car under its own geometry: the same stop short of the paint, for a car swinging
    /// out of a bay, round an obstruction or through a turn-around.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The lane says which crossings there are and the template says where they are.</b> A template is
    /// laid over no lane and its metres are its own, so a distance along the lane under the car is not a
    /// distance along the shape being driven; the paint is projected onto that shape instead, which is the
    /// same measurement the town made to put the paint on the lane in the first place.
    /// </para>
    /// <para>
    /// <b>The stop and the pace, and no bar and no light.</b> A template claims no movement and the ground
    /// it runs over belongs to no lane, so there is no approach for a light to govern — but the pace a
    /// crossing is approached at is owed by a car under its own geometry exactly as by one on its route, and
    /// it used to be left out on the grounds that every template was held to the reverse cap anyway. That
    /// was a coincidence between two unrelated figures, and it stopped being true the moment one template
    /// was let off the manoeuvring pace (`E-4`).
    /// </para>
    /// <para>
    /// <b>Asked of the book first and the geometry only after.</b> Nobody is on a crossing nearly all of
    /// the time, and that answer is a walk of one way's occupants; the projection is what costs something —
    /// so the pace, which is owed whether or not anybody is there, is taken from the same projection rather
    /// than from a second pass.
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
            if (offM > _plan.Crosswalks.SpanM[crossing] * 0.5f) continue;

            // A body already over the paint drives on. Stopping on a crossing is the one thing worse than
            // not having stopped short of it, and it is the same reading the route's own entry takes.
            var aheadM = onLineM - halfDepthM - leadM;
            if (aheadM < 0f || aheadM > reachM) continue;

            atM = MathF.Min(atM, aheadM);
            if (occupied) stopAtM = MathF.Min(stopAtM, MathF.Max(0f, aheadM - _config.CarCrossingStandOffM));
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
        return _occupancy.AnybodyOnFoot(way, alongM, alongM);
    }
}
