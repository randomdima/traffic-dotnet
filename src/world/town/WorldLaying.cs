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
/// One rail vehicle as a world is laid with it: its body, the buffers past it, and its wheels and weight
/// (<see cref="RailBuild"/>) — out of the world until its train is put on the map (TRN-7).
/// </summary>
internal readonly record struct WagonLaid(Vector2 HalfSizeM, float BufferM, float CornerRadiusM, RailBuild Build)
{
    /// <summary>
    /// <b>The coupling behind <paramref name="ahead"/></b> (SOL-38): the solver's bar from the end of its body to the end of
    /// this one's, as long as their two buffers, so the buffers meet.
    /// </summary>
    public (Vector2 AheadM, Vector2 BehindM, float RestM) CoupledBehind(in WagonLaid ahead) =>
        (new Vector2(-ahead.HalfSizeM.X, 0f), new Vector2(HalfSizeM.X, 0f), ahead.BufferM + BufferM);
}

/// <summary>
/// A level crossing's arm as a world is laid with it (RAIL-10): where its middle stands lying down, its heading, and its
/// half-size — out of the world until its crossing first lowers it, a world being laid before the town's first tick.
/// </summary>
internal readonly record struct BarrierLaid(Vector2 MiddleM, float HeadingRad, Vector2 HalfSizeM);

/// <summary>
/// <b>What a world is laid off</b>, once and as plain data: the furniture, the ground, the builds, each agent's body
/// as the town stood it, and the lanes a car is read along (<see cref="LineReading"/>). Nothing in it is the actor
/// layer's own state.
/// </summary>
/// <remarks>
/// <para>
/// <b>One numbering</b>: a world stands the furniture first, then every walker, then every car, each in roster order
/// (<see cref="AgentRoster"/>), then every rail vehicle the town's trains can field (<see cref="Wagons"/>), signalled as
/// the agents past the roster (<see cref="AgentOfWagon"/>) — driven by its train's driver, so on no roster the loop walks —
/// and last every level crossing's arm (<see cref="AgentOfBarrier"/>), moved by its crossing.
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

    /// <summary>Every level crossing's arm, a body of its own an approach (RAIL-10).</summary>
    public BarrierLaid[] Barriers { get; init; } = [];

    /// <summary>
    /// How many links a world holds room for (SOL-38): a tow at each end of every car and every rail vehicle (EVA-5), then a
    /// coupling a rail vehicle — each in its own slot.
    /// </summary>
    public int LinkSlots => (2 * (Cars.Length + Wagons.Length)) + Wagons.Length;

    /// <summary>The link slot a rail vehicle's coupling to the vehicle ahead of it is held in.</summary>
    public int LinkSlotOfWagon(int wagon) => (2 * (Cars.Length + Wagons.Length)) + wagon;

    /// <summary>
    /// <b>The link slot a tow on one end of a body is held in</b> (EVA-5), keyed by the body towed and the end held: one
    /// fork to an end, so two tractors holding one body from its two sides hold it in two slots.
    /// </summary>
    public int LinkSlotOfTow(int loadAgent, bool byTheTail) =>
        (2 * (IsAWagon(loadAgent) ? Cars.Length + WagonOfAgent(loadAgent) : Roster.CarIndex(loadAgent))) + (byTheTail ? 1 : 0);

    /// <summary>Whether a signal's agent is a rail vehicle.</summary>
    public bool IsAWagon(int agent) => agent >= Roster.Count && agent < Roster.Count + Wagons.Length;

    public int WagonOfAgent(int agent) => agent - Roster.Count;

    /// <summary>
    /// <b>Where on a towed body the arm holds it</b> (EVA-5): the eye in the body's own frame a tractor of
    /// <paramref name="reachM"/> pins its hinge to, and how far along its axis the fork is — a car's off its build, a rail
    /// vehicle's off its body.
    /// </summary>
    public (Vector2 EyeM, float ForkAlongM) TowedAt(int loadAgent, float reachM, bool byTheTail)
    {
        var gripM = IsAWagon(loadAgent)
            ? TowBar.GripFromTheMiddleM(Wagons[WagonOfAgent(loadAgent)].HalfSizeM.X * 2f, Config.Evacuator.TowGripInsideTheEndM)
            : Builds.Of(Cars[Roster.CarIndex(loadAgent)].Variant).TowGripFromTheMiddleM;
        return (TowBar.EyeOnTheBodyM(gripM, reachM, byTheTail), TowBar.ForkAlongM(gripM, byTheTail));
    }

    /// <summary>Everything a signal may name: the roster, then the wagons, then the barriers.</summary>
    public int Agents => Walkers.Length + Cars.Length + Wagons.Length + Barriers.Length;

    /// <summary>The agent a signal names a wagon by.</summary>
    public int AgentOfWagon(int wagon) => Walkers.Length + Cars.Length + wagon;

    /// <summary>And a barrier, by its approach's gear.</summary>
    public int AgentOfBarrier(int gear) => Walkers.Length + Cars.Length + Wagons.Length + gear;

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

        if (agent < Walkers.Length + Cars.Length) return new BodyTag(BodyKind.Car, agent - Walkers.Length);

        var wagon = agent - Walkers.Length - Cars.Length;
        return wagon < Wagons.Length ? new BodyTag(BodyKind.Train, wagon) : new BodyTag(BodyKind.Barrier, wagon - Wagons.Length);
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

        for (var gear = 0; gear < Barriers.Length; gear++)
        {
            ref readonly var laid = ref Barriers[gear];
            var body = bodies.AddBarrier(laid.MiddleM, laid.HeadingRad, laid.HalfSizeM);
            bodies.Tag(body, new BodyTag(BodyKind.Barrier, gear));
            bodies.Contain(body);
        }

        bodies.FitLinks(LinkSlots);
        return bodies;
    }
}
