using System.Numerics;

namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>A body read by place</b> (TER-4c.5): how one met on a way moves, off the world's last step, and whether the asker's
/// own line is held just past it — what decides whether it is something to get past (CAR-46, CAR-53, PER-28) and how far
/// short of it to stand (S-2a). <b>Never carried on a claim, and never who</b>: the body is found again where it was met,
/// and only how it moves and what lies past it is handed back.
/// </summary>
internal sealed partial class LaneOccupancy
{
    /// <summary>Every car's velocity as the world's last step left it, by its index — the fleet's own row, handed over.</summary>
    Vector2[] _drivingVelocityMps = [];

    /// <summary>And every walker's.</summary>
    Vector2[] _walkingVelocityMps = [];

    /// <summary>Every car's heading, by its index — the fleet's own row, handed over.</summary>
    float[] _drivingHeadingRad = [];

    /// <summary>And every walker's.</summary>
    float[] _walkingHeadingRad = [];

    /// <summary>
    /// <b>The rows the bodies' motion is read from</b>: each fleet's velocity and heading as the world's last step left
    /// them, read into the fleet every tick — handed over as data once, so the road never learns what a fleet is.
    /// </summary>
    public void SeeTheBodiesMoveIn(
        Vector2[] drivingVelocityMps, float[] drivingHeadingRad, Vector2[] walkingVelocityMps, float[] walkingHeadingRad)
    {
        (_drivingVelocityMps, _drivingHeadingRad) = (drivingVelocityMps, drivingHeadingRad);
        (_walkingVelocityMps, _walkingHeadingRad) = (walkingVelocityMps, walkingHeadingRad);
    }

    /// <summary>
    /// <b>How a body met on a way moves and which way it points</b>, as the world's last step left it, and whether it is
    /// wheeled traffic — false where it is no longer where it was met, or is laid as another holder's ground (a trailer,
    /// an owner walking to its car), whose own motion is not this one's: a body not read is never one to get past.
    /// </summary>
    /// <param name="occupant">The asker, whose own body is never the one met.</param>
    public bool ReadTheBody(
        in BodySeen body, int occupant, LaneRoster of, out Vector2 velocityMps, out float headingRad, out bool wheeled)
    {
        (velocityMps, headingRad, wheeled) = (default, 0f, false);
        var slot = BodySlotAt(body, KeyOf(occupant, of));
        if (slot == NoSlot || !MotionOf(slot, out velocityMps, out headingRad)) return false;

        wheeled = RosterOf(_slots[slot].Key) == LaneRoster.Driving;
        return true;
    }

    /// <summary>
    /// The motion of the body one body stretch is of — false where it is laid as another holder's ground, whose motion
    /// is not its own.
    /// </summary>
    bool MotionOf(int slot, out Vector2 velocityMps, out float headingRad)
    {
        (velocityMps, headingRad) = (default, 0f);
        var key = _slots[slot].Key;
        if (key >= _bodyHoldOf.Length || _bodyHoldOf[key] != _owners[slot].Hold) return false;

        var index = OccupantOf(key);
        var (velocities, headings) = RosterOf(key) switch
        {
            LaneRoster.Driving => (_drivingVelocityMps, _drivingHeadingRad),
            LaneRoster.Walking => (_walkingVelocityMps, _walkingHeadingRad),
            _ => (Array.Empty<Vector2>(), Array.Empty<float>()),
        };
        if ((uint)index >= (uint)velocities.Length || (uint)index >= (uint)headings.Length) return false;

        (velocityMps, headingRad) = (velocities[index], headings[index]);
        return true;
    }

    /// <summary>
    /// <b>Whether the asker's own line is held just past a body met on it</b>: anything laid within
    /// <paramref name="reachM"/> past the body's far edge, down <paramref name="line"/> — a plan or a secondary claim, the
    /// body's own plan among them, or another body that is moving — but the asker's own ground. <b>A body at rest that is
    /// held is waiting on what the asker would wait on</b>, a queue's; one that is not is going nowhere the asker's way.
    /// <b>Another body at rest that near is read the same way in its turn</b>, so a row of wrecks nose to tail is held by
    /// what holds the front one.
    /// </summary>
    /// <param name="line">The asker's ways from the body on, at least <paramref name="reachM"/> past it, nearest first.</param>
    /// <param name="atRestMps">The speed at or under which a body is at rest.</param>
    /// <remarks>A body not on <paramref name="line"/>, or not read, stands nowhere the line runs on past, and is held.</remarks>
    public bool IsHeldPast(
        in BodySeen body, ReadOnlySpan<LineWay> line, float reachM, float atRestMps, int occupant, LaneRoster of)
    {
        var asker = KeyOf(occupant, of);
        var slot = BodySlotAt(body, asker);
        if (slot == NoSlot || !LineWays.OnTheLine(line, body.Way, body.ToM, out var pastM)) return true;

        var bodyKey = _slots[slot].Key;
        for (var hop = 0; hop < MostBodiesReadInARow; hop++)
        {
            var untilM = pastM + reachM;
            var next = NoSlot;
            var nextOn = 0;
            foreach (ref readonly var way in line)
            {
                var fromM = LineWays.OnTheWayM(way, pastM);
                var toM = LineWays.OnTheWayM(way, untilM);
                if (toM <= fromM) continue;
                if (IsPlannedOver(way.Way, DmOf(fromM), DmOf(toM), asker)) return true;
                if (next == NoSlot && FirstOtherBody(way.Way, DmOf(fromM), DmOf(toM), asker, bodyKey) is var found and not NoSlot)
                {
                    (next, nextOn) = (found, way.Way);
                }
            }

            if (next == NoSlot) return false;
            if (!MotionOf(next, out var velocityMps, out _) || velocityMps.Length() > atRestMps
                || !LineWays.OnTheLine(line, nextOn, MetresOf(_slots[next].ToDm), out var nextPastM))
            {
                return true;
            }

            (pastM, bodyKey) = (MathF.Max(pastM, nextPastM), _slots[next].Key);
        }

        return true;
    }

    /// <summary>How many bodies at rest nose to tail one reading follows before it reads the row as held. A bound on a loop.</summary>
    const int MostBodiesReadInARow = 24;

    /// <summary>
    /// Whether a plan of anybody's but the asker is laid over <c>[fromDm, toDm)</c> of one way: a main claim, or a
    /// secondary claim one but the asker holds.
    /// </summary>
    bool IsPlannedOver(int way, ushort fromDm, ushort toDm, int asker)
    {
        var row = RowOf(way);
        if (row == NoRow || toDm <= fromDm) return false;

        for (var at = _rows[row].Mains; at != NoSlot; at = _slots[at].Next)
        {
            ref readonly var main = ref _slots[at];
            if (main.FromDm >= toDm) break;
            if (main.ToDm > fromDm && main.Key != asker) return true;
        }

        return HeldByOthers(_rows[row].Seconds, fromDm, toDm, asker, asker, ClaimPriority.Backing);
    }

    /// <summary>The first body stretch over <c>[fromDm, toDm)</c> of one way but the asker's and one body's, or <see cref="NoSlot"/>.</summary>
    int FirstOtherBody(int way, ushort fromDm, ushort toDm, int asker, int bodyKey)
    {
        for (var at = BodiesOn(RowOf(way)); at != NoSlot; at = _slots[at].Next)
        {
            ref readonly var other = ref _slots[at];
            if (other.FromDm >= toDm) break;
            if (other.ToDm > fromDm && other.Key != asker && other.Key != bodyKey) return at;
        }

        return NoSlot;
    }

    /// <summary>The body stretch met as <paramref name="body"/>, exactly where it was met and not the asker's, or <see cref="NoSlot"/>.</summary>
    int BodySlotAt(in BodySeen body, int asker)
    {
        var fromDm = DmOf(body.FromM);
        var toDm = DmOf(body.ToM);
        for (var at = BodiesOn(RowOf(body.Way)); at != NoSlot; at = _slots[at].Next)
        {
            ref readonly var stretch = ref _slots[at];
            if (stretch.FromDm > fromDm) break;
            if (stretch.FromDm == fromDm && stretch.ToDm == toDm && stretch.Key != asker) return at;
        }

        return NoSlot;
    }
}
