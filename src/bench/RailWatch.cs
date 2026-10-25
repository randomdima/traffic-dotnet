using System.Numerics;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>What a town's trains have to keep while it runs</b> (TRN-12): a train left to its driver holds the pace it wants —
/// its kind's, or a curve's. Beside it, quoted and never gated, what the trains did and how near their rails their wheels
/// stood (TRN-8).
/// </summary>
/// <remarks>
/// <para>
/// <b>A train off its pace and closing on it is not one held up</b>: pulling back up to it past a curve, or braking down
/// to a curve's, it is off it for as long as its traction or its brake takes, and that is the driver doing its work.
/// </para>
/// <para>
/// <b>How far a bogie stands off its line is a reading</b>: it is a fact about how finely a map's track is laid as much
/// as about the wheels, and a traced line's sharp points are the map's (<c>docs/tech-debt.md</c>).
/// </para>
/// </remarks>
internal sealed class RailWatch : ScenarioWatch
{
    const int WhatRan = 0;
    const int OnTheRails = 1;
    const int RoundTheCurves = 2;

    /// <summary>How far off its pace a train may be — a collision's dip — and for how long before it is a train held up or running away.</summary>
    public const float OffAllowanceMps = 1f;

    public const float OffForS = 2f;

    static readonly string[] TheClaims = ["a train left to its driver holds the pace it wants"];

    static readonly string[] TheReadings = ["what the trains did", "how near their rails the wheels stood", "how hard a curve pulled a train"];

    readonly int _ticksAMeasure;
    readonly float _measureS;
    readonly int _offAfterTicks;
    readonly int[] _offTicks;
    readonly float[] _wasMps;

    /// <summary>Each train's locomotive's heading at the last measure, or NaN where it was not in the world for it.</summary>
    readonly float[] _headingWasRad;

    float _hardestSideMps2;
    long _ticks;
    long _readings;
    int _longestOffTicks;
    float _mostOffMps;
    float _worstOffM;
    int _mostInTheWorld;

    public RailWatch(TownWorld world, SimConfig config)
        : base("the trains", "what every train has to keep while it runs", TheClaims, TheReadings)
    {
        _ticksAMeasure = config.Sim.TickRateHz;
        _measureS = _ticksAMeasure * config.TickSeconds;
        _offAfterTicks = (int)MathF.Ceiling(OffForS * config.Sim.TickRateHz);
        _offTicks = new int[world.Trains.Count];
        _wasMps = new float[world.Trains.Count];
        _headingWasRad = new float[world.Trains.Count];
        Array.Fill(_headingWasRad, float.NaN);
    }

    public override ClaimVerdict Verdict(int claim) => _readings == 0
        ? ClaimVerdict.Waiting
        : _longestOffTicks > _offAfterTicks ? ClaimVerdict.Broken : ClaimVerdict.Kept;

    public override void Says(int claim, ref TextBuffer into)
    {
        into.Add("most off ");
        into.Add(_mostOffMps, "F2");
        into.Add(" m/s, longest ");
        into.Add(_longestOffTicks);
        into.Add(" of ");
        into.Add(_offAfterTicks);
        into.Add(" ticks off and not closing by more than ");
        into.Add(OffAllowanceMps, "F1");
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

            case RoundTheCurves:
                into.Add("at most ");
                into.Add(_hardestSideMps2, "F2");
                into.Add(" m/s/s sideways, a locomotive's turn over a second by its speed");
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
            if (vehicles.Length == 0 || !views[vehicles[0]].IsInWorld || trains.Handed(train))
            {
                _offTicks[train] = 0;
                _headingWasRad[train] = float.NaN;
                continue;
            }

            inTheWorld += vehicles.Length;
            _readings++;
            ref readonly var loco = ref views[vehicles[0]];
            var alongMps = Vector2.Dot(loco.VelocityMps, Heading.Unit(loco.HeadingRad));
            var offMps = alongMps - trains.WantedMps(train);
            var closing = MathF.Sign(offMps) * (alongMps - _wasMps[train]) < 0f;
            _wasMps[train] = alongMps;
            _mostOffMps = MathF.Max(_mostOffMps, MathF.Abs(offMps));
            _offTicks[train] = MathF.Abs(offMps) > OffAllowanceMps && !closing ? _offTicks[train] + 1 : 0;
            _longestOffTicks = Math.Max(_longestOffTicks, _offTicks[train]);
            if (_ticks % _ticksAMeasure == 0)
            {
                _worstOffM = MathF.Max(_worstOffM, WorstOffTheLineM(world, vehicles));
                if (!float.IsNaN(_headingWasRad[train]))
                {
                    var turnedRad = MathF.Abs(MathF.IEEERemainder(loco.HeadingRad - _headingWasRad[train], MathF.Tau));
                    _hardestSideMps2 = MathF.Max(_hardestSideMps2, turnedRad / _measureS * MathF.Abs(alongMps));
                }

                _headingWasRad[train] = loco.HeadingRad;
            }
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
