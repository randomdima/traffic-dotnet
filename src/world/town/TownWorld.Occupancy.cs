using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// One of the town's own ways under a stretch of one car's line: which way, the metres of it the stretch
/// covers, and where the near end of that falls back on the line it came from.
/// </summary>
/// <remarks>
/// <see cref="LineFromM"/> is what makes the trip back cheap. A line's metres and a way's metres share no
/// origin — the assembler trims each lane by the setbacks its joins were taken at — so a distance read off
/// the index has to be carried home through the same offset it was carried out on.
/// </remarks>
internal readonly record struct LineWay(int Way, float FromM, float ToM, float LineFromM);

/// <summary>
/// <b>The lane index</b>: who is on each way of the road and which stretch of it each of them has been
/// granted, laid once a tick from the bodies themselves — and the two questions a driver asks of it: what
/// is in front of me on the road I am actually driving, and how much of that road is mine.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the whole of what a driver looks at.</b> A ray found a shape at a distance and a shape is the
/// same reading whether it is a driver waiting his turn or a wreck; the index knows which, because it is
/// built from the town's own arrays rather than from the geometry. Everything that can be on a lane is in
/// it — the traffic, anybody on foot in the lane, and the town's furniture (<see cref="StandingGround"/>) —
/// so there is no second mechanism behind it to catch what it left out.
/// </para>
/// <para>
/// <b>Following is a grant and not a reading.</b> Every driver asks for the road from its own tail to
/// where it plans to stop, and is granted what is left of it in front of the nearest car already on it —
/// so nothing has to measure a gap to hold one, and the car behind simply has less road to stop in and
/// plans a speed that fits it. <b>Nobody is granted ground another car will still be standing on when it
/// has stopped</b>, which is the one thing the whole arrangement is for.
/// </para>
/// <para>
/// <b>The ask, the answer, and then the ground is yours</b> (TER-4c.1). What comes back is regularly less
/// than what was asked for — cut at the first metre somebody else has, or at the place a rule stops the
/// asker — and a part of what was wanted is the ordinary answer rather than a refusal. <b>Past that the
/// holder moves without asking anything again</b>: nothing in this file is a permission handed out a second
/// time at the moment of moving, and whoever comes to that ground after is the one that has to stop.
/// </para>
/// <para>
/// <b>Identity and distance both from the index.</b> Everything in front is a stretch of the same way in the
/// same metres, so the gap is a subtraction (<see cref="LookAhead"/>) — and the reading and the grant are
/// then two walks of one book rather than two opinions about one road. Both are walked in by the ground
/// covered since they were taken rather than carried.
/// </para>
/// <para>
/// <b>A body takes ground on the ways it drives and on no others</b> (TER-5c.1). Inside a junction two ways
/// run over one piece of the world, and what settles that is a table filled once from the lines themselves
/// (<see cref="JunctionCrossings"/>): a driver looks its own way up and reads the metres named there in the
/// crossed way's own book, so its grant is cut by ground it will never be on without its ever having
/// written to it (<see cref="WhereTheGroundIsCrossed"/>). <b>A reservation is stated in one way's metres and
/// means something about the whole town</b>, which is what makes one body to a piece of ground true across a
/// box and not only along a lane.
/// </para>
/// <para>
/// <b>A body it does not describe is a body off the network</b> — one sliding into a lane from a collision,
/// one on ground no way owns. Those are the solver's, and a car under geometry of its own asks the ways
/// beneath that geometry instead (<see cref="GroundAhead"/>).
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>
    /// How many ways one line may be cut into: every lane it is laid over and the join between each pair.
    /// A bound on a stack span and not a figure behaviour reads.
    /// </summary>
    const int MostWaysAlongALine = (PathAssembler.MostLanes * 2) - 1;

    /// <summary>
    /// How many stretches a car under way may put in the index at once <em>on the road it is driving</em>:
    /// the ways its reservation runs over — which begins at its own tail, is a stopping distance long, and
    /// crosses a junction where one falls inside it — and the one way it can claim.
    /// </summary>
    const int MostSlotsPerDrivingCar = 8;

    /// <summary>
    /// From how many places a body that is <em>not</em> driving a route is laid: both ends of the sweep it is
    /// committed to (<see cref="WhereTheTemplateSweepEndsM"/>), which for anything not on a template are the
    /// same place. Each costs the lane that end is nearest and every join of the junctions at either end of
    /// that lane (<see cref="LieInTheBox"/>).
    /// </summary>
    const int LyingPassesPerCar = 2;

    /// <summary>
    /// And how many any car may, which is the wider of the two shapes one can be in: a car under way, plus
    /// the runs of <em>its own</em> join the crossings on it take off it (<see cref="PlaceTheCrossing"/>);
    /// or a body standing still, laid where it lies and — where it is driving a template — at either end of
    /// the sweep that template has still to make (<see cref="WhereTheTemplateSweepEndsM"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One over the runs, because the run the car's own road ends inside is the one that comes in two
    /// pieces</b> (<see cref="LayTheCrossing"/>). A reservation is one interval, so it can split one run and
    /// no more.
    /// </para>
    /// <para>
    /// <b>Nothing here is sized by the ways a car is driven <em>over</em></b> (TER-5c). A car takes ground
    /// on the ways it drives and on no others; where its line crosses another way, the grant is cut by
    /// looking that way up (<see cref="WhereTheGroundIsCrossed"/>) rather than by writing a stretch onto it.
    /// </para>
    /// </remarks>
    static int MostSlotsPerCar(RoadGraph roads) =>
        Math.Max(
            MostSlotsPerDrivingCar + roads.Crossings.MostOwnRuns + 1,
            LyingPassesPerCar * (1 + (2 * roads.MostTurnsAtANode)));

    /// <summary>
    /// How many stretches one walker may put in the <em>road's</em> book: the bands of a crossing it is
    /// standing on and the one in front of it, which at worst is every lane the crossing is painted
    /// across, or the single stretch of lane a body standing on bare tarmac covers.
    /// </summary>
    /// <remarks>
    /// Never less than one, because a town with no paint on it still has people who can stand in a road —
    /// and a dropped stretch here is a body no driver's grant is cut at.
    /// </remarks>
    static int MostRoadSlotsPerWalker(LaneFurniture furniture) => Math.Max(1, furniture.MostLanesUnderACrossing);

    /// <summary>
    /// <b>The index rebuilt from the bodies</b>, in phase 2, before any driver has decided anything. Every
    /// reader this tick therefore sees the same book, whatever tick its own decision clock came round on.
    /// </summary>
    /// <remarks>
    /// <b>Asked in one walk and granted in the next.</b> What a car is granted is its own stretch cut at the
    /// near edge of the nearest one already spoken for, and that near edge is a fact about a body and its
    /// speed rather than about who was served first — so every ask goes into the book before any of them is
    /// answered, and no car has to be ordered against another to get the same answer either way round.
    /// </remarks>
    void RebuildLaneOccupancy()
    {
        _occupancy.Begin();
        _standing.LayInto(_occupancy);

        // Where every walker stands on the pavement's own network, before either book is laid: the road's
        // book needs it to say which lane a body on the paint stands in, and the pavement's book lays
        // that body's own ask from it.
        for (var person = 0; person < People.Count; person++) StationTheWalker(person);

        Span<LineWay> ways = stackalloc LineWay[MostWaysAlongALine];
        for (var car = 0; car < Cars.Count; car++)
        {
            PlaceWhatIsNotDriving(car);
            PlaceTheClaim(car);

            // The ask before the crossing, because the crossing is only the ground the ask did not reach
            // (<see cref="LayTheCrossing"/>) — laid the other way round it would be clipped against the
            // stretch this car held a tick ago.
            AskForTheGround(car, ways);
            PlaceTheCrossing(car);
        }

        for (var car = 0; car < Cars.Count; car++) GrantTheGround(car, ways);

        for (var person = 0; person < People.Count; person++) PlaceTheWalkerOnTheRoad(person);
    }

    /// <summary>
    /// <b>A person on the carriageway, written into the road's book</b>: the bands of the crossing under
    /// them their body covers, or the stretch of lane a body standing on bare tarmac covers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The two are one fact and are laid together.</b> A walker on a lane cuts the road a driver is
    /// granted wherever it stands, and paint changes only which stretch of road it takes: on a crossing it
    /// is the band of the lane it stands in rather than the stretch of lane its body covers, because a
    /// body crossing may be anywhere along the depth of the paint before a driver reaches it. On bare
    /// tarmac it is the body and nothing more — `P-12` is about paint, and a walker that steps into a lane
    /// where nothing is painted is owed a driver who can stop and no ground beyond itself (`PER-12`).
    /// </para>
    /// <para>
    /// <b>The lane it is standing in, and the one in front of it once that one is granted</b> (`PER-15`,
    /// TER-4c.1). A lane it has cleared is given back the moment it is out of it — a car that has been walked
    /// past has nothing in front of it — and a lane further on than the next was never this body's to ask
    /// for. <b>A zebra is crossed a lane at a time</b>, and each lane of it is asked for on the terms every
    /// other piece of the town is asked for on.
    /// </para>
    /// <para>
    /// <b>It is laid by the body and not searched for by the cars.</b> Where a person is standing is a fact
    /// about that person; asked as a question about a patch of ground it was a proximity query per crossing
    /// per approaching car per tick, and it answered yes for anybody merely walking past a zebra.
    /// </para>
    /// <para>
    /// <b>The band in front is ground the body cannot reach from where it stands, so it is asked for and
    /// answered rather than simply taken</b> (<see cref="MayStepOnto"/>): granted where no car's road is over
    /// it, and refused where one is — which is the whole of what waiting for a gap is, and the whole of what
    /// makes a zebra safe to step onto. <b>Granted, it is this body's</b>: the traffic in that lane is cut at
    /// it and the walker needs nobody's leave to walk into it. A signal a body is refused by (PER-7.3) asks
    /// for nothing at all — a walker on its own red holding the traffic on its green would be the crossing
    /// working backwards.
    /// </para>
    /// </remarks>
    void PlaceTheWalkerOnTheRoad(int person)
    {
        People.RefusedWay[person] = PersonFleet.NoWay;

        // PHY-7: inside a container there is no body in the world and nothing in anybody's way.
        if (People.Inside[person].Any || People.Dead[person]) return;

        if (!OnACrossing(person, out var edge, out var alongM))
        {
            People.WaitingForLane[person] = PersonFleet.NoLane;
            StandInTheRoad(person);
            return;
        }

        var paintM = PaintClaimM(_bands.CrossingOf(edge));
        var claimM = People.RadiusM[person] * _config.Person.RoadClaimMargin;
        var backM = alongM - claimM;
        var frontM = alongM + claimM;

        // How far in front of itself this body is asking for ground at all. A band further off than that
        // is one it has not asked for: a stride into the near lane is not a reason to stop the traffic in
        // the far one, and what a body may take is what its own ask reaches — the same bar a car's road is
        // held to. At a kerb the body stands short of the way and its own metre is unknown, so the lane it
        // is about to step into is always in reach and `P-3` is what decides it.
        var reachM = float.IsFinite(alongM) ? alongM + WantsAheadM(person) : float.PositiveInfinity;

        var lookedAhead = false;
        foreach (var band in _bands.On(edge))
        {
            // Behind the body and given back: a car that has been walked past has nothing in front of it.
            if (band.ToM < backM) continue;

            // <b>Standing in the lane it was waiting for is the wait being over</b>, and not the traffic
            // having given way: the patience that bought this ground is spent when the body is on it.
            if (band.FromM <= frontM && People.WaitingForLane[person] == band.Lane)
            {
                People.WaitingForLane[person] = PersonFleet.NoLane;
                People.WaitingToCrossS[person] = 0f;
            }

            if (band.FromM > frontM)
            {
                // Ahead of it, and only the next lane: a body asks for the ground it is about to be on and
                // never for the lane after that, exactly as a car asks for the road it can stop in.
                if (lookedAhead || band.FromM > reachM) continue;

                lookedAhead = true;
                if (!MayStepOnto(person, band, paintM))
                {
                    // What it is standing here for, so that the patience it spends is spent on this lane
                    // and given back when it is standing in it.
                    People.WaitingForLane[person] = band.Lane;

                    // And where the walk it is on runs out, which is this same answer said in the other
                    // network's metres (<see cref="WhereTheWalkRunsOut"/>).
                    People.RefusedWay[person] = _footfall.WayOfLane(edge);
                    People.RefusedAtM[person] = band.FromM;
                    continue;
                }
            }

            _occupancy.Add(
                _occupancy.WayOfLane(band.Lane), band.AlongLaneM - paintM, band.AlongLaneM + paintM, 0f,
                person, LaneUse.OnFoot, LaneRoster.Walking);
        }
    }

    /// <summary>
    /// <b>The answer to the ask for the band in front</b> (TER-4c.1): granted where no car's road is over
    /// that band, and refused where one is. <b>The kerb is only where the body happens to be standing when
    /// it asks</b> — one at a lane's edge half way over is asking the same question about the same strip of
    /// road, and the answer cannot turn on which side of the kerb line the asker is.
    /// </summary>
    /// <remarks>
    /// <b>And granted regardless past the patience</b>: PER-15's escape from a crossing that never clears,
    /// wherever the body has got to on it, and the one thing that makes a wait a wait rather than a jam. It
    /// is the single place in the town where ground is taken that somebody else's road is over, so the cars
    /// give way to it — which is what a pedestrian's priority costs, spent by the clock and by nothing else.
    /// </remarks>
    bool MayStepOnto(int person, CrossingBands.Band band, float paintM) =>
        Kerb.BandIsFree(_occupancy, band, paintM)
        || People.WaitingToCrossS[person] >= _config.Person.KerbPatienceS;

    /// <summary>
    /// How much of a lane a body on this crossing's paint is owed, measured along the way the traffic runs:
    /// a stride either side of the paint — what a body covers in the time a driver has to do anything about
    /// it — and the margin a body on a road is owed over that.
    /// </summary>
    float PaintClaimM(int crossing) =>
        ((_plan.Crosswalks.DepthM[crossing] * 0.5f) + _config.PersonDiameterM) * _config.Person.RoadClaimMargin;

    /// <summary>
    /// A body standing on the carriageway with no paint under it, as the stretch of lane it covers. <b>Where
    /// it lies and not where it is going</b> — a walker off the network, one knocked over, one pacing a road
    /// on purpose (`PER-12`) and one a hand is steering are the same fact to whoever is driving up behind.
    /// </summary>
    void StandInTheRoad(int person)
    {
        var positionM = People.PositionM[person];
        if (!_terrain.At(positionM).Drivable) return;

        var lane = _roads.NearestLane(positionM, out var alongM);
        if (lane < 0) return;

        var radiusM = People.RadiusM[person];
        if (!RoadGraph.WithinTheBand(
                _roads.ArcsOf(lane), alongM, positionM, _roads.LaneWidthM[lane], radiusM, radiusM, out var on))
        {
            return;
        }

        var claimM = radiusM * _config.Person.RoadClaimMargin;
        _occupancy.Add(
            _occupancy.WayOfLane(lane), alongM - claimM, alongM + claimM,
            Vector2.Dot(People.VelocityMps[person], on.Direction), person, LaneUse.OnFoot, LaneRoster.Walking);
    }

    /// <summary>
    /// The way of a crossing this walker is standing on, and how far along it the body stands — or the way
    /// it is about to step onto, at its own start. <b>The paint underfoot comes first</b>: a body halfway
    /// across has the next crossing of its line ahead of it as well, and the one it is standing on is the
    /// one the traffic has to know about.
    /// </summary>
    /// <remarks>
    /// <b>A way and not a crossing</b>, because a lane's band falls at different metres on each of the ways
    /// a zebra is made of (<see cref="CrossingBands"/>) — and it is the way the body is actually walking,
    /// so which side of the road it started from is a fact the book already has.
    /// </remarks>
    bool OnACrossing(int person, out int edge, out float alongM)
    {
        edge = CityPlan.NoRecord;
        alongM = 0f;
        if (!People.Walking[person]) return false;

        var way = People.OnWay[person];
        if (way != PersonFleet.NoWay && _footfall.WayIsLane(way)
            && _bands.CrossingOf(_footfall.WayIndex(way)) >= 0)
        {
            edge = _footfall.WayIndex(way);
            alongM = People.OnWayM[person];
            return true;
        }

        // About to step off. A red is the whole refusal and a body held by one is going nowhere, so it
        // holds no paint; a body waiting for a gap is about to take one and does.
        var ahead = People.CrossingAhead(person);
        if (ahead < 0) return false;
        if (_signals.CrossingIsLit(ahead) && _signals.ForCrossing(ahead, _elapsedS) != SignalColour.Green)
        {
            return false;
        }

        // At the kerb the body stands short of the way's own start, so it covers no band of it however far
        // back it is standing, and the first band is the one in front of it.
        alongM = float.NegativeInfinity;
        return TheWayItStepsOnto(person, ahead, out edge);
    }

    /// <summary>
    /// Which way of the crossing ahead this walker's own line steps onto, read off that line. <b>The
    /// mitre onto the paint is not it</b>: a corner belongs to the stretch it leads onto and carries that
    /// stretch's crossing, so the way is the first point of the crossing that stands on a lane of it.
    /// </summary>
    bool TheWayItStepsOnto(int person, int crossing, out int edge)
    {
        var crossings = People.WalkedCrossingOf(person);
        var codes = People.WalkedWayOf(person);
        for (var at = People.WalkedAt(person) + 1; at < People.WalkedCount[person]; at++)
        {
            if (crossings[at] != crossing || codes[at] < 0) continue;

            edge = codes[at];
            return true;
        }

        edge = CityPlan.NoRecord;
        return false;
    }

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

    /// <summary>
    /// Where a car that is <em>not</em> driving a route of its own stands: on the lane it is nearest and
    /// on every join of a junction it is lying in, as the obstruction it is. <b>A car under way is placed
    /// by its own reservation</b> (<see cref="AskForTheGround"/>), which begins at that car's tail and is
    /// the only stretch it has.
    /// </summary>
    void PlaceWhatIsNotDriving(int car)
    {
        if (IsUnderWay(car)) return;

        var standingM = Cars.PositionM[car];
        var sweeping = WhereTheTemplateSweepEndsM(car, standingM, out var committedToM);

        // A car standing in a bay is on no lane by construction — a bay stands off the kerb — and a town's
        // parked cars are most of its fleet. Asking the road where each of them is, every tick, for an
        // answer the reach test below would throw away, was the whole cost of this pass. One driving a
        // template out of a bay is a sweep across the lane and is held like any other.
        if (!sweeping && _parking.BayOf(car) >= 0) return;

        // Anything else is wherever it actually lies, which is a question for the road and not for a line
        // it is no longer on: a wreck, a car nobody is in, a body shoved off its own route, and the swerve
        // halfway across the oncoming lane are all the same fact to whoever is coming up behind.
        LieUnder(car, standingM, committedToM);

        // Read from both ends, because the ways under one end of a sweep are regularly not the ways under
        // the other — and from each end the stretch is the whole sweep, so a way both ends are over is laid
        // once and identically (<see cref="LieOnTheWay"/>).
        if (sweeping) LieUnder(car, committedToM, standingM);
    }

    /// <summary>
    /// <b>Where a car driving a template of its own is committed to being</b>: the far end of that line, read
    /// for the middle of the body rather than for the axle the line is drawn for, and in whichever gear the
    /// line is driven. <b>False where there is no sweep left to make</b>, and the body's ground is the body.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A template is walked before it is laid and held for as long as it is driven</b> (TER-4c.1). Held
    /// only where the body had got to, the ground a recovery straight was drawn through was ground the
    /// traffic was free to come to rest on — checked once at the moment of laying, and then reversed into.
    /// </para>
    /// <para>
    /// <b>A line already driven out is not a sweep</b>, which is what tells a car working through a template
    /// from one standing at the end of the one it has finished: a parked car keeps the line that put it in
    /// the bay, and a town's parked cars holding a body of road apiece is the whole fleet holding road.
    /// </para>
    /// </remarks>
    bool WhereTheTemplateSweepEndsM(int car, Vector2 standingM, out Vector2 endsM)
    {
        endsM = standingM;

        var line = Cars.Line[car];
        if (line.ArcCount == 0 || line.LaneCount > 0 || !Cars.Driven[car] || Cars.Broken[car]) return false;
        if (line.LengthM <= Cars.ProgressM[car]) return false;

        var at = Spline.SampleAt(Cars.LineArcsOf(car)[..line.ArcCount], line.LengthM);
        var forward = Heading.Unit(at.HeadingRad);

        endsM = at.PositionM + ((Cars.LineIsReverse[car] ? -forward : forward) * _config.CarCentreAheadOfAxleM);
        return true;
    }

    /// <summary>
    /// One end of a body's ground, laid onto the lane that end is nearest and onto every join of a junction
    /// it is lying in — as the whole stretch between the two ends, wherever the reading was taken from.
    /// </summary>
    /// <remarks>
    /// <b>Read from both ends and laid once</b>: which ways an end is over is a question about that end, and
    /// a body askew of the lane it is nearest is on none of it — so an end whose own band test fails is not
    /// an end that can answer for the other. What keeps the two readings to one stretch is the book
    /// (<see cref="LaneOccupancy.AlreadyHolds"/>) and never the order they were taken in.
    /// </remarks>
    void LieUnder(int car, Vector2 atM, Vector2 sweptToM)
    {
        var lane = _roads.NearestLane(atM, out var alongM);
        if (lane < 0) return;

        // Half a car either way, which is what a body lying askew covers along a way at worst. It is the
        // conservative reading of a pose the index deliberately does not carry the angle of.
        var halfM = _config.Car.LengthM * 0.5f;
        LieOnTheWay(
            car, _occupancy.WayOfLane(lane), _roads.ArcsOf(lane), alongM, atM, sweptToM,
            _roads.LaneWidthM[lane], halfM);
        LieInTheBox(car, lane, alongM, atM, sweptToM, halfM);
    }

    /// <summary>
    /// A body laid onto one of the town's ways where it stands inside that way's own band, and left off
    /// where it does not.
    /// </summary>
    /// <remarks>
    /// <b>The band and the body's own width</b>, and not how far the body is off the line. A wreck shoved
    /// sideways is still standing in what it was shoved into, and since the book is the whole of what a
    /// driver looks at (TER-4c), one left out of it here is one nothing can see: the reach a line's own
    /// tolerance allows is a bar on whether a car is still *driving* that line, which is a different
    /// question and a tighter one.
    /// <para>
    /// <b>And half a body past either end of the way, which is the same conservative reading taken along
    /// it</b>: the stretch laid below runs half a car either side of where the body projects, so a body
    /// standing further past the end than that is standing on nothing of this way.
    /// </para>
    /// <para>
    /// <b>A body on a template of its own is laid over the whole sweep it is committed to</b>
    /// (<see cref="WhereTheTemplateSweepEndsM"/>) and not only over the pose it is passing through: the
    /// ground a manoeuvre is about to be on is ground it is holding. The stretch reaches no further along
    /// this way than the sweep itself is long, because a template leaves the way it started on and a point
    /// projected onto a way it has left lands wherever that way happens to bend nearest to it.
    /// </para>
    /// <para>
    /// <b>And it is laid once</b> (TER-5c.2), whichever end of the sweep it was read from — the stretch is
    /// the same interval either way round, so the second reading is the first one over again and the book
    /// would count one body as two.
    /// </para>
    /// </remarks>
    void LieOnTheWay(
        int car, int way, ReadOnlySpan<ArcSeg> arcs, float alongM, Vector2 atM, Vector2 sweptToM, float bandM,
        float halfM)
    {
        if (!RoadGraph.WithinTheBand(arcs, alongM, atM, bandM, _config.Car.WidthM * 0.5f, halfM, out var on))
        {
            return;
        }

        var sweptM = (sweptToM - atM).Length();
        var farM = sweptM <= 0f ? alongM : Spline.ProjectM(arcs, sweptToM, alongM, sweptM);
        var fromM = MathF.Min(alongM, farM) - halfM;
        var toM = MathF.Max(alongM, farM) + halfM;
        if (_occupancy.AlreadyHolds(way, fromM, toM, car)) return;

        _occupancy.Add(
            way, fromM, toM, Vector2.Dot(Cars.VelocityMps[car], on.Direction), car, LaneUse.Obstruction);
    }

    /// <summary>
    /// <b>A body standing in a junction, laid onto every one of that junction's joins it is lying under</b>
    /// — which is the whole of what holds the traffic crossing the box off it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A body in a box is on no lane anybody drives.</b> Past a lane's own setback (TER-5d) the lane's
    /// line runs on into the junction under a movement rather than under itself, and no driver's line is
    /// laid over that stretch (<see cref="WaysAlong"/>) — so a stretch put there is one nothing walks. The
    /// ground a car actually crosses a junction on is the join, and the join is a way of the road like any
    /// other.
    /// </para>
    /// <para>
    /// <b>What a car is crossing on cannot be what answers this.</b> It is given back the moment nobody is
    /// driving it (<see cref="PlaceTheCrossing"/>), and a wreck, a car under a hand and a body
    /// shoved into a box on no movement at all are none of them making anything — so what refuses the
    /// traffic crossing them is the ground they are lying on, and there is nothing else left to say it.
    /// </para>
    /// <para>
    /// <b>Both ends of the nearest lane are asked, and the setbacks are what say which of them can be
    /// it</b> (TER-5d): past one the ground stops being the lane's, and a lane that hands nothing over at
    /// that end still ends at a junction whose other arms are driven across it. A lane shorter than the
    /// junctions either side of it answers to both, which is why this is two questions and not a choice.
    /// </para>
    /// </remarks>
    void LieInTheBox(int car, int nearest, float alongM, Vector2 atM, Vector2 sweptToM, float halfM)
    {
        if (alongM <= _roads.JoinedAtM(nearest))
        {
            LieUnderTheJoins(car, _roads.LaneFromNode[nearest], atM, sweptToM, halfM);
        }

        if (alongM >= _roads.LaneLengthM[nearest] - _roads.LeftAtM(nearest))
        {
            LieUnderTheJoins(car, _roads.LaneToNode[nearest], atM, sweptToM, halfM);
        }
    }

    /// <summary>
    /// The joins of one junction, and this body laid onto each of them it is lying under — <b>except the one
    /// it is crossing</b>, where the ground it holds is the crossing it is making
    /// (<see cref="LayTheCrossing"/>). One body is one stretch of one way (TER-5c.2), and the two readings
    /// are of one piece of ground in two measures: a projection across the box, and the metres of the line
    /// the car was driving down it.
    /// </summary>
    void LieUnderTheJoins(int car, int node, Vector2 atM, Vector2 sweptToM, float halfM)
    {
        foreach (var arriving in _roads.LanesIn(node))
        {
            for (var turn = 0; turn < _roads.TurnsFrom(arriving).Length; turn++)
            {
                var slot = _roads.TurnSlotAt(arriving, turn);
                if (slot == Cars.Crossing[car] && Cars.Driven[car] && !Cars.Broken[car]) continue;

                var arcs = _roads.JoinArcs(slot);
                if (arcs.Length == 0) continue;

                var lengthM = _roads.JoinLengthM(slot);
                LieOnTheWay(
                    car, _occupancy.WayOfTurn(slot), arcs,
                    Spline.ProjectM(arcs, atM, lengthM * 0.5f, lengthM), atM, sweptToM,
                    _roads.LaneWidthM[arriving], halfM);
            }
        }
    }

    /// <summary>The stretch this car has claimed and is not on yet, laid where its own field says.</summary>
    /// <remarks>
    /// <para>
    /// Re-laid from the car every tick, so nothing is released and nothing leaks: a claim held by a car that
    /// has been wrecked, unmanned or taken over by a hand is gone on the next rebuild without anything
    /// having had to notice it.
    /// </para>
    /// <para>
    /// <b>And never over ground this body is already holding</b> (TER-5c.2). A car waits in the mouth of its
    /// bay on a claim and then leaves on a template, and the sweep that template makes is over the very lane
    /// the claim was taken on (<see cref="PlaceWhatIsNotDriving"/>) — one body, laid twice, in two measures
    /// of one piece of ground. The sweep is the reading taken from the body and it is the one that stands.
    /// </para>
    /// </remarks>
    void PlaceTheClaim(int car)
    {
        var way = Cars.ClaimWay[car];
        if (way < 0) return;

        if (!Cars.Driven[car] || Cars.Broken[car])
        {
            Cars.ClaimWay[car] = CarFleet.NoWay;
            return;
        }

        var fromM = Cars.ClaimFromM[car];
        var toM = Cars.ClaimToM[car];
        if (_occupancy.AlreadyHolds(way, fromM, toM, car)) return;

        _occupancy.Add(way, fromM, toM, Cars.AlongMps[car], car, LaneUse.Claimed);
    }

    /// <summary>
    /// <b>The road this car is committed to, and the car</b>: one stretch, from its own tail through its
    /// nose to where that nose comes to rest if it holds the pedal it has until its next decision and then
    /// stops, plus the gap it keeps.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is what the car cannot undo and not what it would like</b> — one reaction interval of ground at
    /// the fastest that interval can leave it doing, and a stop from there. A car planning for a speed it is
    /// nowhere near does not hold the road up to it: the profile's own figure is the ceiling on this and
    /// never the size of it, or a car pulling out of a junction would reserve the two hundred metres its top
    /// speed would take to shed and hold a street shut to do it.
    /// </para>
    /// <para>
    /// <b>And it still leaves the room to pull away</b>, which sizing by the speed the car is doing would
    /// not: a stopped car is committed to whatever a reaction interval of its own acceleration reaches, so
    /// what it asks for grows with the pedal rather than with the speed the pedal has produced. The grant
    /// that comes back off an uncut ask always inverts to at least that same speed, so this bounds nothing
    /// the car could actually have done.
    /// </para>
    /// <para>
    /// <b>And never past the place a rule holds it at</b> — a red, a bar, a crossing it must stop short of, a
    /// box whose ground is somebody's (S-4, TER-4c.1). Ground beyond a stop point is ground the car is not
    /// committed to whatever its pedal is doing, and holding it would queue the traffic behind further up the
    /// road than any of it is going to get — and, where the stop point is a zebra, would hold the paint shut
    /// against the very people the stop was made for.
    /// </para>
    /// <para>
    /// <b>The margin it keeps is part of what it asks for, at both ends of it</b>
    /// (<see cref="SimConfig.CarBodyMarginM"/> in front, <see cref="SimConfig.CarTailMarginM"/> behind). In
    /// front it is clamped with the rest of the ask — added after the clamp it was a margin of ground held
    /// past every bar in the town, and a car stopped a metre short of a crossing held a metre of the
    /// crossing. Behind, it is the ground the book's one-dimensional reading of a swinging body owes whoever
    /// comes next, and <b>it is a stretch of this same reservation on every way the car is on</b> rather than
    /// a claim of its own on a junction's join: one body, one stretch, one piece of ground (TER-5c.2).
    /// </para>
    /// </remarks>
    void AskForTheGround(int car, Span<LineWay> ways)
    {
        // Where this car's ground begins is a fact about the body and is filled for every car with a line,
        // driven or not: what says it asked for no road is that the stretch has no length
        // (<see cref="PastOnTheCrossing"/> reads the near edge of a car that is going nowhere).
        var noseM = Cars.ProgressM[car] + _config.CarNoseAheadOfAxleM;
        Cars.ReserveFromM[car] = noseM - _config.Car.LengthM - _config.CarTailMarginM;
        Cars.ReserveToM[car] = Cars.ReserveFromM[car];
        Cars.AuthorityM[car] = float.PositiveInfinity;
        if (!IsUnderWay(car)) return;

        var brakingMps2 = CarFollower.BrakingMps2(_config, Cars.GroundCoefficient[car]);
        var reachableMps = MathF.Min(
            Cars.PlannedMps[car], Cars.AlongMps[car] + (_config.Car.AccelerationMps2 * _config.CarReactionS));
        var committedM = (reachableMps * _config.CarReactionS) + StoppingM(reachableMps, brakingMps2)
                         + _config.CarBodyMarginM;
        var heldAtM = MathF.Min(Cars.Context[car].StopAtM, Cars.Context[car].CrossingStopM);
        var wantedM = MathF.Max(StoppingM(Cars.AlongMps[car], brakingMps2), MathF.Min(committedM, heldAtM));

        Cars.ReserveToM[car] = noseM + wantedM;

        var count = WaysAlong(car, Cars.ReserveFromM[car], Cars.ReserveToM[car], ways);
        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref ways[index];
            _occupancy.AddUnderWay(
                way.Way, way.FromM, OnTheWayM(way, noseM), way.ToM, Cars.AlongMps[car], car);
        }
    }

    /// <summary>
    /// Where a place on a line falls in the own metres of one of the ways under it, held to the stretch of
    /// that way the caller is laying. <b>Past the nose the answer is the near edge</b>: a way the body has
    /// not reached carries the grant and none of the car.
    /// </summary>
    static float OnTheWayM(in LineWay way, float lineM) =>
        Math.Clamp(way.FromM + (lineM - way.LineFromM), way.FromM, way.ToM);

    /// <summary>
    /// <b>The town's furniture, projected onto the lanes it stands on</b>, once, before the first tick — so
    /// that what a driver has to be held off is one book and not a book and a ray.
    /// </summary>
    /// <remarks>
    /// <b>The nearest lane and the one running back the other way</b>, and no wider a search than that. A
    /// prop is a street's furniture and a carriageway is two lanes, so those are the ones a thing standing
    /// in the road can be standing in; anything broad enough to reach a third is a town that has built a
    /// wall across its own street, which is <see cref="StandingGround"/>'s stated bound.
    /// </remarks>
    StandingGround StaticsOnTheRoad()
    {
        var into = new StandingGround.Builder();
        for (var prop = 0; prop < _plan.Props.Count; prop++)
        {
            var centreM = _plan.Props.CentreM[prop];
            var radiusM = _plan.Props.RadiusM[prop];

            var lane = _roads.NearestLane(centreM, out _);
            if (lane < 0) continue;

            CoverTheLane(into, lane, centreM, radiusM);

            var back = _roads.LaneReverse[lane];
            if (back >= 0) CoverTheLane(into, back, centreM, radiusM);
        }

        return into.Seal();
    }

    /// <summary>
    /// The stretch of one lane a circle standing beside it covers, or nothing where it stands clear of the
    /// lane's own band.
    /// </summary>
    void CoverTheLane(StandingGround.Builder into, int lane, Vector2 centreM, float radiusM)
    {
        var arcs = _roads.ArcsOf(lane);
        var lengthM = _roads.LaneLengthM[lane];
        var alongM = Spline.ProjectM(arcs, centreM, lengthM * 0.5f, lengthM);
        if (!RoadGraph.WithinTheBand(arcs, alongM, centreM, _roads.LaneWidthM[lane], radiusM, radiusM, out _))
        {
            return;
        }

        into.Add(lane, alongM - radiusM, alongM + radiusM);
    }

    /// <summary>How much road a body doing this speed needs before it can be at rest on the ground it is on.</summary>
    static float StoppingM(float alongMps, float brakingMps2) =>
        alongMps <= 0f ? 0f : alongMps * alongMps / (2f * brakingMps2);

    /// <summary>
    /// <b>What the car actually got</b>: its own stretch, cut at the nearest place anything in front of it
    /// will come to rest, as a distance from its nose to the far end of the room it may stop in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The cut is at the resting place of what is in front and not at its bumper.</b> A stretch is
    /// answered at the near edge of the ground that body has — its own tail less the margin it keeps
    /// (<see cref="SimConfig.CarBodyMarginM"/>), which is what a queue at rest stands at — and what the
    /// ground beyond that edge is worth to this car is that body's own stopping distance — nothing for a wreck or a claim, which are not going
    /// anywhere, and a whole stopping distance for a car at speed. Two cars running at the same speed
    /// therefore keep a standing gap rather than opening out to a stopping distance apiece.
    /// </para>
    /// <para>
    /// <b>The least of them and never the nearest.</b> A car at speed rests further up the road than a
    /// slower car ahead of it, so the stretch whose near edge comes first is regularly not the one that
    /// binds — and cutting at it grants this car the road through whatever is beyond.
    /// </para>
    /// <para>
    /// <b>The credit is worked out against the grip this car has and not the grip that one has.</b> How
    /// fast the thing in front is going is something a driver can see; what it is standing on is not, and
    /// a book that handed out the answer would be handing out a reading no driver could take.
    /// </para>
    /// <para>
    /// <b>And it is cut by the ways this car is driven <em>over</em> as well as by the ways it is
    /// driving</b> (<see cref="WhereTheGroundIsCrossed"/>). A reservation is a lane's, and the ground is the
    /// town's: two ways that meet inside a junction are one piece of the world, so a grant that stopped at
    /// the edge of its own way would be two cars each given the metre they meet on.
    /// </para>
    /// <para>
    /// <b>A car nothing cut is held by nobody</b>, and its grant stays infinite rather than coming back as
    /// the length of its own ask. The ask is what this car is committed to and the profile has already
    /// bound itself to it; handing it back as a limit would make a car alone on an empty road read as one
    /// queueing behind itself.
    /// </para>
    /// <para>
    /// Negative where the car cannot stop in what is left, which is a fact about a contact rather than
    /// about a gap and is left to say so.
    /// </para>
    /// </remarks>
    void GrantTheGround(int car, Span<LineWay> ways)
    {
        if (Cars.ReserveToM[car] <= Cars.ReserveFromM[car]) return;

        var brakingMps2 = CarFollower.BrakingMps2(_config, Cars.GroundCoefficient[car]);
        var grantedToM = float.PositiveInfinity;

        var noseM = Cars.ProgressM[car] + _config.CarNoseAheadOfAxleM;
        var count = WaysAlong(car, Cars.ReserveFromM[car], LookForTheCutToM(car, brakingMps2), ways);
        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref ways[index];

            // In front of the nose and not of the ground this car holds: every stretch begins a margin
            // behind its owner, so a walk taken from the near edge of this car's own would answer with the
            // body behind it (<see cref="LaneOccupancy.NextSpokenFor"/>).
            var at = LaneOccupancy.FromTheStart;
            while (_occupancy.NextSpokenFor(
                       way.Way, OnTheWayM(way, noseM), way.ToM, car, ref at, out var taken))
            {
                // A wreck and a claim are going nowhere, so where they lie is where they rest; a driver at
                // speed is worth its own stopping distance beyond its tail. <b>And a reservation is the one
                // stretch that carries a margin of its own</b> (TER-5c.2), so the cut at one is where its
                // ground begins — where a wreck, a claim, a body on foot and the town's own furniture have
                // none, and this car keeps its own off them.
                var creditM = taken.Use == LaneUse.Reserved
                    ? StoppingM(taken.AlongMps, brakingMps2)
                    : -_config.CarBodyMarginM;

                grantedToM = MathF.Min(grantedToM, way.LineFromM + (taken.FromM - way.FromM) + creditM);
            }

            // A crossing point is a place and has no margin of its own, so the asker's is taken off it here
            // — the one cut in the town that is not made at somebody else's stretch.
            var crossedAtM = WhereTheGroundIsCrossed(car, way);
            if (float.IsFinite(crossedAtM))
            {
                grantedToM = MathF.Min(
                    grantedToM, way.LineFromM + (crossedAtM - way.FromM) - _config.CarBodyMarginM);
            }
        }

        if (float.IsPositiveInfinity(grantedToM)) return;

        Cars.AuthorityM[car] = grantedToM - noseM;
    }

    /// <summary>
    /// <b>The first metre of one of this car's own ways that somebody else's ground is driven over</b>, in
    /// that way's own metres, or infinity where none of it is. The town says once, when it is laid, where
    /// each way through a junction crosses each other one (<see cref="JunctionCrossings"/>); a driver looks
    /// its own way up in that table and reads the far side of every section it finds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the whole of what makes a lane's reservation mean something globally.</b> A stretch of a
    /// way is a piece of the world and not a piece of a lane, and inside a junction the pieces overlap; a
    /// car that only ever read the way it was driving would be granted the metre two lines meet on at the
    /// same time as the car on the other line. What it does not do is take ground it will never be on —
    /// which is what marking the crossed ways did, and what a junction under a fan of one car's claims
    /// looked like.
    /// </para>
    /// <para>
    /// <b>In front means the section's own near edge is</b>, exactly as it does for a stretch
    /// (<see cref="LaneOccupancy.NextSpokenFor"/>): a crossing point behind the car's tail is one this body
    /// has been over, and a grant cut at it would be a car braking for the corner it came in by.
    /// </para>
    /// <para>
    /// <b>The cut is at the near edge of the section and carries no credit past it</b>, where the cut at a
    /// body carries that body's stopping distance. There is nothing on the section to come to rest — it is a
    /// place, and what is standing on it is standing on another way's metres, at a pose and a heading this
    /// car has no reading of.
    /// </para>
    /// <para>
    /// Only a join is asked. Two lanes of the town are laid side by side and end where the box begins
    /// (TER-5d), so the ground where movements meet is a join's and the table has nothing to say about the
    /// rest.
    /// </para>
    /// </remarks>
    float WhereTheGroundIsCrossed(int car, in LineWay way)
    {
        if (_occupancy.WayIsLane(way.Way)) return float.PositiveInfinity;

        var leastM = float.PositiveInfinity;
        foreach (ref readonly var section in _roads.Crossings.Of(_occupancy.WayIndex(way.Way)))
        {
            if (section.MineFromM < way.FromM || section.MineFromM > way.ToM) continue;
            if (section.MineFromM >= leastM) continue;

            if (_occupancy.SpokenForByAnother(
                    _occupancy.WayOfTurn(section.OnTurn), section.FromM, section.ToM, car, out _))
            {
                leastM = section.MineFromM;
            }
        }

        return leastM;
    }

    /// <summary>
    /// <b>How far up the line the cut is looked for, which is not how far the ask reached.</b> What a car
    /// lays into the book is the road it is committed to; what it has to be told about is the road it means
    /// to be keeping — the stop, the gap of a following time in front of that, and a body's length of margin
    /// past it.
    /// </summary>
    /// <remarks>
    /// <b>Looking further can only make a grant smaller</b>, because the grant is the least of everything
    /// found — so nothing about the safety of it turns on this figure and only the fluency does. Looked for
    /// no further than the ask, the grant simply went uncut whenever the car was more than a stopping
    /// distance behind: the profile then held the car off on the headway reading, at the reaction lead,
    /// until it had closed to inside its own reservation — and the pair of them settled into closing up and
    /// falling back rather than into a gap.
    /// </remarks>
    float LookForTheCutToM(int car, float brakingMps2)
    {
        var alongMps = MathF.Max(0f, Cars.AlongMps[car]);
        var noseM = Cars.ProgressM[car] + _config.CarNoseAheadOfAxleM;
        return MathF.Max(
            Cars.ReserveToM[car],
            noseM + StoppingM(alongMps, brakingMps2) + (alongMps * _config.Driving.FollowingHeadwayS)
            + _config.CarBodyMarginM + _config.Car.LengthM);
    }

    /// <summary>
    /// Whether this car is a driver on a route rather than a shape on the road — <b>the whole of what the
    /// index is for</b>. A car that is gets queued behind however long it stands; a car that is not gets
    /// driven round.
    /// </summary>
    /// <remarks>
    /// <b>Nothing here is worked out twice.</b> How far off its line the car is was measured by the sensing
    /// half of last tick and is a tick old, which is a tenth of a metre at town speed against a lane three
    /// metres wide; a hand at the wheel is the one case that leaves it standing at whatever the road last
    /// wrote, and it is named apart for that reason.
    /// </remarks>
    bool IsUnderWay(int car)
    {
        if (!Cars.Driven[car] || Cars.Broken[car] || Cars.Line[car].LaneCount == 0) return false;
        if (_hands.Held && _selected.Kind == SelectionKind.Car && _selected.Index == car) return false;

        // The same bar the road holds the car to before it calls the line lost. A car it still considers to
        // be on its line is a car going where that line goes, and reading one as an obstruction for having
        // taken a corner wide is how a driver ends up swerving round traffic.
        return Cars.OffLineM[car] <= _config.CarOffPathM * OffLineTolerance;
    }

    /// <summary>
    /// Where a place on one of this car's lanes falls on the line it is driving — <see cref="WaysAlong"/>'s
    /// own trip, made the other way round for one place instead of in bulk for a stretch.
    /// </summary>
    float OnTheLineM(int car, int slot, float alongLaneM) =>
        PathAssembler.OnTheLineM(
            _roads, Cars.ChainOf(car), Cars.LaneStartsOf(car), Cars.LaneEndsOf(car), slot, alongLaneM);

    /// <summary>
    /// The town's ways under a stretch of one car's line, nearest first — the lanes it is laid over and the
    /// joins threaded between them, each with the metres of its own that the stretch covers.
    /// </summary>
    /// <remarks>
    /// <b>The metres of a way and the metres of a line run at the same rate</b> and differ only by where
    /// each lane's own start falls under the line (<see cref="PathAssembler.LaneOriginM"/>) — the line over
    /// a lane is that lane's own arcs, so a stretch carried across is the same stretch of the same bending
    /// ground and not a chord over it.
    /// </remarks>
    int WaysAlong(int car, float fromLineM, float toLineM, Span<LineWay> into)
    {
        var lanes = Cars.Line[car].LaneCount;
        var chain = Cars.ChainOf(car);
        var starts = Cars.LaneStartsOf(car);
        var ends = Cars.LaneEndsOf(car);

        var written = 0;
        var arrivedOn = RoadGraph.NoTurn;
        for (var index = 0; index < lanes && written < into.Length; index++)
        {
            var leavingOn = index < lanes - 1 ? _roads.TurnSlot(chain[index], chain[index + 1]) : RoadGraph.NoTurn;

            // Where the lane's own metres begin under the line's: the assembler took the line from the
            // setback the arriving join was drawn to, and a lane measures everything from its own start.
            var originM = arrivedOn == RoadGraph.NoTurn ? 0f : _roads.JoinToM(arrivedOn);
            if (Overlaps(fromLineM, toLineM, starts[index], ends[index], out var fromM, out var toM))
            {
                into[written++] = new LineWay(
                    _occupancy.WayOfLane(chain[index]),
                    originM + fromM - starts[index],
                    originM + toM - starts[index],
                    fromM);
            }

            // <b>The stretch runs out at the box's near edge — `ends[index]` — and not at its far one.</b>
            // The join is the ground from there to `starts[index + 1]`, so a stretch ending anywhere inside
            // it still covers some of it; guarded on the far edge, a car approaching a junction lays
            // nothing on it until its own stretch reaches clear across.
            if (leavingOn == RoadGraph.NoTurn || ends[index] >= toLineM) break;

            if (written < into.Length && Overlaps(fromLineM, toLineM, ends[index], starts[index + 1], out fromM, out toM))
            {
                into[written++] = new LineWay(
                    _occupancy.WayOfTurn(leavingOn), fromM - ends[index], toM - ends[index], fromM);
            }

            arrivedOn = leavingOn;
        }

        return written;
    }

    static bool Overlaps(float fromM, float toM, float leastM, float mostM, out float fromOut, out float toOut)
    {
        fromOut = MathF.Max(fromM, leastM);
        toOut = MathF.Min(toM, mostM);
        return toOut > fromOut;
    }

    /// <summary>
    /// <b>What is in front of this car on the road it is driving</b>, out to <paramref name="reachM"/> from
    /// its nose: the nearest body, <b>how far off it is</b>, and how far off the nearest stretch somebody
    /// else has claimed is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The distance is the book's too, and that is the change a ray's going made.</b> Both are stretches
    /// of the same way measured in the same metres, so the gap is a subtraction — where a cast had to walk
    /// three chains through a tree to find a shape it could not then name.
    /// </para>
    /// <para>
    /// <b>It reads the near edge of the body's own stretch and not its shape</b>, which is the conservative
    /// end of it: a body straddling a way is laid over the whole of what it covers, so the gap given is the
    /// short reading and a driver on it follows no closer than one that cast.
    /// </para>
    /// </remarks>
    void AheadOnThePath(int car, float noseM, float reachM, out LaneSlot body, out float bodyM, out float claimM)
    {
        body = LaneSlot.Nothing;
        bodyM = float.PositiveInfinity;
        claimM = float.PositiveInfinity;

        Span<LineWay> ways = stackalloc LineWay[MostWaysAlongALine];
        var count = WaysAlong(car, noseM, noseM + reachM, ways);
        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref ways[index];

            if (!body.Found && _occupancy.AheadBody(way.Way, way.FromM, way.ToM, car, out body))
            {
                bodyM = MathF.Max(0f, way.LineFromM + (body.FromM - way.FromM) - noseM);
            }

            if (float.IsPositiveInfinity(claimM)
                && _occupancy.AheadClaim(way.Way, way.FromM, way.ToM, car, out var claim))
            {
                claimM = MathF.Max(0f, way.LineFromM + (claim.FromM - way.FromM) - noseM);
            }

            if (body.Found && !float.IsPositiveInfinity(claimM)) break;
        }
    }

    /// <summary>What a slot the index laid is to a driver reading it.</summary>
    static HeadwayKind KindOf(LaneUse use) => use switch
    {
        LaneUse.Reserved => HeadwayKind.Queue,
        LaneUse.Claimed => HeadwayKind.Claimed,
        LaneUse.OnFoot => HeadwayKind.Walker,
        _ => HeadwayKind.Obstruction,
    };
}
