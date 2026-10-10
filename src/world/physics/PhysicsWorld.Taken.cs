using System.Numerics;

namespace TrafficSimulation.World.Physics;

/// <summary>
/// <b>A step taken elsewhere</b> — on a device that steps every body in the world — handed back so that everything the
/// table answers reads as after a step of its own: the moved bodies' poses and motion, which bodies froze and which a
/// step woke, how deep each is into what it touches, the census, and which pairs began touching (SOL-6). And, for that
/// device, which rows the town changed between two steps and whose rest it counted afresh.
/// </summary>
/// <remarks>
/// <para>
/// <b>The device freezes, and the table takes what it froze</b> (SOL-37). A frozen body stands on the device at exactly
/// no motion, so the device tells the owners the table's own step would have had: the awake roster as the step began,
/// every frozen body the step moved — an impulse reached it — and every frozen body touching one of those, and so on
/// along what touches what (SOL-37a). It keeps the rest counts and works rest and its unsettling over those owners as
/// the table's own step does (<c>kernels/settle.comp</c>), and hands back which frozen bodies it took in and which it
/// froze. The rest counts are the device's from then on; the table's are the ones it was handed, but where the town
/// counts one afresh (<see cref="CountsRestAfresh"/>).
/// </para>
/// <para>
/// <b>The device asks every pair</b>, frozen ones among them; a contact neither of whose bodies the step owned is
/// dropped, so a touch held between frozen bodies is held here as before (SOL-37c).
/// </para>
/// <para>
/// The order is <see cref="BeginTheStepTaken"/>, then <see cref="Moved(BodyId, Vector2, Vector2, float, Vector2, float)"/>
/// for each body the device moved, <see cref="WokeInTheStep"/> for each frozen body it took in and
/// <see cref="Touched"/> for each contact in the device's order, then <see cref="EndTheStepTaken"/> with what it froze.
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
    bool[] _restAfresh = new bool[Room];

    /// <summary>
    /// How many steps this table has been through, its own and those taken elsewhere — so whoever steps it elsewhere
    /// can tell that it has stepped itself since.
    /// </summary>
    public int StepsTaken => _stepStamp;

    /// <summary>
    /// <b>Every row the town changes from here on is remembered</b> until <see cref="ForgetTheChanges"/>: a body added,
    /// taken out of the world or put back, put on another layer or channel, reweighed, pushed, or frozen by the table's
    /// own step — so a device keeping the table is sent those rows and no others.
    /// </summary>
    public void KeepTheChanges() => _keepsChanges = true;

    /// <summary>The rows changed since <see cref="ForgetTheChanges"/>, each once, in the order they were first changed.</summary>
    public ReadOnlySpan<int> Changes => _changedList.AsSpan(0, _changedCount);

    /// <summary>Whether the town changed this body's row since <see cref="ForgetTheChanges"/>.</summary>
    public bool HasChanged(BodyId body) => _changed[body.Index];

    /// <summary>
    /// Whether the town has counted this body's rest afresh since <see cref="ForgetTheChanges"/> — put it in a container,
    /// or added it — so a device that keeps the rest counts does too. Such a body is one of the <see cref="Changes"/>.
    /// </summary>
    public bool CountsRestAfresh(int body) => _restAfresh[body];

    public void ForgetTheChanges()
    {
        foreach (var body in Changes) _changed[body] = _restAfresh[body] = false;
        _changedCount = 0;
    }

    void RestAfresh(int index)
    {
        _restTicks[index] = 0;
        if (!_keepsChanges) return;

        _restAfresh[index] = true;
        Changed(index);
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
    /// <b>Where the step left a body</b>, and how it left it moving — at exactly no motion where it froze. A body out of
    /// the world is not the step's.
    /// </summary>
    public void Moved(BodyId body, Vector2 positionM, Vector2 rotation, float headingRad, Vector2 velocityMps, float yawRateRadPerS)
    {
        var index = body.Index;
        if ((_flags[index] & BodyFlags.Enabled) == 0) return;

        if (positionM != _positionM[index] || headingRad != _headingRad[index]) _flags[index] &= ~BodyFlags.FiledFrozen;

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
    /// <b>A frozen body the step took in</b> (SOL-37a): one it moved or left moving, or one touching a body it owned,
    /// and so on along what touches what — woken into it, and owned by it. A body the town has woken or taken out of
    /// the world since the step was laid is the town's.
    /// </summary>
    public void WokeInTheStep(int body)
    {
        if ((_flags[body] & (BodyFlags.Static | BodyFlags.Frozen | BodyFlags.Enabled)) != (BodyFlags.Frozen | BodyFlags.Enabled)) return;

        WakeInStep(body);
        _ownedInStep[body] = _stepStamp;
    }

    /// <summary>
    /// <b>The step taken elsewhere ended</b>: the contacts kept that the table's own step would have found; the census;
    /// what the step froze (SOL-37, SOL-37b); and which pairs began touching (SOL-6).
    /// </summary>
    /// <param name="froze">The bodies the step froze, each an owner of it and stilled there.</param>
    public void EndTheStepTaken(ReadOnlySpan<int> froze)
    {
        KeepTheOwnedContacts();
        OrderContacts();
        IntegratedBodyCount = _movingCount;
        FreezeWhatTheStepFroze(froze);
        _movingIndexStale = true;
        ReportBegun();
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
