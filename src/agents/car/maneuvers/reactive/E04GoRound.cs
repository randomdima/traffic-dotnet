namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>`E-4` — go round what is in the way.</b> The only overtaking that exists in this town: an S out
/// beside the lane, past what is in it — a wreck, a car nobody is in, somebody who has stopped in the
/// road, somebody reeling down the middle of it — and the mirror S back onto the line it left. See
/// <c>docs/e04-go-round.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a change to the driven line and never to the route</b> (MAN-7). The progress measure, the
/// lanes and the junction claims stay exactly as they were; a driver does not re-route to get round
/// something in its own lane. The centreline may be crossed for this and for nothing else (CAR-6.2b),
/// which is why the swerve tries the centreline side first and the verge only if the ground refuses.
/// </para>
/// <para>
/// <b>The lateral shift is a function of distance and not of time</b> (§8 rule 2), which is why it is
/// laid as geometry: a line that moves on a clock arrives whether or not the car did, and steers the car
/// into the thing it was avoiding.
/// </para>
/// <para>
/// <b>The shape is sized by what it is passing and not by where that thing stands.</b> A target that is
/// moving is gained on at the closing speed, so the ground the car needs to get past it is what it covers
/// in that time — <c>clear · v ⁄ (v − u)</c>, which is the static gap again wherever <c>u</c> is zero.
/// Something almost as fast as this car needs hundreds of metres, and the ground is what refuses that.
/// </para>
/// <para>
/// <b>Discretionary</b> (§1.4 row 5), and the discretion is <see cref="DriveScene.WorthGoingRound"/>'s.
/// A queue is not an obstruction, and a car that swings out round a queue is a head-on.
/// </para>
/// </remarks>
internal static class E04GoRound
{
    public const bool ThinksEveryTick = true;

    public const bool Watched = true;

    /// <summary>
    /// <c>Sa</c>: on a route, with something in front worth getting past, and a swerve the ground and the
    /// book both admit.
    /// </summary>
    /// <remarks>
    /// <b>What is in front, what it is doing, whether it is entitled to be there and how long this car has
    /// put up with it are one reading</b> (<see cref="DriveScene.WorthGoingRound"/>), and it is the same one
    /// `P-4` names this entry off. Asked again here in this entry's own words they would be an entry `P-4`
    /// hands a car that then refuses it, which is a pair passing a car to and fro in one spot. What is left
    /// below is only whether the shape fits.
    /// </remarks>
    public static ManeuverStart Begin(in DriveScene scene, ManeuverDesk desk, int subject)
    {
        if (!scene.OnARoute || !scene.WorthGoingRound) return ManeuverStart.No;
        if (float.IsPositiveInfinity(scene.Context.HeadwayM)) return ManeuverStart.No;

        // <b>Further than a driver can see is not a manoeuvre.</b> It is what refuses an overtake of
        // something nearly as fast as this car: the pass straight is the ground the closing speed needs,
        // and a closing speed near zero asks for a road nobody has.
        var passM = PassM(scene);
        if (passM > scene.Build.SightM) return ManeuverStart.No;

        // <b>And it has to be finished on the segment it was begun on.</b>
        // <see cref="DriveScene.OnACarriageway"/> is what says this car is not <em>at</em> a junction, and
        // it is a fact about where the car stands; this is the other half of the same rule, and the only
        // half that can be asked here, because how much road the pass wants is not known until it is
        // measured. A swerve that reaches into the box is a car crossing a junction on a line the town has
        // no record of.
        if (passM > scene.ToTheBoxM) return ManeuverStart.No;
        if (!desk.LayTheSwerve(scene.Car, passM, scene.AlongMps)) return ManeuverStart.No;

        // The stretch of this car's own lane the swerve leaves and comes back into, so the traffic behind
        // holds off the ground it is about to swing through. It is measured from where the car stood on its
        // route rather than from where it stands now, because laying the template above has just restarted
        // the progress measure. The oncoming half is deliberately not claimed — see
        // <see cref="ManeuverDesk.ClaimTheSwerve"/>.
        desk.ClaimTheSwerve(scene.Car, scene.ProgressM, passM);
        return ManeuverStart.Yes;
    }

    public static ManeuverOutcome Tick(in DriveScene scene, ManeuverDesk desk, float sinceS, ref DriveLimits limits)
    {
        if (!scene.OnATemplate) return ManeuverOutcome.Fail(Maneuver.RunTheLine, ManeuverReason.LostTheLine);

        return scene.LineIsSpent
            ? ManeuverOutcome.To(Maneuver.RunTheLine, ManeuverReason.LineSpent)
            : ManeuverOutcome.Running;
    }

    /// <summary>
    /// The run between the two S-bends, in the car's own metres: <b>what has to be got past, plus the room
    /// to be clear of it, scaled by how much of that ground the car spends catching up</b>. Against
    /// something stopped the two are the same and the scaling is one.
    /// </summary>
    static float PassM(in DriveScene scene)
    {
        var clearM = scene.Context.HeadwayM + scene.Build.LengthM * PassClearanceInCarLengths;
        var aheadMps = scene.Context.HeadwaySpeedMps;
        if (aheadMps <= scene.Config.Driving.StopSpeedMps) return clearM;

        var closingMps = scene.AlongMps - aheadMps;
        return closingMps > 0f ? clearM * scene.AlongMps / closingMps : float.PositiveInfinity;
    }

    /// <summary>How far past the obstruction the swerve comes back in: two car lengths, so the tail is clear before the line is.</summary>
    const float PassClearanceInCarLengths = 2f;
}
