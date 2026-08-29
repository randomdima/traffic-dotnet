using System.Numerics;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>The crossings map, walked</b>: five streets with a crossing on each — one of them deliberately laid
/// off square to its street — and one body apiece paced kerb to kerb over its own paint.
/// </summary>
/// <remarks>
/// <para>
/// <b>The map is nothing but crossings</b>, which is what it is for: a skewed one is the case that can
/// fail while every square crossing passes, and a whole city cannot be asked about it because a question
/// asked of whatever a city happens to contain is a different question every time somebody edits it.
/// </para>
/// <para>
/// <b>The walking is staged and the answer is not</b>, on the driving exam's terms
/// (<see cref="ExamWatch"/>): a body left to itself draws a destination anywhere on the pavement and may
/// never take a crossing at all, so each is sent over its own paint and turned round when it arrives.
/// What is claimed is what the walker did with that order and never that it was given one.
/// </para>
/// </remarks>
internal sealed class ZebraWatch : ScenarioWatch
{
    /// <summary>The map this is about. It is hand-authored rather than laid by this build, so its name is all there is to go on.</summary>
    public const string Map = "Zebras";

    const int EveryCrossingIsWalked = 0;
    const int NobodyIsOffThePaint = 1;

    const int CrossingsWalked = 0;
    const int TimeOnThePaint = 1;

    static readonly string[] TheClaims =
    [
        "every crossing is walked kerb to kerb, none given up on",
        "nobody on foot is on a carriageway off the paint",
    ];

    static readonly string[] TheReadings =
    [
        "what has been walked",
        "time spent on the paint",
    ];

    /// <summary>One crossing of the map: the two kerbs of it, who is pacing it and how often they have been over.</summary>
    struct Paced
    {
        public Vector2 NearKerbM;
        public Vector2 FarKerbM;
        public int Walker;
        public bool GoingOver;

        /// <summary>Whether the order to the kerb it is heading for has been given yet.</summary>
        public bool Sent;

        public int Crossed;
    }

    readonly Paced[] _paced;

    /// <summary>
    /// How many crossings the map carries at all. <b>The claim is read against this and not against the
    /// number staged</b>: a crossing with nobody to walk it is a crossing nothing says anything about, and
    /// counting only what was staged would report that as a full set.
    /// </summary>
    readonly int _crossings;

    readonly float _arrivesWithinM;

    long _gaveUpBefore = -1;
    long _gaveUpSince;
    long _walkerTicksOffThePaint;
    long _walkerTicksOnThePaint;
    int _lastOffThePaint = -1;

    public ZebraWatch(SimConfig config, TownWorld world)
        : base("the crossings", "five streets, a crossing on each, one body paced over each", TheClaims, TheReadings)
    {
        _arrivesWithinM = config.PersonDiameterM;
        _paced = Stage(world);
        _crossings = world.Plan.Crosswalks.Count;
    }

    /// <summary>
    /// One walker sent over each crossing: the nearest body to its near kerb, which on a map of isolated
    /// streets is the one standing on that street.
    /// </summary>
    /// <remarks>
    /// <b>The kerbs are the crossing edge's own two ends</b> and not a rectangle measured off the plan: a
    /// crossing is a link of the walking network, so the ground a body is sent to is the ground the network
    /// says the paint arrives at — including where the paint is skewed, which is the whole point of the map.
    /// </remarks>
    static Paced[] Stage(TownWorld world)
    {
        var foot = world.Foot;
        var taken = new bool[world.People.Count];
        var paced = new List<Paced>();

        for (var edge = 0; edge < foot.EdgeCount; edge++)
        {
            // Both directions of one crossing are laid together, so one of the pair is the crossing.
            if (foot.KindOf(edge) != FootEdgeKind.Crossing || foot.Reverse(edge) < edge) continue;

            var nearM = foot.AnchorM(foot.FromNode(edge));
            var farM = foot.AnchorM(foot.ToNode(edge));
            var walker = Nearest(world, nearM, taken);
            if (walker < 0) continue;

            taken[walker] = true;
            paced.Add(new Paced { NearKerbM = nearM, FarKerbM = farM, Walker = walker, GoingOver = true });
        }

        return [.. paced];
    }

    static int Nearest(TownWorld world, Vector2 toM, bool[] taken)
    {
        var nearest = -1;
        var nearestM = float.MaxValue;
        for (var person = 0; person < world.People.Count; person++)
        {
            if (taken[person] || !world.People.Acts(person)) continue;

            var awayM = (world.People.PositionM[person] - toM).LengthSquared();
            if (awayM >= nearestM) continue;

            nearestM = awayM;
            nearest = person;
        }

        return nearest;
    }

    public override void Saw(TownWorld world)
    {
        // The town's own give-up count when the staging began, so what is claimed is the walks this watch
        // asked for and not whatever the town had already abandoned before it.
        if (_gaveUpBefore < 0) _gaveUpBefore = world.WalksGivenUp;

        _gaveUpSince = world.WalksGivenUp - _gaveUpBefore;

        for (var at = 0; at < _paced.Length; at++)
        {
            ref var paced = ref _paced[at];
            var standingM = world.People.PositionM[paced.Walker];
            var toM = paced.GoingOver ? paced.FarKerbM : paced.NearKerbM;
            if (paced.Sent && (standingM - toM).LengthSquared() > _arrivesWithinM * _arrivesWithinM) continue;

            // <b>Ordered once and turned round on arrival</b>, never every tick: an order is a walk
            // planned from where the body stands, and one given again every tick is a body that gives up
            // the ground it has claimed and re-plans the same walk sixty times a second.
            if (paced.Sent)
            {
                if (paced.GoingOver) paced.Crossed++;

                paced.GoingOver = !paced.GoingOver;
            }

            paced.Sent = true;
            world.Order(paced.Walker, paced.GoingOver ? paced.FarKerbM : paced.NearKerbM);
        }

        for (var person = 0; person < world.People.Count; person++)
        {
            // A body on the ground is not walking, and where it was left is the crash's business.
            if (!world.People.Acts(person)) continue;

            var ground = world.Terrain.At(world.People.PositionM[person]);
            if (!ground.Drivable) continue;

            if (ground.Walkable)
            {
                _walkerTicksOnThePaint++;
                continue;
            }

            _walkerTicksOffThePaint++;
            _lastOffThePaint = person;
        }
    }

    public override ClaimVerdict Verdict(int claim) => claim switch
    {
        EveryCrossingIsWalked when GivenUp() > 0 => ClaimVerdict.Broken,
        EveryCrossingIsWalked => _crossings > 0 && Walked() == _crossings
            ? ClaimVerdict.Kept
            : ClaimVerdict.Waiting,

        NobodyIsOffThePaint when _walkerTicksOffThePaint > 0 => ClaimVerdict.Broken,
        NobodyIsOffThePaint => _walkerTicksOnThePaint > 0 ? ClaimVerdict.Kept : ClaimVerdict.Waiting,

        _ => ClaimVerdict.Waiting,
    };

    public override void Says(int claim, ref TextBuffer into)
    {
        switch (claim)
        {
            case EveryCrossingIsWalked:
                into.Add(Walked());
                into.Add(" of ");
                into.Add(_crossings);
                into.Add(" crossings walked over, ");
                into.Add(GivenUp());
                into.Add(" walks given up");
                break;

            case NobodyIsOffThePaint:
                into.Add(_walkerTicksOffThePaint);
                into.Add(" walker-ticks on a carriageway off the paint");
                if (_lastOffThePaint < 0) break;

                into.Add(", last walker ");
                into.Add(_lastOffThePaint);
                break;
        }
    }

    public override void Reads(int reading, ref TextBuffer into)
    {
        switch (reading)
        {
            case CrossingsWalked:
                into.Add(Crossings());
                into.Add(" kerb-to-kerb walks by the ");
                into.Add(_paced.Length);
                into.Add(" bodies paced over them");
                break;

            case TimeOnThePaint:
                into.Add(_walkerTicksOnThePaint);
                into.Add(" walker-ticks on the paint");
                break;
        }
    }

    /// <summary>How many of the crossings have been walked at all, which is what the claim is about.</summary>
    int Walked()
    {
        var walked = 0;
        foreach (var paced in _paced)
        {
            if (paced.Crossed > 0) walked++;
        }

        return walked;
    }

    /// <summary>And how many times over, which is the reading beside it.</summary>
    int Crossings()
    {
        var crossings = 0;
        foreach (var paced in _paced) crossings += paced.Crossed;

        return crossings;
    }

    /// <summary>Walks abandoned since the staging began: on a walk of one crossing's width, giving up is the failure.</summary>
    long GivenUp() => _gaveUpSince;
}
