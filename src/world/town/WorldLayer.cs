using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Terrain;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The world layer</b> (root <c>decision-log.md</c>, 2026-10-11): the physics engine — tyres, a walker's footing,
/// the solver, the contacts — and the lane ribbons each moving body stands on. It owns all of that state, laid once off
/// a <see cref="WorldLaying"/>, steps from what it left last tick, and is told nothing but the <see cref="Signals"/>;
/// what the actor layer reads of it is the <see cref="View"/> and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two implementations, one contract</b>: <see cref="ManagedWorld"/> for a head with no compute (the browser) and the
/// suite, and the device's (<c>app/devicetick/DeviceWorld</c>). The actor layer cannot tell which it drives.
/// </para>
/// <para>
/// <b>One step ahead of what it shows</b> (<see cref="StepsAhead"/>): a <see cref="Step"/> waits for the step begun the
/// tick before, makes what it left the <see cref="View"/>, and begins this tick's — so the device steps while the actors
/// decide the next tick off the view before it, and neither waits on the other but once a tick. The managed world steps
/// on a thread of its own the same way, or at once where there is none, and shows the answer as late either way — so the
/// actor layer reads the same world off any of them.
/// </para>
/// <para>
/// <b>Every step reads the covers</b> (TER-49): some agent takes its turn to claim on every tick, off the view whose step
/// read them (<see cref="WorldView.CoversRead"/>).
/// </para>
/// <para>
/// A class with virtual per-tick calls and none per agent: a signal is a non-virtual append to a buffer, and a tick is
/// one <see cref="Step"/>.
/// </para>
/// </remarks>
internal abstract class WorldLayer : IDisposable
{
    /// <summary>What the step begun and not yet shown is written into; swapped with <see cref="View"/> once it is.</summary>
    WorldView _ahead;

    bool _stepAhead;

    protected WorldLayer(WorldLaying laying)
    {
        Config = laying.Config;
        Signals = new ControlSignals(laying.Cars.Length + laying.Walkers.Length);
        View = new WorldView(laying.Walkers.Length, laying.Cars.Length, slot: 0);
        _ahead = new WorldView(laying.Walkers.Length, laying.Cars.Length, slot: 1);
        Marks = new DriftMarks(laying.Config.Marks.Capacity, laying.Config.Grid.Main, 0f, laying.WorldSizeM.Y);
    }

    protected SimConfig Config { get; }

    /// <summary>What the actor layer asks of the world this tick, in the order it decided it.</summary>
    public ControlSignals Signals { get; }

    /// <summary>The world as the step before the last one begun left it (<see cref="StepsAhead"/>).</summary>
    public WorldView View { get; private set; }

    /// <summary>One of the two views by its <see cref="WorldView.Slot"/>, for a world that writes each into memory of its own.</summary>
    protected WorldView ViewInSlot(int slot) => View.Slot == slot ? View : _ahead;

    /// <summary>How many steps the world runs ahead of its <see cref="View"/>.</summary>
    public const int StepsAhead = SimFigures.WorldStepsAhead;

    /// <summary>What the tyres have written on the ground. Scenery: drawn, and read by nothing that decides anything.</summary>
    public DriftMarks Marks { get; }

    /// <summary>How many steps the world has begun.</summary>
    public long Steps { get; private set; }

    /// <summary>
    /// How many batches of signals the world has taken: one a step, and one each time the covers are read off the clock.
    /// A body a signal sets is where it was set from the view that includes its batch (<see cref="WorldView.SignalsTaken"/>).
    /// </summary>
    public long SignalsTaken { get; private set; }

    /// <summary>
    /// <b>One step</b>: the step begun last tick waited for and made the <see cref="View"/>, then this tick's signals taken
    /// and emptied and the next step begun — every body stepped, the touches begun, and the covers read.
    /// </summary>
    public void Step(float dtS)
    {
        ShowTheStepAhead();
        _ahead.CoversRead = false;
        _ahead.SignalsTaken = ++SignalsTaken;
        Begin(dtS, _ahead, readCovers: true);
        _stepAhead = true;
        Signals.Clear();
        Steps++;
    }

    /// <summary>The step begun: the signals taken, the bodies stepped, the answer written into <paramref name="into"/> by <see cref="Finish"/> at latest.</summary>
    protected abstract void Begin(float dtS, WorldView into, bool readCovers);

    /// <summary>The step begun last waited for, and whatever of its answer is not yet in <paramref name="into"/> written there.</summary>
    protected abstract void Finish(WorldView into);

    void ShowTheStepAhead()
    {
        if (!_stepAhead) return;

        Finish(_ahead);
        (View, _ahead) = (_ahead, View);
        _stepAhead = false;
    }

    /// <summary>
    /// <b>The covers read now</b>, for whoever lays the claims off their clock: the step ahead shown, the signals written
    /// since it taken, so a body put somewhere is read where it was put, and the view written again with them.
    /// </summary>
    public void ReadTheCovers()
    {
        ShowTheStepAhead();
        View.SignalsTaken = ++SignalsTaken;
        TakeTheSignalsAndReadCovers(View);
        Signals.Clear();
    }

    /// <summary>
    /// This tick's signals taken, without a step, and <paramref name="into"/> written as they leave the world, its covers
    /// read with it.
    /// </summary>
    protected abstract void TakeTheSignalsAndReadCovers(WorldView into);

    /// <summary>
    /// The bodies of one kind whose boxes meet a box, as roster indices into <paramref name="into"/>; −1 where the world
    /// keeps no index to answer from or is stepping it, and the caller takes the whole roster.
    /// </summary>
    public abstract int InBox(Vector2 leastM, Vector2 mostM, BodyKind kind, int mostCells, Span<int> into);

    /// <summary>
    /// The figures retuned under a standing town: the cars' builds (<see cref="Agents.Car.Body.CarBuilds.Resolve"/>, read
    /// again off the laying's table) and what a wheel on and off a way stands on.
    /// </summary>
    public abstract void FiguresChanged(in WheelGrounds grounds);

    public abstract int IntegratedBodyCount { get; }

    public abstract int FrozenBodyCount { get; }

    /// <summary>For an instrument that measures or draws the solver itself, where the world is stepped in managed code.</summary>
    public virtual PhysicsWorld? Solver => null;

    /// <summary>
    /// What the world has allocated on a thread of its own, all told — what a gate counting the caller's thread does not
    /// see (rule 2). None for a world that steps on the caller's.
    /// </summary>
    public virtual long AllocatedAsideBytes => 0;

    public virtual void Dispose()
    {
    }
}
