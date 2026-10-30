using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Multiplayer.Wire;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Multiplayer.Client;

/// <summary>
/// <b>What the player's own keys show before the server answers them</b> (MP-7): its car's road wheels and its brake and
/// reversing lamps, and the way its body on foot faces — worked out on the page by the server's own hand
/// (<see cref="TownWorld.HandCommand"/>, <see cref="WalkerFollower.TurnToward"/>), a tick at a time, from the keys held
/// now. Where anything stands, and everything else about it, stays the server's: these are drawn over the posed area.
/// </summary>
/// <remarks>
/// <para>
/// <b>Each tracks its keys to a stop</b> — a wheel wound to the key's lock, a pedal pushed to the key's ask, a heading
/// turned to the key's bearing — so the server's answer, the same keys a round trip later, comes to where this already
/// stands, and a key the server never heard is outlived by the next one held.
/// </para>
/// <para>
/// <b>A body is shown facing its keys only while one is held</b>: let go, its heading is the server's again, since
/// whatever else turns a body — a car, a fall — is the server's to tell.
/// </para>
/// <para>
/// <b>The hand reads the car's speed as drawn</b>, a delay behind the server's, so the reversing lamps come on as the
/// car drawn comes to a stop rather than as the server's does — which is the picture they are seen against.
/// </para>
/// </remarks>
internal sealed class OwnKeysShown(SimConfig config)
{
    /// <summary>What the keys were last shown on, by mover number; −1 for nothing, so the next is taken up afresh.</summary>
    int _mover = -1;

    DriveCommand _command;
    float _headingRad;
    bool _facing;

    /// <summary>The local instant the hand has been stepped to.</summary>
    double _steppedS;

    /// <summary>
    /// The keys held, <paramref name="keys"/>, shown on the player's own mover in <paramref name="replica"/> as posed for
    /// the local <paramref name="localS"/>; <paramref name="longestReplayS"/> is past where a stall is not caught up, the
    /// server's own answer being on screen by then.
    /// </summary>
    public void Show(Replica replica, in InputMessage keys, double localS, double longestReplayS)
    {
        var own = replica.OwnMover;
        var slot = replica.Slot(own);
        var kind = own < 0 ? default : replica.Movers.KindOf(own);
        if (slot < 0 || !replica.Own(out _, out var velocityMps, out var forward) || kind == MoverKind.Wagon)
        {
            _mover = -1;
            return;
        }

        if (own != _mover || localS - _steppedS > longestReplayS) TakeUp(replica, own, slot, localS);

        var hand = new HandInput(Held: true, keys.Throttle, keys.Steer, keys.Handbrake, keys.WalkDirection, keys.Boost);
        var walking = !hand.Handbrake && hand.WalkDirection.LengthSquared() > 0f;
        if (kind == MoverKind.Person && walking && !_facing) _headingRad = replica.People.HeadingRad[slot];
        _facing = kind == MoverKind.Person && walking;

        var tickS = 1d / config.Sim.TickRateHz;
        for (; _steppedS + tickS <= localS; _steppedS += tickS)
        {
            if (kind == MoverKind.Car) _command = TownWorld.HandCommand(_command, replica.Cars.BuildOf(slot), Vector2.Dot(velocityMps, forward), hand, config);
            else if (_facing) Face(hand.WalkDirection);
        }

        if (kind == MoverKind.Car) ShowOnTheCar(replica.Cars, slot);
        else if (_facing) replica.People.HeadingRad[slot] = _headingRad;
    }

    void TakeUp(Replica replica, int own, int slot, double localS)
    {
        _mover = own;
        _steppedS = localS;
        _command = replica.Movers.KindOf(own) == MoverKind.Car ? new DriveCommand(replica.Cars.Command[slot].SteerRad, 0f, 0f, false, false) : default;
        _facing = false;
    }

    /// <summary>One tick of the body turning to the keys' bearing, as <see cref="WalkerFollower.Step"/> turns it.</summary>
    void Face(Vector2 wanted)
    {
        var mostRad = config.PersonTurnRateDegPerS * MathF.PI / 180f * config.TickSeconds;
        _headingRad = WalkerFollower.TurnToward(_headingRad, MathF.Atan2(wanted.Y, wanted.X), mostRad);
    }

    /// <summary>The wheels and the pedals' lamps from the hand, over a car whose lamps are lit at all (CAR-14).</summary>
    void ShowOnTheCar(CarFleet cars, int slot)
    {
        cars.Command[slot] = _command;
        if (cars.Lamps[slot] == CarLampSet.None) return;

        var lamps = cars.Lamps[slot] & ~(CarLampSet.Brake | CarLampSet.Reverse);
        if (_command.BrakeMps2 > config.Lamps.BrakeMps2) lamps |= CarLampSet.Brake;
        if (_command.Reverse) lamps |= CarLampSet.Reverse;
        cars.Lamps[slot] = lamps;
    }
}
