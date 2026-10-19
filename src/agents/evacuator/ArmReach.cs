using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.Agents.Evacuator;

/// <summary>
/// <b>What an arm swung out from its hinge would catch, and by which end</b> (EVA-5): every body put to it in turn, and
/// the nearest free end kept — numbered as <see cref="TowHitches"/> numbers bodies, or <see cref="TowHitches.Nothing"/>.
/// </summary>
/// <remarks>
/// <b>The arm swings, so what it catches is what it can reach and not only what is straight behind it</b>: an end whose
/// own half of a body comes within the arm's reach of the hinge. Behind the truck because the hinge is on the deck and
/// the cab is in the way of everything in front of it. <b>Any body, and only an end no other fork has</b>: the end
/// another arm holds is passed over, and its other end is still there to be caught.
/// </remarks>
internal struct ArmReach(Vector2 hingeM, Vector2 forward, float reachM)
{
    readonly Vector2 _hingeM = hingeM;
    readonly Vector2 _forward = forward;
    float _nearestM = reachM;

    public int Caught { get; private set; } = TowHitches.Nothing;

    public bool ByTheTail { get; private set; }

    /// <summary>
    /// One body put to the arm: for each of its ends no fork holds, the point of that end's half of its footprint nearest
    /// the hinge, kept where it is behind the truck and no further than anything found yet.
    /// </summary>
    public void Consider(int body, Vector2 positionM, Vector2 forward, Vector2 halfSizeM, bool noseHeld, bool tailHeld)
    {
        var right = Heading.RightOf(forward);
        var offset = _hingeM - positionM;
        var acrossM = Math.Clamp(Vector2.Dot(offset, right), -halfSizeM.Y, halfSizeM.Y);
        var alongM = Vector2.Dot(offset, forward);
        for (var end = 0; end < 2; end++)
        {
            var byTheTail = end == 1;
            if (byTheTail ? tailHeld : noseHeld) continue;

            var onItsHalfM = byTheTail ? Math.Clamp(alongM, -halfSizeM.X, 0f) : Math.Clamp(alongM, 0f, halfSizeM.X);
            var toItM = positionM + (forward * onItsHalfM) + (right * acrossM) - _hingeM;
            var farM = toItM.Length();
            if (farM > _nearestM || Vector2.Dot(toItM, _forward) >= 0f) continue;

            Caught = body;
            _nearestM = farM;
            ByTheTail = byTheTail;
        }
    }
}
