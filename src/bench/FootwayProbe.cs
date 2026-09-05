using System.Numerics;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Persistence;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>The walking exam, walked</b>: every card of <see cref="FootwayCards"/> staged at once on the map
/// laid for it, every body ordered to the place its card names, and what each of them did read off the
/// bodies rather than off anything a walker said about itself.
/// </summary>
/// <remarks>
/// <b>The instrument and the test are one machine</b>: <c>--bench footway</c> prints the table and the
/// town suite asserts the same verdicts, so a card cannot pass in one and fail in the other. What each
/// card claims and what this build does instead is the card's own (<see cref="FootwayCard.Finding"/>).
/// </remarks>
internal static class FootwayProbe
{
    public static bool Run(SimConfig config)
    {
        using var world = new TownWorld(
            Maps.Plan(FootwayPlan.Name, config, BuildingCatalog.Shared.OrdinaryFootprintsM()), config);
        var watch = new FootwayWatch(config, world);
        var loop = new SimLoop<TownWorld>(world, config);
        for (var tick = 0; tick < FootwayWalk.Ticks; tick++)
        {
            loop.Advance();
            watch.Saw(world);
        }

        var walk = watch.Walk;
        Console.WriteLine(
            $"walking exam — {FootwayCards.Count} cards on {FootwayPlan.Name}, {FootwayWalk.Ticks} ticks "
            + $"({FootwayWalk.Ticks / config.Sim.TickRateHz} s), {walk.Walkers} bodies ordered at once, no traffic");
        Console.WriteLine();
        Console.WriteLine($"{"card",5}  {"claim",-15}{"verdict",-9} what was asked");

        var passed = 0;
        var outstanding = 0;
        for (var card = 0; card < FootwayCards.Count; card++)
        {
            var of = FootwayCards.All[card];
            var wrong = walk.Verdict(card);
            var known = of.Finding.Length > 0;
            if (wrong is null) passed++;
            else if (known) outstanding++;

            Console.WriteLine(
                $"{card,5}  {of.Asks,-15}{(wrong is null ? "passed" : known ? "known" : "FAILED"),-9}{of.Name}");
            if (wrong is not null) Console.WriteLine($"{"",14}{wrong}");
        }

        Console.WriteLine();
        Console.WriteLine(
            $"{passed} of {FootwayCards.Count} cards walked as written, {outstanding} outstanding findings, "
            + $"{FootwayCards.Count - passed - outstanding} failing.");

        for (var card = 0; card < FootwayCards.Count; card++)
        {
            if (FootwayCards.All[card].Finding.Length > 0)
            {
                Console.WriteLine($"  card {card}: {FootwayCards.All[card].Finding}");
            }
        }

        return ScenarioReport.Print(FootwayPlan.Name, [watch], world.ElapsedS);
    }
}

/// <summary>
/// The walking exam being walked on a town somebody else is ticking: every card ordered on the first
/// tick, and what each body did recorded tick by tick.
/// </summary>
/// <remarks>
/// <para>
/// <b>One run and twenty questions.</b> The cards stand a lattice apart and are ordered on the same
/// tick, so the whole exam is one town ticked once — the alternative, a town a card, is twenty towns
/// laid to ask twenty questions of one engine.
/// </para>
/// <para>
/// <b>The town is the caller's</b>, which is what lets the probe stand one of its own and the game hand
/// over the map somebody has just opened. Both then see the same cards walked the same way, because this
/// is the only thing that stages them.
/// </para>
/// <para>
/// <b>Every body is ordered, including the ones that are furniture.</b> A walker nobody is telling
/// anything wanders this map (<c>TownWorld.WanderInstead</c>) and turns up in somebody else's card, so a
/// body a card wants standing about is ordered to the ground it is already on — which is an order carried
/// out on the tick it is given.
/// </para>
/// </remarks>
internal sealed class FootwayWalk
{
    /// <summary>
    /// A minute of town. The longest card is four blocks at a walking pace, which is under a minute, and
    /// the rest is room for a light to come round and for a body to be got past.
    /// </summary>
    public const int Ticks = 3_600;

    readonly SimConfig _config;
    readonly TownWorld _world;
    readonly FootwayLattice _lattice;
    readonly Walked[] _walked;

    /// <summary>Each card's verdict once it has one, and whether it was taken on a card that had been decided.</summary>
    readonly string?[] _wrong = new string?[FootwayCards.Count];

    readonly bool[] _judged = new bool[FootwayCards.Count];

    int _tick = -1;

    public FootwayWalk(SimConfig config, TownWorld world)
    {
        _config = config;
        _world = world;
        _lattice = FootwayLattice.Of(config);
        _walked = new Walked[_lattice.Walkers];

        Order();
    }

    public int Walkers => _walked.Length;

    /// <summary>How many ticks of the town this has watched, which is what says a card has had its chance.</summary>
    public int Ticked => _tick + 1;

    /// <summary>One tick of the town, seen: every staged body read off its own position and state.</summary>
    public void Saw()
    {
        _tick++;
        for (var walker = 0; walker < _walked.Length; walker++) _walked[walker].Read(_tick, _world, _lattice);
    }

    /// <summary>
    /// <b>Whether a card has been answered at all yet.</b> Every body it stages has arrived where it was
    /// sent, and until then the card is a question still being asked rather than one the engine got wrong.
    /// </summary>
    /// <remarks>
    /// <b>The exam's own window is what says a walk is over</b>, because nothing else does: a leg that runs
    /// out is laid again from where the body stands (PER-8), so a body still standing in the street and a
    /// body about to walk on are the same state — and a walk that never finishes is a finding rather than
    /// a question still open.
    /// </remarks>
    public bool Decided(int card)
    {
        if (Ticked >= Ticks) return true;

        for (var walker = 0; walker < FootwayCards.All[card].Walkers.Length; walker++)
        {
            if (Of(card, walker).ArrivedAt < 0) return false;
        }

        return true;
    }

    public string Name(int card) => $"card {card} ({FootwayCards.All[card].Name})";

    public Walked Of(int card, int walker) => _walked[_lattice.WalkerOf(card, walker)];

    /// <summary>
    /// <b>What was wrong with the way a card was walked, or nothing</b> — judged once, on the tick the card
    /// is decided on, and kept.
    /// </summary>
    /// <remarks>
    /// <b>Kept because it is read every tick and not only at the end.</b> A watch on a running town asks
    /// every decided card where it stands on every tick of the frame it is drawn in, and a message composed
    /// afresh each time would be an allocation a second per failing card.
    /// </remarks>
    public string? Verdict(int card)
    {
        if (_judged[card]) return _wrong[card];

        _wrong[card] = Judge(card);
        _judged[card] = Decided(card);
        return _wrong[card];
    }

    /// <summary>
    /// <b>What was wrong with the way a card was walked, or nothing.</b> The standing claims under every
    /// card are that every body staged got where it was sent and that none of them set foot on a
    /// carriageway off the paint; the card's own claim is about its subject and about nothing else.
    /// </summary>
    string? Judge(int card)
    {
        var of = FootwayCards.All[card];
        for (var walker = 0; walker < of.Walkers.Length; walker++)
        {
            var walked = Of(card, walker);
            if (walked.ArrivedAt < 0)
            {
                return $"{Name(card)}: body {walker} never got to the place it was ordered to — {walked}";
            }

            // PER-7.2 read off the ground rather than off the network: the one edge of the walking network
            // that touches a carriageway is a crossing, so a body on a road it is not painted over is a
            // walk that left the network somewhere.
            if (walked.OffThePaintFor > 0)
            {
                return $"{Name(card)}: body {walker} spent {walked.OffThePaintFor} ticks on a carriageway "
                       + $"off the paint — {walked}";
            }
        }

        var subject = Of(card, 0);
        return of.Asks switch
        {
            WalkAsks.Unhindered when Held(subject) =>
                $"{Name(card)}: nothing was in its way and it was held for {subject.HeldFor} ticks — {subject}",

            WalkAsks.TakesThePaint when subject.OnThePaintFor == 0 =>
                $"{Name(card)}: it got there without ever setting foot on a crossing — {subject}",

            // The arm every claim about a light carries: a card whose body never reached the paint asked
            // the signal nothing, and passing it would say the opposite.
            WalkAsks.EntersOnGreen when subject.OnThePaintFor == 0 =>
                $"{Name(card)}: it never reached the lit crossing it was sent over — {subject}",

            WalkAsks.EntersOnGreen when subject.SteppedOutOnRed =>
                $"{Name(card)}: it stepped onto the paint while its own crossing was showing red — {subject}",

            WalkAsks.StepsRound when subject.SteppedRoundFor == 0 =>
                $"{Name(card)}: it never stepped round the body standing in its way — {subject}",

            WalkAsks.Follows when subject.SteppedRoundFor > 0 =>
                $"{Name(card)}: it stepped round the body under way in front of it for "
                + $"{subject.SteppedRoundFor} ticks — {subject}",

            _ => null,
        };
    }

    /// <summary>
    /// Whether a body was <em>held</em> rather than momentarily stopped. The bar is the staleness a
    /// walker's own decision is allowed (<see cref="SimFigures.AgentDecisionIntervalS"/>): shorter than
    /// that and nothing has decided anything about it yet.
    /// </summary>
    bool Held(Walked walked) => walked.HeldFor > _config.Sim.AgentDecisionIntervalS * _config.Sim.TickRateHz;

    /// <summary>
    /// Every body sent where its card names, on the first tick and all at once (CTL-1b). <b>An order and
    /// not a route</b>: the place is a point on the pavement, and what the walking network makes of the
    /// ground between is the whole of what the card is about.
    /// </summary>
    void Order()
    {
        for (var card = 0; card < FootwayCards.Count; card++)
        {
            for (var walker = 0; walker < FootwayCards.All[card].Walkers.Length; walker++)
            {
                var body = _lattice.WalkerOf(card, walker);
                _walked[body] = new Walked(card, walker);
                _world.Order(body, _lattice.AimM(card, walker));
            }
        }
    }
}

/// <summary>What one staged body did, read off it every tick and never off what its walker intended.</summary>
internal sealed class Walked
{
    public Walked(int card, int walker)
    {
        Card = card;
        Walker = walker;
    }

    /// <summary>The card this body was staged by, and which of that card's walkers it is.</summary>
    public int Card { get; }

    public int Walker { get; }

    /// <summary>The tick it stood at the place it was ordered to, which is the order carried out.</summary>
    public int ArrivedAt { get; private set; } = -1;

    /// <summary>
    /// How many legs of this walk ran out short of where it was going. <b>A leg ending is not the walk
    /// ending</b>: a line is laid again from wherever the body has got to (PER-8), so a long walk crosses
    /// the lattice in several of them and only a body that never arrives has given up.
    /// </summary>
    public int LegsRelaid { get; private set; }

    /// <summary>The first tick one of them ran out, and what the body was doing when the last one did.</summary>
    public int StoodAt { get; private set; } = -1;

    public string StoodDoing { get; private set; } = "—";

    /// <summary>
    /// How many ticks it was held on the way: by the pavement's own claims (PER-13) or at a kerb
    /// (PER-15). <b>The town's own statement of it</b> and not a speed read off the body — a walker has no
    /// acceleration profile (PER-3), so what a stopped body means is a question only the claims answer.
    /// </summary>
    public int HeldFor { get; private set; }

    /// <summary>And how many of those were at a kerb, which is the wait a signal or a car is behind.</summary>
    public int HeldAtAKerbFor { get; private set; }

    /// <summary>How many ticks it spent standing on a crossing.</summary>
    public int OnThePaintFor { get; private set; }

    /// <summary>
    /// And how many on a carriageway it was <em>not</em> painted over, which is the one thing no walk may
    /// ever do (PER-7.2).
    /// </summary>
    public int OffThePaintFor { get; private set; }

    /// <summary>How many crossings it stepped onto over the whole walk — a reading, since a route may want several.</summary>
    public int Crossings { get; private set; }

    /// <summary>Whether it ever stepped onto paint whose own signal was showing red (PER-7.3).</summary>
    public bool SteppedOutOnRed { get; private set; }

    /// <summary>How many ticks it spent stepping round a body in its way (PER-24).</summary>
    public int SteppedRoundFor { get; private set; }

    /// <summary>How far it walked, which is what says whether it ever moved off at all.</summary>
    public float WalkedM { get; private set; }

    /// <summary>Where it stood when the card stopped watching it.</summary>
    public Vector2 StoodAtM { get; private set; }

    bool _onThePaint;
    bool _walking;

    public override string ToString() =>
        $"arrived at {ArrivedAt}, {WalkedM:F0} m walked, {LegsRelaid} legs run out (first at {StoodAt}, "
        + $"last standing {StoodDoing}), {HeldFor} ticks held ({HeldAtAKerbFor} at a kerb), "
        + $"{OnThePaintFor} on the paint over {Crossings} crossings, {OffThePaintFor} on a carriageway off "
        + $"it, {SteppedRoundFor} stepping round, last seen {StoodAtM.X:F0},{StoodAtM.Y:F0}";

    public void Read(int tick, TownWorld world, FootwayLattice lattice)
    {
        // <b>The card is over the moment the order is.</b> What a body does after it has arrived is the
        // next thing anybody asks of it and not this card.
        if (ArrivedAt >= 0) return;

        var people = world.People;
        var body = lattice.WalkerOf(Card, Walker);
        var atM = people.PositionM[body];
        StoodAtM = atM;
        WalkedM = people.DistanceWalkedM[body];

        if ((atM - lattice.AimM(Card, Walker)).Length() <= people.RadiusM[body])
        {
            ArrivedAt = tick;
            return;
        }

        // What the ground under the body is, which is the whole of PER-7.2 read off the body: a crossing is
        // drivable ground somebody may walk on, and any other drivable ground is a carriageway.
        var ground = world.Terrain.At(atM);
        var onThePaint = ground.Drivable && ground.Walkable;
        if (ground.Drivable && !ground.Walkable) OffThePaintFor++;

        if (onThePaint)
        {
            OnThePaintFor++;
            if (!_onThePaint)
            {
                Crossings++;
                SteppedOutOnRed |= IsRed(world, people, body);
            }
        }

        _onThePaint = onThePaint;

        if (people.HeldAtTheKerb[body])
        {
            HeldFor++;
            HeldAtAKerbFor++;
        }
        else if (people.IsHeldByTheClaims(body, world.StopsInM(body)))
        {
            HeldFor++;
        }

        if (people.StepsRound[body] != PersonFleet.NoBody) SteppedRoundFor++;

        // A leg that ran out short of where the body was going. <b>Counted and never read as the end of
        // the walk</b>: the line is laid again from where the body has got to, so a walker standing here
        // for one tick is a long walk being taken in stages and one standing here for good is the walk
        // given up — which is the same state, and only arriving tells them apart.
        var walking = people.Walking[body];
        if (_walking && !walking)
        {
            LegsRelaid++;
            if (StoodAt < 0) StoodAt = tick;

            StoodDoing = WalkingWords.WalkName(people, body, world.StopsInM(body));
        }

        _walking = walking;
    }

    /// <summary>
    /// Whether the crossing this body is walking is showing it a red. <b>The signal the walker itself was
    /// asked about</b> (<see cref="Kerb.MayBegin"/>), so what is asserted cannot disagree with what the
    /// body was told; a crossing under no signal shows nothing and refuses nothing (TLT-2a).
    /// </summary>
    static bool IsRed(TownWorld world, PersonFleet people, int body)
    {
        var at = people.WalkedAt(body);
        if (at < 0) return false;

        var crossing = people.WalkedCrossingOf(body)[at];
        return crossing >= 0 && world.Signals.CrossingIsLit(crossing)
               && world.Signals.ForCrossing(crossing, world.ElapsedS) != SignalColour.Green;
    }
}
