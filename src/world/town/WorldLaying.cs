using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Train.Body;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Rail;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>One car as a world is laid with it: where it stands, which build it is, and the channels it is on (PHY-1a).</summary>
internal readonly record struct CarLaid(Vector2 PositionM, float HeadingRad, byte Variant, byte Channels);

/// <summary>One walker as a world is laid with it: where it stands and faces, and whether a container holds it (PHY-7).</summary>
internal readonly record struct WalkerLaid(Vector2 PositionM, float HeadingRad, bool Inside);

/// <summary>
/// One rail vehicle as a world is laid with it: its shape, and its wheels and weight (<see cref="RailBuild"/>) — out of the
/// world until its train is put on the map (TRN-7).
/// </summary>
internal readonly record struct WagonLaid(Vector2 HalfSizeM, float CornerRadiusM, RailBuild Build);

/// <summary>
/// <b>What a world is laid off</b>, once and as plain data: the furniture, the ground, the builds, each agent's body
/// as the town stood it, and the lanes a car is read along (<see cref="LineReading"/>). Nothing in it is the actor
/// layer's own state.
/// </summary>
/// <remarks>
/// <para>
/// <b>One numbering</b>: a world stands the furniture first, then every walker, then every car, each in roster order
/// (<see cref="AgentRoster"/>), then every rail vehicle the town's trains can field (<see cref="Wagons"/>), signalled as
/// the agents past the roster (<see cref="AgentOfWagon"/>) — driven by its train's driver, so on no roster the loop walks.
/// A signal names an agent (<see cref="ControlSignal.Agent"/>), and only the world knows which body that is
/// (<see cref="BodyOf"/>).
/// </para>
/// <para>
/// <b>No atlas for a world that keeps its own</b>, filed into it as the town was laid (<see cref="IWorldMaker"/>).
/// </para>
/// </remarks>
internal sealed record WorldLaying(
    SimConfig Config, Vector2 WorldSizeM, StaticBodies Statics, RibbonAtlas? Atlas, WheelGrounds Grounds, CarBuilds Builds,
    CarLaid[] Cars, WalkerLaid[] Walkers, RoadGraph Roads)
{
    public AgentRoster Roster => new(Walkers.Length, Cars.Length);

    /// <summary>Every rail vehicle the town's trains can field, each a body of its own (TRN-7).</summary>
    public WagonLaid[] Wagons { get; init; } = [];

    /// <summary>The railway the rail vehicles' wheels stand on (TRN-9).</summary>
    public RailGraph Rails { get; init; } = RailGraph.None;

    /// <summary>How many links a world holds room for (SOL-38): a tow a car, and a coupling a rail vehicle — each in its own slot.</summary>
    public int LinkSlots => Cars.Length + Wagons.Length;

    /// <summary>The link slot a rail vehicle's coupling to the vehicle ahead of it is held in.</summary>
    public int LinkSlotOfWagon(int wagon) => Cars.Length + wagon;

    /// <summary>Everything a signal may name: the roster, then the wagons.</summary>
    public int Agents => Walkers.Length + Cars.Length + Wagons.Length;

    /// <summary>The agent a signal names a wagon by.</summary>
    public int AgentOfWagon(int wagon) => Walkers.Length + Cars.Length + wagon;

    /// <summary>How many of the furniture's bodies the table stands first, which every agent's row is counted past.</summary>
    public int FurnitureCount { get; } = Statics.Count;

    /// <summary>
    /// <b>The laying as a world keeps it once it has stood its own ground</b> — its numbering and its agents, and none of the
    /// furniture or the atlas it laid its own tables off, which the town lets go of (the root decision log, 2026-10-13).
    /// </summary>
    public WorldLaying WithoutTheGround() => this with { Statics = null!, Atlas = null };

    /// <summary>The row of the body table an agent's body stands in.</summary>
    public int BodyOf(int agent) => FurnitureCount + agent;

    /// <summary>What a row of the body table is: an agent's body, or the furniture's (<see cref="BodyTag.None"/>).</summary>
    public BodyTag TagOf(int body)
    {
        var agent = body - FurnitureCount;
        if (agent < 0) return BodyTag.None;

        if (agent < Walkers.Length) return new BodyTag(BodyKind.Person, agent);

        return agent < Walkers.Length + Cars.Length
            ? new BodyTag(BodyKind.Car, agent - Walkers.Length)
            : new BodyTag(BodyKind.Train, agent - Walkers.Length - Cars.Length);
    }

    /// <summary>
    /// <b>The body table stood off the laying</b>, in its numbering: the furniture settled, every walker at its pose and
    /// taken out of the world where a container holds it, every car at its pose on its build's shape and weight.
    /// </summary>
    public PhysicsWorld StandTheBodies()
    {
        var bodies = new PhysicsWorld(Config);
        bodies.Fit(Statics.Count + Agents);
        bodies.AddStatics(Statics);
        for (var person = 0; person < Walkers.Length; person++)
        {
            ref readonly var walker = ref Walkers[person];
            var body = bodies.AddPerson(walker.PositionM);
            bodies.Tag(body, new BodyTag(BodyKind.Person, person));
            if (walker.Inside) bodies.Contain(body);
        }

        for (var car = 0; car < Cars.Length; car++)
        {
            ref readonly var laid = ref Cars[car];
            ref readonly var build = ref Builds.Of(laid.Variant);
            var body = bodies.AddCar(
                laid.PositionM, laid.HeadingRad, build.CollisionSizeM * 0.5f, build.CornerRadiusM, build.MassKg);
            bodies.Tag(body, new BodyTag(BodyKind.Car, car));
            if (laid.Channels != CityPlan.RoadArrays.GroundChannel) bodies.PutOnChannels(body, laid.Channels);
        }

        for (var wagon = 0; wagon < Wagons.Length; wagon++)
        {
            ref readonly var laid = ref Wagons[wagon];
            var body = bodies.AddCar(Vector2.Zero, 0f, laid.HalfSizeM, laid.CornerRadiusM, laid.Build.MassKg, onRails: true);
            bodies.Tag(body, new BodyTag(BodyKind.Train, wagon));
            bodies.Contain(body);
        }

        bodies.FitLinks(LinkSlots);
        return bodies;
    }
}
