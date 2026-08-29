using System.Numerics;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>CTL-8: what a right-click on the town asked of a car.</b> Four orders and no fifth, and which one
/// a click is, is decided by what the pointer was over rather than by a mode the player has to be in.
/// </summary>
/// <remarks>
/// Every one of them is <em>a goal</em> and nothing more (CTL-2): what carries them out is the same
/// catalogue, the same routing and the same road that carries a trip, so an ordered car queues, gives way
/// and recovers exactly as any other does.
/// </remarks>
internal enum PlayerOrder : byte
{
    /// <summary>Nothing in hand: the car is idle awaiting the next order (CTL-4), or was never given one.</summary>
    None,

    /// <summary>A place on the carriageway — drive to it and come to rest on the lane that reaches it.</summary>
    DriveThere,

    /// <summary>A place in a car park — take the bay under it, or the nearest free one to it, and stand in it.</summary>
    ParkThere,

    /// <summary>
    /// A place no car can be driven to — park nearest to it, and the driver walks the rest of the way.
    /// </summary>
    ParkAndWalkThere,

    /// <summary>Another car — keep station a set distance back along the road from it, for as long as it stands.</summary>
    FollowThatCar,
}

/// <summary>
/// <b>CTL-8: the order every car is holding, and whether it is the player's at all.</b> Laid with the
/// town like every other per-car column, because an order given is one array write and a town whose
/// cars are all under orders allocates nothing.
/// </summary>
/// <remarks>
/// <b><see cref="Manual"/> and <see cref="Kind"/> are two facts and not one</b> (CTL-4). The order in
/// hand is carried out and then gone; manual mode outlives it, which is what makes a car that has
/// arrived idle awaiting the next order rather than draw a goal of its own the moment it stops.
/// </remarks>
internal sealed class PlayerOrders
{
    public PlayerOrders(int cars)
    {
        Manual = new bool[cars];
        Kind = new PlayerOrder[cars];
        PointM = new Vector2[cars];
        Lead = new int[cars];
        AimedAtM = new Vector2[cars];
        Array.Fill(Lead, NoCar);
    }

    /// <summary>Whether this car answers to the player rather than to a trip or an errand of its own.</summary>
    public bool[] Manual { get; }

    /// <summary>And what it was last told to do, or <see cref="PlayerOrder.None"/> where that is finished.</summary>
    public PlayerOrder[] Kind { get; }

    /// <summary>The place the order was given at — the point the pointer was over, and never a snapped one.</summary>
    public Vector2[] PointM { get; }

    /// <summary>The car a <see cref="PlayerOrder.FollowThatCar"/> is following, or <see cref="NoCar"/>.</summary>
    public int[] Lead { get; }

    /// <summary>
    /// Where the leg in hand was last aimed. <b>A bound on the searching and not on the following</b>: it
    /// is what says a moving leader has moved far enough to be worth a fresh route.
    /// </summary>
    public Vector2[] AimedAtM { get; }

    public const int NoCar = -1;

    /// <summary>An order taken: the car becomes the player's, and stays so until the reset (CTL-4).</summary>
    public void Take(int car, PlayerOrder kind, Vector2 pointM, int lead = NoCar)
    {
        Manual[car] = true;
        Kind[car] = kind;
        PointM[car] = pointM;
        Lead[car] = lead;
        AimedAtM[car] = pointM;
    }

    /// <summary>The order carried out, or given up on. <b>Manual mode is not one of the things it ends.</b></summary>
    public void Done(int car)
    {
        Kind[car] = PlayerOrder.None;
        Lead[car] = NoCar;
    }

    /// <summary>The reset: the car goes back to deciding for itself (CTL-4).</summary>
    public void Release(int car)
    {
        Manual[car] = false;
        Done(car);
    }
}
