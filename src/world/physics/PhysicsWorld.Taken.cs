using System.Numerics;

namespace TrafficSimulation.World.Physics;

/// <summary>
/// <b>A step taken elsewhere</b> — on a device that steps every body in the world — handed back so that everything the
/// table answers reads as after a step of its own: the moved bodies' poses and motion, which bodies froze and which a
/// step woke, how deep each is into what it touches, the census, and which pairs began touching (SOL-6). And, for that
/// device, which rows the town changed between two steps.
/// </summary>
/// <remarks>
/// <para>
/// <b>The device freezes nothing and the table goes on freezing</b> (SOL-37). A body frozen here is handed to the
/// device at exactly no motion, which the device's step leaves exactly where it stood, so the table can tell its
/// owners from what it hands back: the awake roster as the step began, every frozen body the device moved — an impulse
/// reached it — and every frozen body touching one of those, and so on along what touches what (SOL-37a). That is the
/// roster the table's own step would have owned, and its rest, its freezing and its held touches are worked over it as
/// they are after a step of its own.
/// </para>
/// <para>
/// <b>The device asks every pair</b>, frozen ones among them; a contact neither of whose bodies the table would have
/// owned is dropped, so a touch held between frozen bodies is held here as before (SOL-37c).
/// </para>
/// <para>
/// The order is <see cref="BeginTheStepTaken"/>, then <see cref="Moved(BodyId, Vector2, Vector2, float, Vector2, float)"/>
/// for each body the device moved and <see cref="Touched"/> for each contact in the device's order, then
/// <see cref="EndTheStepTaken"/>.
/// </para>
/// </remarks>
internal sealed partial class PhysicsWorld
{
    bool _keepsChanges;

    /// <summary>Whether the last step was taken elsewhere, so the indexes are laid when first asked rather than by a step.</summary>
    bool _indexedWhenAsked;

    bool[] _changed = new bool[Room];
    int[] _changedList = new int[Room];
    int _changedCount;

    /// <summary>
    /// How many steps this table has been through, its own and those taken elsewhere — so whoever steps it elsewhere
    /// can tell that it has stepped itself since.
    /// </summary>
    public int StepsTaken => _stepStamp;

    /// <summary>
    /// <b>Every row the town changes from here on is remembered</b> until <see cref="ForgetTheChanges"/>: a body added,
    /// taken out of the world or put back, put on another layer or channel, reweighed, pushed, or frozen — so a device
    /// keeping the table is sent those rows and no others.
    /// </summary>
    public void KeepTheChanges() => _keepsChanges = true;

    /// <summary>The rows changed since <see cref="ForgetTheChanges"/>, each once, in the order they were first changed.</summary>
    public ReadOnlySpan<int> Changes => _changedList.AsSpan(0, _changedCount);

    public void ForgetTheChanges()
    {
        foreach (var body in Changes) _changed[body] = false;
        _changedCount = 0;
    }

    /// <summary>What a body is, as it was tagged (<see cref="Tag"/>).</summary>
    public BodyTag TagOf(BodyId body) => BodyTag.Unpack(_tag[body.Index]);

    void Changed(int index)
    {
        if (!_keepsChanges || _changed[index]) return;

        _changed[index] = true;
        _changedList[_changedCount++] = index;
    }

    /// <summary>
    /// <b>A step taken elsewhere begun</b>, at the poses it began from: the awake roster taken in, and every awake body
    /// an owner of the step with its overlap cleared.
    /// </summary>
    /// <remarks>
    /// <b>The moving and frozen indexes are not laid here</b>: the step was priced on the device's own grids, and nothing
    /// of it reads these. They are laid when a picture or a ray first asks (<see cref="InBox"/>, <see cref="CastRay"/>).
    /// </remarks>
    public void BeginTheStepTaken()
    {
        if (_staticIndexStale) SettleStatics();

        TakeInTheJoining();
        BeginTheContacts();
        for (var slot = 0; slot < _movingCount; slot++) _ownedInStep[_moving[slot]] = _stepStamp;
        _indexedWhenAsked = true;
    }

    /// <summary>
    /// <b>Where the step left a body</b>, and how it left it moving. A frozen body left exactly where it stood and at
    /// no motion was frozen through the step and keeps its own pose; one the step moved was reached by something, and
    /// is woken into it (SOL-37a). A body out of the world is not the step's.
    /// </summary>
    public void Moved(BodyId body, Vector2 positionM, Vector2 rotation, float headingRad, Vector2 velocityMps, float yawRateRadPerS)
    {
        var index = body.Index;
        if ((_flags[index] & BodyFlags.Enabled) == 0) return;

        var moved = positionM != _positionM[index] || headingRad != _headingRad[index];
        if (_ownedInStep[index] != _stepStamp)
        {
            if (!moved && velocityMps == Vector2.Zero && yawRateRadPerS == 0f) return;

            WakeInStep(index);
            _ownedInStep[index] = _stepStamp;
        }

        if (moved) _flags[index] &= ~BodyFlags.FiledFrozen;

        _positionM[index] = positionM;
        _rotation[index] = rotation;
        _headingRad[index] = headingRad;
        _velocityMps[index] = velocityMps;
        _yawRateRadPerS[index] = yawRateRadPerS;
        Bound(index);
    }

    /// <summary>
    /// The same of a body that never turns — its rotation locked, as a person's is — which keeps its heading and its
    /// rotation.
    /// </summary>
    public void Moved(BodyId body, Vector2 positionM, Vector2 velocityMps) =>
        Moved(body, positionM, _rotation[body.Index], _headingRad[body.Index], velocityMps, 0f);

    /// <summary>One contact the step found, handed over in the step's own order: by the lower body, then the higher, the furniture second.</summary>
    public void Touched(int first, int second, in Manifold manifold)
    {
        RoomForContact();
        WriteContact(_contactCount++, first, second, manifold);
    }

    /// <summary>
    /// <b>The step taken elsewhere ended</b>: every frozen body touching one the step owned woken into it, and so on
    /// along what touches what (SOL-37a); the contacts kept that the table's own step would have found; the census;
    /// whatever has settled frozen (SOL-37, SOL-37b); and which pairs began touching (SOL-6).
    /// </summary>
    public void EndTheStepTaken()
    {
        WakeWhatTheStepTouched();
        KeepTheOwnedContacts();
        OrderContacts();
        IntegratedBodyCount = _movingCount;
        FreezeWhatHasSettled();
        _movingIndexStale = true;
        ReportBegun();
    }

    /// <summary>Every frozen body in a contact with a body the step owned, woken into it, until no contact wakes another.</summary>
    void WakeWhatTheStepTouched()
    {
        bool woke;
        do
        {
            woke = false;
            for (var contact = 0; contact < _contactCount; contact++)
            {
                var first = _contactA[contact];
                var second = _contactB[contact];
                var firstOwned = _ownedInStep[first] == _stepStamp;
                if (firstOwned == (_ownedInStep[second] == _stepStamp)) continue;

                var sleeper = firstOwned ? second : first;
                if ((_flags[sleeper] & (BodyFlags.Static | BodyFlags.Frozen | BodyFlags.Enabled)) != (BodyFlags.Frozen | BodyFlags.Enabled))
                {
                    continue;
                }

                WakeInStep(sleeper);
                _ownedInStep[sleeper] = _stepStamp;
                woke = true;
            }
        }
        while (woke);
    }

    /// <summary>The contacts with a body the step owned, kept in their order and taken in as the table's own step takes them; the rest dropped.</summary>
    void KeepTheOwnedContacts()
    {
        var found = _contactCount;
        _contactCount = 0;
        for (var contact = 0; contact < found; contact++)
        {
            if (_ownedInStep[_contactA[contact]] != _stepStamp && _ownedInStep[_contactB[contact]] != _stepStamp) continue;

            var into = _contactCount++;
            if (into != contact) CopyContact(contact, into);

            Touching(into);
        }
    }
}
