using System.Numerics;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>The tick in shadow: a town's car laid into a slot as its own tick will read it, and held to what that tick did.</summary>
internal sealed partial class DeviceCarTick
{
    readonly float[] _spinMps = new float[TyreModel.Wheels];
    readonly float[] _scrubTravelM = new float[TyreModel.Wheels];
    readonly bool[] _marking = new bool[TyreModel.Wheels];
    readonly Vector2[] _markFromM = new Vector2[TyreModel.Wheels];
    readonly float[] _markIntensity = new float[TyreModel.Wheels];
    readonly SurfaceUnderWheel[] _surfaces = new SurfaceUnderWheel[TyreModel.Wheels];
    readonly WheelImpulse[] _impulses = new WheelImpulse[TyreModel.Wheels];
    readonly TyreScrub[] _scrub = new TyreScrub[TyreModel.Wheels];
    readonly TyreMark[] _marks = new TyreMark[TyreModel.Wheels];

    /// <summary>
    /// <b>Whether the device ticks this car</b>: driven, following its route's line on no pass, and on nobody's arm — the
    /// tick <c>car_tick.glsl</c> is. Asked before the town's own tick, of the state that tick reads.
    /// </summary>
    public static bool Takes(TownWorld town, int car)
    {
        var cars = town.Cars;
        return cars.Driven[car] && cars.Action[car] == CarAction.Follow && !cars.Pass[car].Any && !town.OnTheHook(car);
    }

    /// <summary>
    /// One car laid into a slot as the town holds it before its tick: its pose, last command and tyres, its line, what
    /// the last rebuild granted it and cut it with, and the junction ahead as its last tick read it.
    /// </summary>
    /// <param name="buildOf">Which of the tyre pass's builds the car is.</param>
    public void Lay(int slot, TownWorld town, int car, uint buildOf)
    {
        var cars = town.Cars;
        LayTheTyres(slot, town, car, buildOf);
        LayTheLine(slot, cars, car);

        var claims = town.Occupancy;
        var hold = town.PlanHoldOf(car);
        var cut = LaneClaim.Nothing;
        var cutOn = -1;
        if (hold != LaneOccupancy.NoHold)
        {
            claims.HoldEndsAtM(hold, out _, out cut);
            cutOn = claims.HoldCutOn(hold);
        }

        var hasAPlace = town.SceneOf(car, out var placeM, out var reachM);
        Grants[slot] = new GrantRow(
            cars.AuthorityM[car], cars.HorizonM[car], cars.ClaimFromM[car], cars.CommittedToM[car], cars.GrantMarginM[car],
            (uint)cars.GrantCutBy[car], cutOn, reachM, placeM);
        Cutters[slot] = cut.Found ? DeviceClaims.ClaimRow.Of(cut, cutOn, false, cut.FromM) : default;

        var told = (cars.BlueLight[car] ? TickRow.OnACall : 0u) | (cars.LineIsReverse[car] ? TickRow.LineIsReverse : 0u)
                   | (hasAPlace ? TickRow.HasAPlace : 0u) | (hold != LaneOccupancy.NoHold ? TickRow.HasAHold : 0u)
                   | (cut.Found ? TickRow.Cut : 0u);
        Ticks[slot] = new TickRow(
            cars.ProgressM[car], cars.CoveredSinceClaimM[car], cars.AlongMps[car], cars.OffLineM[car], cars.PlannedMps[car],
            cars.PaceMps[car], town.OffTheLineAllowanceM(car), (uint)cars.Hold[car], told);
        Boxes[slot] = new BoxRow(cars.ToTheBoxM[car], cars.LightAheadM[car], BoxFlagsOf(cars, car), 0u);
        _askedBefore[slot] = town.AsksTheRoute(car);
    }

    void LayTheTyres(int slot, TownWorld town, int car, uint buildOf)
    {
        var cars = town.Cars;
        var tyres = _tyres;
        tyres.BuildOf[slot] = buildOf;
        tyres.Poses[slot] = DeviceTyres.PoseRow.Of(new CarPose(
            cars.PositionM[car], cars.HeadingRad[car], cars.VelocityMps[car], cars.YawRateRadPerS[car], cars.MassKg[car],
            cars.AccelerationMps2[car]));
        tyres.Commands[slot] = DeviceTyres.CommandRow.Of(cars.Command[car]);
        tyres.Cars[slot] = new DeviceTyres.CarRow(
            cars.GroundCoefficient[car], 1f / town.PhysicsForInstruments.InverseInertiaOf(cars.Body[car]), cars.SlipThrottle[car],
            DeviceTyres.CarRow.FlagsOf(false, cars.DrivenSlipping[car], cars.LevelOf(car)));
        for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
        {
            var at = (car * TyreModel.Wheels) + wheel;
            tyres.Wheels[(slot * TyreModel.Wheels) + wheel] = new DeviceTyres.WheelRow(
                cars.MarkFromM[at], cars.WheelSpinMps[at], cars.ScrubTravelM[at], cars.MarkIntensity[at], cars.Marking[at] ? 1u : 0u);
        }
    }

    void LayTheLine(int slot, CarFleet cars, int car)
    {
        var line = cars.Line[car];
        Lines[slot] = LineOf(cars, car);
        var arcs = cars.LineOf(car);
        var entries = cars.EntriesOf(car);
        var arcRows = LineArcsOf(slot);
        var entryRows = EntriesOf(slot);
        for (var arc = 0; arc < line.ArcCount; arc++)
        {
            arcRows[arc] = DeviceGround.ArcRow.Of(arcs[arc]);
            entryRows[arc] = entries[arc];
        }

        var chain = ChainOf(slot);
        var starts = StartsOf(slot);
        var ends = EndsOf(slot);
        var firstArcs = FirstArcsOf(slot);
        var breaks = BreaksOf(slot);
        for (var lane = 0; lane < line.LaneCount; lane++)
        {
            chain[lane] = cars.ChainOf(car)[lane];
            starts[lane] = cars.LaneStartsOf(car)[lane];
            ends[lane] = cars.LaneEndsOf(car)[lane];
            firstArcs[lane] = (uint)cars.LaneFirstArcsOf(car)[lane];
            breaks[lane] = cars.JoinBreaksOf(car)[lane] ? 1u : 0u;
        }
    }

    static string ArcUnder(CarFleet cars, int car, float progressM)
    {
        var startM = 0f;
        var line = cars.LineOf(car);
        for (var arc = 0; arc < line.Length; arc++)
        {
            if (progressM <= startM + line[arc].LengthM) return $"arc {arc} of {line.Length} from {startM}: {line[arc]}";
            startM += line[arc].LengthM;
        }

        return $"past the {line.Length} arcs, {startM}";
    }

    static LineRow LineOf(CarFleet cars, int car) => new(
        (uint)cars.Line[car].ArcCount, (uint)cars.Line[car].LaneCount, cars.Line[car].LengthM,
        LineReader.EndsWhereTheLegDoes(cars, car) ? 1u : 0u);

    static uint BoxFlagsOf(CarFleet cars, int car) =>
        (cars.BoxIsOurs[car] ? BoxRow.Claimed : 0u) | (cars.TurningAtTheBox[car] ? BoxRow.Turning : 0u)
        | (cars.InsideTheBox[car] ? BoxRow.Inside : 0u) | (cars.CommittedToTheBox[car] ? BoxRow.Committed : 0u);

    /// <summary>
    /// <b>What the device's tick of a slot did that the town's tick of its car did not</b>, after both: whether it lost
    /// its line, where it is on its line and the line itself, what it was told and the junction ahead, then its drive and
    /// its tyres — or null where they agree. Every figure is held within <see cref="Near"/>, since a point on a bend is
    /// found by the device's own sine; every outcome is held exactly.
    /// </summary>
    /// <remarks>
    /// <b>The drive and the tyres are held to the host's own functions asked of what the device handed them</b>
    /// (<see cref="Driven"/>, <see cref="Delivered"/>), not to the town's answer. The device finds a car on its line
    /// a tenth of a millimetre from where the town does, and a pedal asked to close a speed in one tick is that tenth
    /// over a stop and a tick — so held to the town, a car braking to a stop is a hundredth of a m/s² off.
    /// </remarks>
    public string? Disagreement(int slot, TownWorld town, int car)
    {
        var cars = town.Cars;
        var tick = TicksAfter[slot];
        var lost = (tick.Flags & TickRow.Lost) != 0;
        if (lost != (cars.Action[car] == CarAction.Rejoin)) return $"lost: town {cars.Action[car]}, device {lost}";

        var shifted = (tick.Flags & TickRow.Shifted) != 0;
        if (shifted != (cars.Line[car].LaneCount < Lines[slot].LaneCount))
        {
            return $"lane end: town at {cars.ProgressM[car]} of {cars.Line[car].LaneCount} lanes, device shifted {shifted} " +
                   $"covering {tick.CoveredSinceClaimM - Ticks[slot].CoveredSinceClaimM} from {Ticks[slot].ProgressM}; next lane from {StartsOf(slot)[1]}";
        }

        // A car that lost its line has left the action its grant was laid for, and the town's Enter dropped the grant; the
        // device's tick leaves that to the action's change.
        if (!Near(cars.ProgressM[car], tick.ProgressM) || (!lost && !Near(cars.CoveredSinceClaimM[car], tick.CoveredSinceClaimM))
            || !Near(cars.AlongMps[car], tick.AlongMps) || !Near(cars.OffLineM[car], tick.OffLineM))
        {
            return $"reading: town progress {cars.ProgressM[car]} covered {cars.CoveredSinceClaimM[car]} along {cars.AlongMps[car]} " +
                   $"off {cars.OffLineM[car]}; device {tick}; {ArcUnder(cars, car, tick.ProgressM)} {ArcUnder(cars, car, cars.ProgressM[car])}";
        }

        var asks = (tick.Flags & TickRow.AsksTheRoute) != 0;
        if (asks ? !town.AsksTheRoute(car) : town.AsksTheRoute(car) && !_askedBefore[slot]) return $"asks the route: device {asks}";

        if (LinesAfter[slot] != LineOf(cars, car)) return $"line: town {LineOf(cars, car)}, device {LinesAfter[slot]}";
        for (var lane = 0; lane < cars.Line[car].LaneCount; lane++)
        {
            if (ChainAfter(slot)[lane] != cars.ChainOf(car)[lane] || StartsAfter(slot)[lane] != cars.LaneStartsOf(car)[lane]
                || EndsAfter(slot)[lane] != cars.LaneEndsOf(car)[lane])
            {
                return $"lane {lane} of the line";
            }
        }

        return Told(cars, car, ContextsAfter[slot], BoxesAfter[slot])
               ?? (lost ? Stopped(cars, car, slot) : Driven(cars, car, slot))
               ?? Delivered(town, car, slot, CommandsAfter[slot]);
    }

    static string? Told(CarFleet cars, int car, in ContextRow context, in BoxRow box)
    {
        var host = cars.Context[car];
        var same = host.GroundCoefficient == context.GroundCoefficient && Near(host.AuthorityM, context.AuthorityM)
                   && (uint)host.GrantCutBy == context.GrantCutBy && Near(host.PlaceStopM, context.PlaceStopM)
                   && host.MarginM == context.MarginM && host.WaitsToPass == (context.WaitsToPass != 0u)
                   && Near(host.HorizonM, context.HorizonM) && host.PassAsideM == context.PassAsideM && host.BackUpM == context.BackUpM
                   && host.Blocked == (context.Blocked != 0u) && host.WaitAtM == context.WaitAtM;
        if (!same) return $"told: town {host}, device {context}";

        return Near(cars.ToTheBoxM[car], box.ToTheBoxM) && Near(cars.LightAheadM[car], box.LightAheadM) && BoxFlagsOf(cars, car) == box.Flags
            ? null
            : $"box: town {cars.ToTheBoxM[car]} light {cars.LightAheadM[car]} flags {BoxFlagsOf(cars, car)}, device {box}";
    }

    /// <summary>A car that lost its line stops as the town stopped it, to the bit.</summary>
    string? Stopped(CarFleet cars, int car, int slot)
    {
        var tick = TicksAfter[slot];
        return tick.Hold == (uint)cars.Hold[car] && CommandsAfter[slot] == DeviceTyres.CommandRow.Of(cars.Command[car])
            ? null
            : $"stopped: town {cars.Hold[car]} {cars.Command[car]}, device {(DrivingHold)tick.Hold} {CommandsAfter[slot]}";
    }

    /// <summary>
    /// <b>The device's drive held to <see cref="CarFollower.DriveTheLine"/> asked of the device's own reading</b>: where
    /// it found the car on its line and what it told it, over the line the town's tick left, from the command it was left at.
    /// </summary>
    string? Driven(CarFleet cars, int car, int slot)
    {
        var tick = TicksAfter[slot];
        var told = ContextsAfter[slot];
        var context = new DriveContext(
            told.GroundCoefficient, told.AuthorityM, (HeadwayKind)told.GrantCutBy, told.PlaceStopM, told.MarginM, told.WaitsToPass != 0u,
            told.HorizonM, told.PassAsideM, told.BackUpM, told.Blocked != 0u, told.WaitAtM);
        var pose = PoseOf(slot);
        var drive = CarFollower.DriveTheLine(
            _config, cars.BuildOf(car), pose, cars.LineOf(car), cars.EntriesOf(car), tick.ProgressM, cars.Line[car].LengthM, context,
            pose.Forward, tick.AlongMps, reverse: false, CommandOf(_tyres.Commands[slot]), cars.Pass[car], laneless: false,
            cars.BlueLight[car], cars.PaceMps[car]);

        var host = DeviceTyres.CommandRow.Of(drive.Command);
        var command = CommandsAfter[slot];
        var same = Near(host.SteerRad, command.SteerRad) && Near(host.ThrottleMps2, command.ThrottleMps2)
                   && Near(host.BrakeMps2, command.BrakeMps2) && host.HandbrakeAndReverse == command.HandbrakeAndReverse
                   && (uint)drive.Hold == tick.Hold && Near(drive.PlannedMps, tick.PlannedMps)
                   && drive.Hazard == ((tick.Flags & TickRow.Hazard) != 0);
        return same ? null : $"drive: host {drive}, device {command} {(DrivingHold)tick.Hold} planned {tick.PlannedMps}";
    }

    CarPose PoseOf(int slot)
    {
        var pose = _tyres.Poses[slot];
        return new CarPose(pose.PositionM, pose.HeadingRad, pose.VelocityMps, pose.YawRateRadPerS, pose.MassKg, pose.AccelerationMps2);
    }

    static DriveCommand CommandOf(in DeviceTyres.CommandRow row) => new(
        row.SteerRad, row.ThrottleMps2, row.BrakeMps2, (row.HandbrakeAndReverse & 1u) != 0, (row.HandbrakeAndReverse & 2u) != 0);

    /// <summary>
    /// How many wheels the shadow found at the onset of a slide — sliding on one side and gripping on the other — and so
    /// did not hold (<see cref="Delivered"/>).
    /// </summary>
    public int SlideOnsets { get; private set; }

    /// <summary>
    /// <b>The device's tyre pass held to the host's asked of the device's own command</b>, off the state the town's tick
    /// read. The town's own tyres were asked the town's command, and a steer a part in ten thousand off — a point on its
    /// line found by the device's own sine — is a wheel's sideways impulse a part in a hundred off.
    /// </summary>
    /// <remarks>
    /// <b>A wheel on its friction circle answers either way</b>: the device's own tangent turns one front wheel a
    /// ten-millionth of a radian from the host's, and that decides between grip and slide, a tenth of the impulse apart. Such
    /// a wheel is counted (<see cref="SlideOnsets"/>) and not held, and neither is whether the car's driven wheels slipped,
    /// which it can decide.
    /// </remarks>
    string? Delivered(TownWorld town, int car, int slot, in DeviceTyres.CommandRow onDevice)
    {
        var cars = town.Cars;
        var four = slot * TyreModel.Wheels;
        var laid = _tyres.Cars[slot];
        var slipThrottle = laid.SlipThrottle;
        var slipping = (laid.Flags & DeviceTyres.CarRow.DrivenSlipping) != 0;
        for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
        {
            var carried = _tyres.Wheels[four + wheel];
            _spinMps[wheel] = Flushed(carried.SpinMps);
            _scrubTravelM[wheel] = Flushed(carried.ScrubTravelM);
            _marking[wheel] = carried.Marking != 0;
            _markFromM[wheel] = carried.MarkFromM;
            _markIntensity[wheel] = carried.MarkIntensity;
        }

        var after = _tyres.CarsAfter[slot];
        if (after.GroundCoefficient != cars.GroundCoefficient[car]) return $"ground: town {cars.GroundCoefficient[car]}, device {after}";

        var pose = PoseOf(slot);
        var atRest = TyrePass.Step(
            _config, town.Terrain, cars.BuildOf(car),
            pose with
            {
                VelocityMps = new Vector2(Flushed(pose.VelocityMps.X), Flushed(pose.VelocityMps.Y)),
                YawRateRadPerS = Flushed(pose.YawRateRadPerS),
            },
            CommandOf(onDevice), false, after.GroundCoefficient, laid.InertiaKgM2, cars.LevelOf(car),
            new TyreCarry(_spinMps, _scrubTravelM, _marking, _markFromM, _markIntensity, ref slipThrottle, ref slipping), _surfaces,
            _impulses, _scrub, _marks, out _);
        if (atRest != ((after.Flags & DeviceTyres.CarRow.AtRest) != 0)) return $"at rest: host {atRest}, device {after}";
        if (!Near(slipThrottle, after.SlipThrottle)) return $"throttle: host {slipThrottle}, device {after}";

        var onsets = 0;
        for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
        {
            var carried = _tyres.WheelsAfter[four + wheel];
            var spent = _tyres.Impulses[four + wheel];
            if (_scrub[wheel].Sliding != ((_tyres.Scrub[four + wheel].Flags & DeviceTyres.ScrubRow.Sliding) != 0))
            {
                onsets++;
                continue;
            }

            var same = Near(_spinMps[wheel], carried.SpinMps) && Near(_scrubTravelM[wheel], carried.ScrubTravelM)
                       && _marking[wheel] == (carried.Marking != 0) && Near(_markIntensity[wheel], carried.MarkIntensity)
                       && Near(_markFromM[wheel].X, carried.MarkFromM.X) && Near(_markFromM[wheel].Y, carried.MarkFromM.Y)
                       && Near(_impulses[wheel].ImpulseNs.X, spent.X) && Near(_impulses[wheel].ImpulseNs.Y, spent.Y)
                       && Near(_impulses[wheel].AtM.X, spent.Z) && Near(_impulses[wheel].AtM.Y, spent.W);
            if (!same) return $"wheel {wheel}: host spin {_spinMps[wheel]} {_impulses[wheel]} {_scrub[wheel]}, device {carried} {spent}";
        }

        SlideOnsets += onsets;
        return onsets > 0 || slipping == ((after.Flags & DeviceTyres.CarRow.DrivenSlipping) != 0)
            ? null
            : $"slipping: host {slipping}, device {after}";
    }

    /// <summary>Within a part in a thousand and a hundredth absolute — the device's trigonometry, as every kernel test reads it.</summary>
    static bool Near(float host, float onDevice) => host == onDevice || MathF.Abs(host - onDevice) <= 1e-2f + (1e-3f * MathF.Abs(host));

    /// <summary>
    /// A figure as a kernel reads it: <b>below the least normal float it is nought</b>, since a kernel's float flushes
    /// denormals unless it asks to keep them. A body coming to rest decays through them for a score of ticks — a velocity
    /// of 1e-38 m/s the host does not call rest and the device does.
    /// </summary>
    static float Flushed(float figure) => float.IsSubnormal(figure) ? 0f : figure;
}
