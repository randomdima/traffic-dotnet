using System.Numerics;

namespace TrafficSimulation.World.Physics;

/// <summary>
/// <b>The links</b> (SOL-38): a solid bar between a point on each of two bodies, free to turn at both ends — a row of the
/// velocity passes beside the contacts and of the push passes beside the overlaps.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bilateral along the bar and nothing across it.</b> A contact may only push; a link pulls as hard as it pushes, so
/// its impulse is never clamped and nothing is accumulated. Across the bar it spends nothing, which is what leaves either
/// end free to turn. A pin — a rest length of nought — has no "along", and is held on both axes at once by the 2×2 the
/// two points' masses and inertias make.
/// </para>
/// <para>
/// <b>Solved first in every pass, one after another in slot order</b>, then the contacts — on the device by one thread
/// before the colours (<c>kernels/solve.comp</c>), so a chain is run in the same order on either. A chain of a dozen
/// converges inside the passes: each pass carries a pull from its head to its tail.
/// </para>
/// <para>
/// <b>A linked body never comes to rest</b>: its rest count starts afresh every step, so the two never freeze apart
/// (SOL-37b) and neither is ever frozen under the other's pull. A link to a body out of the world holds nothing.
/// </para>
/// </remarks>
internal sealed partial class PhysicsWorld
{
    LinkSlots _links = new(0);

    // What each live link is solved against this step, by its place in the live list: laid by PrepareLinks.
    Vector2[] _linkArmA = [], _linkArmB = [], _linkAxis = [], _linkStretchM = [];

    /// <summary>A pin's inverse 2×2 (xx, xy, yy), or a bar's mass along its axis in x.</summary>
    Vector3[] _linkMass = [];

    /// <summary>Whether each live link holds this step: both its bodies in the world.</summary>
    bool[] _linkHolds = [];

    /// <summary>Room for <paramref name="slots"/> links (<see cref="LinkSlots"/>), each empty. Called once, before any link is made.</summary>
    public void FitLinks(int slots)
    {
        _links = new LinkSlots(slots);
        _linkArmA = new Vector2[slots];
        _linkArmB = new Vector2[slots];
        _linkAxis = new Vector2[slots];
        _linkStretchM = new Vector2[slots];
        _linkMass = new Vector3[slots];
        _linkHolds = new bool[slots];
    }

    /// <summary>
    /// <b>Two bodies linked in a slot</b>, replacing whatever it held: a point on each, in that body's own frame, held
    /// <paramref name="restM"/> apart. Both are woken.
    /// </summary>
    public void Link(int slot, BodyId first, Vector2 anchorFirstM, BodyId second, Vector2 anchorSecondM, float restM)
    {
        _links.Link(slot, new LinkRow(first.Index, second.Index, anchorFirstM, anchorSecondM, restM));
        Wake(first.Index);
        Wake(second.Index);
    }

    /// <summary>A slot emptied; nothing woken.</summary>
    public void Unlink(int slot) => _links.Unlink(slot);

    /// <summary>Whether a slot holds a link.</summary>
    public bool IsLinked(int slot) => _links.IsLinked(slot);

    /// <summary>Every link held, in the order a step solves them.</summary>
    public ReadOnlySpan<LinkRow> Links => _links.Live;

    /// <summary>Every linked body's rest counted afresh (SOL-38): a linked pair never comes to rest, and so never freezes apart.</summary>
    void RestTheLinkedAfresh()
    {
        foreach (ref readonly var link in _links.Live)
        {
            RestAfresh(link.First);
            RestAfresh(link.Second);
        }
    }

    /// <summary>
    /// The arms, the axis, the masses and the stretch each link is solved against, off the poses the step begins at — as
    /// the contacts' are (<see cref="Prepare"/>).
    /// </summary>
    void PrepareLinks()
    {
        var allowedM = _config.Solver.AllowedPenetrationM;
        var links = _links.Live;
        for (var at = 0; at < links.Length; at++)
        {
            ref readonly var link = ref links[at];
            var first = link.First;
            var second = link.Second;
            _linkHolds[at] = (_flags[first] & _flags[second] & BodyFlags.Enabled) != 0;
            if (!_linkHolds[at]) continue;

            var armA = Shape.Rotate(_rotation[first], link.AnchorFirstM);
            var armB = Shape.Rotate(_rotation[second], link.AnchorSecondM);
            _linkArmA[at] = armA;
            _linkArmB[at] = armB;
            var apartM = _positionM[second] + armB - (_positionM[first] + armA);
            var lengthM = MathF.Sqrt(Vector2.Dot(apartM, apartM));

            if (link.RestM > 0f)
            {
                var axis = lengthM > 0f ? apartM / lengthM : _rotation[first];
                var stretchM = lengthM - link.RestM;
                var beyondM = MathF.Max(MathF.Abs(stretchM) - allowedM, 0f);
                _linkAxis[at] = axis;
                _linkStretchM[at] = new Vector2(stretchM < 0f ? -beyondM : beyondM, 0f);
                _linkMass[at] = new Vector3(EffectiveMass(first, second, armA, armB, axis), 0f, 0f);
                continue;
            }

            var pinBeyondM = MathF.Max(lengthM - allowedM, 0f);
            _linkStretchM[at] = lengthM > 0f ? apartM * (pinBeyondM / lengthM) : Vector2.Zero;
            _linkMass[at] = PinMass(first, second, armA, armB);
        }
    }

    /// <summary>
    /// <b>A pin's inverse mass matrix</b>: what an impulse at the two points does to their closing on each world axis,
    /// inverted — the contact's effective mass, said of both axes at once (xx, xy, yy).
    /// </summary>
    Vector3 PinMass(int first, int second, Vector2 armA, Vector2 armB)
    {
        var masses = _inverseMass[first] + _inverseMass[second];
        var turnA = _inverseInertia[first];
        var turnB = _inverseInertia[second];
        var xx = masses + turnA * armA.Y * armA.Y + turnB * armB.Y * armB.Y;
        var xy = -(turnA * armA.X * armA.Y) - turnB * armB.X * armB.Y;
        var yy = masses + turnA * armA.X * armA.X + turnB * armB.X * armB.X;
        var determinant = xx * yy - xy * xy;
        if (determinant <= 0f) return Vector3.Zero;

        var inverse = 1f / determinant;
        return new Vector3(yy * inverse, -xy * inverse, xx * inverse);
    }

    /// <summary>Each link's closing along it taken out, in slot order (<see cref="SolveVelocities"/>).</summary>
    void SolveLinkVelocities()
    {
        var links = _links.Live;
        for (var at = 0; at < links.Length; at++)
        {
            if (!_linkHolds[at]) continue;

            ref readonly var link = ref links[at];
            var armA = _linkArmA[at];
            var armB = _linkArmB[at];
            var closing = Closing(link.First, link.Second, armA, armB);
            Push(link.First, link.Second, armA, armB, LinkImpulse(link.RestM, at, -closing));
        }
    }

    /// <summary>Each link's stretch past the allowance pushed out on the push accumulator, as an overlap is (<see cref="SolvePush"/>).</summary>
    void SolveLinkPush(float bias, float mostMps)
    {
        var links = _links.Live;
        for (var at = 0; at < links.Length; at++)
        {
            if (!_linkHolds[at] || _linkStretchM[at] == Vector2.Zero) continue;

            ref readonly var link = ref links[at];
            var first = link.First;
            var second = link.Second;
            var armA = _linkArmA[at];
            var armB = _linkArmB[at];
            var wantedMps = LinkPushMps(link.RestM, _linkStretchM[at], _linkAxis[at], bias, mostMps);
            var impulse = LinkImpulse(link.RestM, at, wantedMps - Separating(first, second, armA, armB));
            _pushMps[first] -= impulse * _inverseMass[first];
            _pushYawRadPerS[first] -= _inverseInertia[first] * Shape.Cross(armA, impulse);
            _pushMps[second] += impulse * _inverseMass[second];
            _pushYawRadPerS[second] += _inverseInertia[second] * Shape.Cross(armB, impulse);
        }
    }

    /// <summary>
    /// How fast the push wants the two points to close: the stretch past the allowance over the contacts' bias, no faster
    /// than they push an overlap out — along the bar for a bar, toward each other for a pin.
    /// </summary>
    static Vector2 LinkPushMps(float restM, Vector2 stretchM, Vector2 axis, float bias, float mostMps)
    {
        if (restM > 0f) return axis * Math.Clamp(-stretchM.X * bias, -mostMps, mostMps);

        var wantedMps = -stretchM * bias;
        var speedMps = MathF.Sqrt(Vector2.Dot(wantedMps, wantedMps));
        return speedMps > mostMps ? wantedMps * (mostMps / speedMps) : wantedMps;
    }

    /// <summary>
    /// The impulse that changes the two points' closing by <paramref name="changeMps"/> — along the bar alone for a bar,
    /// on both axes for a pin.
    /// </summary>
    Vector2 LinkImpulse(float restM, int at, Vector2 changeMps)
    {
        var mass = _linkMass[at];
        if (restM > 0f)
        {
            var axis = _linkAxis[at];
            return axis * (mass.X * Vector2.Dot(changeMps, axis));
        }

        return new Vector2(mass.X * changeMps.X + mass.Y * changeMps.Y, mass.Y * changeMps.X + mass.Z * changeMps.Y);
    }
}
