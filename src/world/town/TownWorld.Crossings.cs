using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Town;

/// <summary>What a crossing does to a car approaching it, which is one thing: it may not be parked on.</summary>
/// <remarks>
/// <b>A body on a crossing is owed nothing here</b> (TER-5e). It stands on the lane under the paint and
/// holds what it covers of it like any other body (`PER-26`), so the traffic is already held off it by the
/// grant that stretch cuts — and a stop owed to the paint as well would be one gap kept twice (SIM-7).
/// What is left is the car's own courtesy: not coming to rest on ground somebody has to walk over.
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>
    /// <b>A stop short of the paint for a queue that would otherwise leave this car standing on it</b>
    /// (TER-4c.1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Paint is not a speed limit.</b> A crossing nobody is standing on takes nothing off a car, and the
    /// car drives over it at whatever the rest of the road affords.
    /// </para>
    /// <para>
    /// <b>It is discharged here and by no manoeuvre of its own.</b> The answer is a term of the speed
    /// profile, taken every tick into the same minimum the corners and the grant are taken into, so a car
    /// stopping short of a zebra is running its line on the road the zebra left it (`P-4`).
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

            // <b>A queue that would leave this car standing on the paint, and nothing else.</b> Somebody on
            // the crossing is a body standing on this lane and has already cut the road this car was
            // granted (`PER-26`, TER-4c.2) — asked again here it would be one gap kept twice (SIM-7), and
            // the one that is kept is the driver's own.
            var wouldRestOnIt = stopShortOfM + noseM < farEdgeM + Cars.BuildOf(car).LengthM;
            stopAtM = centreM < nearEdgeM && wouldRestOnIt
                ? MathF.Max(0f, aheadM - Cars.BuildOf(car).CrossingStandOffM)
                : float.PositiveInfinity;
        }
    }

    /// <summary>
    /// <b>Where the paint is under a car driving geometry of its own</b> — a swerve, a bay entry, a bay
    /// exit — so a shape standing over a crossing knows it has nowhere to swing to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The lane says which crossings there are and the template says where they are.</b> A template is
    /// laid over no lane and its metres are its own, so a distance along the lane under the car is not a
    /// distance along the shape being driven; the paint is projected onto that shape instead, which is the
    /// same measurement the town made to put the paint on the lane in the first place.
    /// </para>
    /// <para>
    /// <b>Where it is and never a stop.</b> A body on a crossing is a body on the lane under it and cuts the
    /// ground a template asked for like anything else standing there (`PER-26`), which is the reading
    /// <see cref="GroundAhead"/> already takes; a second refusal owed to the paint would be that stop kept
    /// twice (SIM-7).
    /// </para>
    /// </remarks>
    /// <param name="leadM">Where the leading edge of the body stands along the line, in whichever gear it is being driven.</param>
    void CrossingOnTheTemplate(int car, ReadOnlySpan<ArcSeg> line, float leadM, float reachM, out float atM)
    {
        atM = float.PositiveInfinity;

        var lane = _roads.NearestLane(Cars.PositionM[car], out _);
        if (lane < 0) return;

        var painted = _furniture.CrossingsOn(lane);
        for (var slot = painted.From; slot < painted.To; slot++)
        {
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
        }
    }
}
