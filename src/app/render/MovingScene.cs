using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Evacuator;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.TrafficLight.Body;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.World.Rail;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Render;

/// <summary>
/// <b>What moves in a frame, handed to the fill as data</b> (<see cref="TownSprites.FillMoving(in MovingScene, Core.Config.SimConfig, System.Numerics.Vector2, System.Numerics.Vector2, float, Span{SpriteInstance}, Span{SpriteInstance})"/>):
/// the fleets and the fields of them a picture reads, which may be drawn and in what order, and the clock the lamps,
/// the heads and the barriers are shown at.
/// </summary>
/// <remarks>
/// <b>Two hold one</b>: a town that steps holds its own fleets, and a player served a town holds copies of the area it
/// is sent (MP-7), so the picture is drawn from the same fields whichever it is and never learns which.
/// </remarks>
internal readonly ref struct MovingScene
{
    public required CarFleet Cars { get; init; }

    public required PersonFleet People { get; init; }

    /// <summary>The cars the frame may show, in index order; each is culled against the view as it is drawn.</summary>
    public required ReadOnlySpan<int> CarsDrawn { get; init; }

    /// <summary>And the walkers.</summary>
    public required ReadOnlySpan<int> PeopleDrawn { get; init; }

    /// <summary>Every rail vehicle, those out of the world among them (<see cref="WagonView.IsInWorld"/>).</summary>
    public required ReadOnlySpan<WagonView> Wagons { get; init; }

    public required TowHitches Hitches { get; init; }

    /// <summary>Whether the town has a level above the ground, which is whether the cars are parted by it.</summary>
    public required bool Levelled { get; init; }

    /// <summary>The instant it is drawn at, on the town's clock.</summary>
    public required float ElapsedS { get; init; }

    public required SignalHeads Heads { get; init; }

    public required SignalService Signals { get; init; }

    public required CrossingGear CrossingGear { get; init; }

    public SpriteRings Rings { get; init; }
}
