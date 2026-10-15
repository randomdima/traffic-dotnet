using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>One car as a world is laid with it: where it stands, which build it is, and the channels it is on (PHY-1a).</summary>
internal readonly record struct CarLaid(Vector2 PositionM, float HeadingRad, byte Variant, byte Channels);

/// <summary>One walker as a world is laid with it: where it stands and faces, and whether a container holds it (PHY-7).</summary>
internal readonly record struct WalkerLaid(Vector2 PositionM, float HeadingRad, bool Inside);

/// <summary>
/// <b>What a world is laid off</b>, once and as plain data: the furniture, the ground, the builds, each agent's body
/// as the town stood it, and the lanes a car is read along (<see cref="LineReading"/>). Nothing in it is the actor
/// layer's own state.
/// </summary>
/// <remarks>
/// <para>
/// <b>One numbering</b>: a world stands the furniture first, then every walker, then every car, each in roster order
/// (<see cref="AgentRoster"/>). A signal names an agent (<see cref="ControlSignal.Agent"/>), and only the world knows
/// which body that is (<see cref="BodyOf"/>).
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

        return agent < Walkers.Length
            ? new BodyTag(BodyKind.Person, agent)
            : new BodyTag(BodyKind.Car, agent - Walkers.Length);
    }

    /// <summary>
    /// <b>The body table stood off the laying</b>, in its numbering: the furniture settled, every walker at its pose and
    /// taken out of the world where a container holds it, every car at its pose on its build's shape and weight.
    /// </summary>
    public PhysicsWorld StandTheBodies()
    {
        var bodies = new PhysicsWorld(Config);
        bodies.Fit(Statics.Count + Walkers.Length + Cars.Length);
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

        return bodies;
    }
}
