using System.Numerics;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>What a town's trains have to keep while it runs</b> (TRN-2): a train holds its line's speed. Beside it, quoted and
/// never gated, what the trains did and how near their rails their wheels stood (TRN-8).
/// </summary>
/// <remarks>
/// <b>How far a bogie stands off its line is a reading</b>: it is a fact about how finely a map's track is laid as much
/// as about the wheels, and a traced line's sharp points are the map's (<c>docs/tech-debt.md</c>).
/// </remarks>
internal sealed class RailWatch : ScenarioWatch
{
    /// <summary>TRN-2 as this town keeps it.</summary>
    public const int HoldsItsLinesSpeed = 0;

    const int WhatRan = 0;
    const int OnTheRails = 1;

    /// <summary>How far short of its line's speed a train may fall — a collision's dip — and for how long before it is a train held up.</summary>
    public const float ShortAllowanceMps = 1f;

    public const float ShortForS = 2f;

    static readonly string[] TheClaims = ["a train holds its line's speed"];

    static readonly string[] TheReadings = ["what the trains did", "how near their rails the wheels stood"];

    readonly int _ticksAMeasure;
    readonly int _shortAfterTicks;
    readonly int[] _shortTicks;
    long _ticks;
    long _readings;
    int _longestShortTicks;
    float _mostShortMps;
    float _worstOffM;
    int _mostInTheWorld;

    public RailWatch(TownWorld world, SimConfig config)
        : base("the trains", "what every train has to keep while it runs", TheClaims, TheReadings)
    {
        _ticksAMeasure = config.Sim.TickRateHz;
        _shortAfterTicks = (int)MathF.Ceiling(ShortForS * config.Sim.TickRateHz);
        _shortTicks = new int[world.Trains.Count];
    }

    /// <summary>The longest any train has run short of its line's speed, in ticks, and the most it was short by.</summary>
    public int LongestShortTicks => _longestShortTicks;

    public float WorstOffItsLineM => _worstOffM;

    public override ClaimVerdict Verdict(int claim) => _readings == 0
        ? ClaimVerdict.Waiting
        : _longestShortTicks > _shortAfterTicks ? ClaimVerdict.Broken : ClaimVerdict.Kept;

    public override void Says(int claim, ref TextBuffer into)
    {
        into.Add("most short ");
        into.Add(_mostShortMps, "F2");
        into.Add(" m/s, longest ");
        into.Add(_longestShortTicks);
        into.Add(" of ");
        into.Add(_shortAfterTicks);
        into.Add(" ticks short by more than ");
        into.Add(ShortAllowanceMps, "F1");
    }

    public override void Reads(int reading, ref TextBuffer into)
    {
        switch (reading)
        {
            case WhatRan:
                into.Add(_sent);
                into.Add(" trains sent, ");
                into.Add(_waited);
                into.Add(" ticks waiting for a clear leg, at most ");
                into.Add(_mostInTheWorld);
                into.Add(" vehicles in the world");
                break;

            case OnTheRails:
                into.Add("a bogie at most ");
                into.Add(_worstOffM * 1_000f, "F0");
                into.Add(" mm off its line, read each second");
                break;
        }
    }

    int _sent, _waited;

    public override void Saw(TownWorld world)
    {
        _ticks++;
        var trains = world.Trains;
        var views = world.World.View.Wagons;
        var inTheWorld = 0;
        for (var train = 0; train < trains.Count; train++)
        {
            var vehicles = trains.VehiclesOf(train);
            if (vehicles.Length == 0 || !views[vehicles[0]].IsInWorld)
            {
                _shortTicks[train] = 0;
                continue;
            }

            inTheWorld += vehicles.Length;
            _readings++;
            ref readonly var loco = ref views[vehicles[0]];
            var shortMps = trains.SpeedMps(train) - Vector2.Dot(loco.VelocityMps, Heading.Unit(loco.HeadingRad));
            _mostShortMps = MathF.Max(_mostShortMps, shortMps);
            _shortTicks[train] = shortMps > ShortAllowanceMps ? _shortTicks[train] + 1 : 0;
            _longestShortTicks = Math.Max(_longestShortTicks, _shortTicks[train]);
            if (_ticks % _ticksAMeasure == 0) _worstOffM = MathF.Max(_worstOffM, WorstOffTheLineM(world, vehicles));
        }

        _mostInTheWorld = Math.Max(_mostInTheWorld, inTheWorld);
        (_sent, _waited) = (trains.Sent, trains.WaitedForGround);
    }

    /// <summary>How far the furthest bogie of a train stands off the track, as the world left it.</summary>
    static float WorstOffTheLineM(TownWorld world, ReadOnlySpan<int> vehicles)
    {
        var worstM = 0f;
        var views = world.World.View.Wagons;
        foreach (var vehicle in vehicles)
        {
            ref readonly var view = ref views[vehicle];
            var forward = Heading.Unit(view.HeadingRad);
            foreach (var bogieM in world.Trains.VehicleOf(vehicle).BogiesM)
            {
                var pivotM = view.PositionM + (forward * bogieM);
                var nearest = world.Rails.Nearest(pivotM);
                worstM = MathF.Max(worstM, Vector2.Distance(world.Rails.Segments[nearest.Segment].PlaceAt(nearest.AlongM), pivotM));
            }
        }

        return worstM;
    }
}
