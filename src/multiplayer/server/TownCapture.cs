using TrafficSimulation.Multiplayer.Wire;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Multiplayer.Server;

/// <summary>
/// <b>What is drawn of the whole town at one tick</b>, laid down by the thread stepping it and read by the one sending it
/// (MP-8): every mover quantised once into its <see cref="MoverState"/>, whatever any player can see, so the tick's share
/// is one pass over the fleets and the players' areas are the network thread's work.
/// </summary>
/// <remarks>
/// <b>Three frames handed round</b>: the town writes one, the sender reads another, and the third is the newest finished
/// one waiting between them. Neither side ever waits on the other's work, only on the swap, and a frame the sender never
/// got to is simply replaced.
/// </remarks>
internal sealed class TownCapture
{
    readonly object _swap = new();
    CapturedTown _writing;
    CapturedTown _ready;
    CapturedTown _reading;
    bool _fresh;

    public TownCapture(MoverIds movers)
    {
        Movers = movers;
        _writing = new CapturedTown(movers.Count);
        _ready = new CapturedTown(movers.Count);
        _reading = new CapturedTown(movers.Count);
    }

    public MoverIds Movers { get; }

    /// <summary>The town as it stands after <paramref name="tick"/>, on the thread that stepped it. Allocates nothing.</summary>
    public void Take(TownWorld world, in WireShape shape, uint tick)
    {
        var frame = _writing;
        frame.Tick = tick;
        var cars = world.Cars;
        for (var car = 0; car < Movers.Cars; car++)
        {
            ref var mover = ref frame.Movers[Movers.OfCar(car)];
            mover.Id = Movers.OfCar(car);
            mover.X = shape.ToPosition(cars.PositionM[car].X);
            mover.Y = shape.ToPosition(cars.PositionM[car].Y);
            mover.Heading = shape.ToHeading(cars.HeadingRad[car]);
            mover.Steer = shape.ToSmallAngle(cars.Command[car].SteerRad);
            mover.TiltAlong = shape.ToSmallAngle(cars.TiltRad[car].X);
            mover.TiltAcross = shape.ToSmallAngle(cars.TiltRad[car].Y);
            mover.Lamps = (byte)cars.Lamps[car];
            mover.Flags = cars.Broken[car] ? MoverFlags.Wrecked : MoverFlags.None;
            mover.Channels = cars.Channels[car];
            mover.Driver = (byte)(world.SeatOfCar(car) + 1);
            mover.Look = cars.Variant[car];
            frame.Shown[mover.Id] = true;
        }

        var people = world.People;
        for (var person = 0; person < Movers.People; person++)
        {
            ref var mover = ref frame.Movers[Movers.OfPerson(person)];
            mover = default;
            mover.Id = Movers.OfPerson(person);
            mover.X = shape.ToPosition(people.PositionM[person].X);
            mover.Y = shape.ToPosition(people.PositionM[person].Y);
            mover.Heading = shape.ToHeading(people.HeadingRad[person]);
            mover.Flags = (people.Walking[person] ? MoverFlags.Walking : MoverFlags.None) |
                          (people.IsOnItsFeet(person) ? MoverFlags.None : MoverFlags.Down);
            mover.Channels = people.Channels[person];
            mover.Driver = (byte)(world.SeatOfPerson(person) + 1);
            mover.Look = people.Variant[person];

            // PHY-7: somebody inside a building or a car is drawn as the container, and so is sent as it.
            frame.Shown[mover.Id] = !people.Inside[person].Any;
        }

        var wagons = world.World.View.Wagons;
        for (var wagon = 0; wagon < Movers.Wagons; wagon++)
        {
            ref var mover = ref frame.Movers[Movers.OfWagon(wagon)];
            var view = wagons[wagon];
            mover = default;
            mover.Id = Movers.OfWagon(wagon);
            mover.X = shape.ToPosition(view.PositionM.X);
            mover.Y = shape.ToPosition(view.PositionM.Y);
            mover.Heading = shape.ToHeading(view.HeadingRad);
            mover.Channels = view.Channels;
            mover.Driver = (byte)(world.SeatOfWagon(wagon) + 1);
            mover.Look = (ushort)view.Look;
            frame.Shown[mover.Id] = view.IsInWorld;
        }

        lock (_swap)
        {
            (_writing, _ready) = (_ready, _writing);
            _fresh = true;
        }
    }

    /// <summary>The newest frame the town has finished since the last asked for, or null; on the sending thread.</summary>
    public CapturedTown? Newest()
    {
        lock (_swap)
        {
            if (!_fresh) return null;

            (_reading, _ready) = (_ready, _reading);
            _fresh = false;
        }

        return _reading;
    }
}

/// <summary>One frame of <see cref="TownCapture"/>: every mover by its number, and whether it is drawn at all.</summary>
internal sealed class CapturedTown(int movers)
{
    public uint Tick;

    public readonly MoverState[] Movers = new MoverState[movers];

    /// <summary>Whether the mover is drawn: a walker indoors and a wagon out of the world are not, and are sent as nothing.</summary>
    public readonly bool[] Shown = new bool[movers];
}
