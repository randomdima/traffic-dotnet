using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The lights in the reservations</b> (TLT-1): the ground each lays against the traffic, and what a driver
/// reads back of the junction in front of it.
/// </summary>
/// <remarks>
/// <b>Nothing here stops a car.</b> A light's hold is laid with the planned layer and every plan is answered
/// against it like any other (TER-4c.1), so a red is where a grant ends and a car drives to that the way it
/// drives to anything. What is left is reading: the junction's facts for the indicator and the instruments, how
/// far off a light holds the road — the one wait that spends no clock (TLT-2a) — and the bar a red was crossed
/// at.
/// </remarks>
internal sealed partial class TownWorld
{

    /// <summary>
    /// <b>The junction ahead of this car, for its indicator</b> (CAR-14.1), each tick: how far off the box its own line
    /// enters stands — nought inside it, infinity where its line enters none — and whether the way through it turns.
    /// </summary>
    /// <remarks>
    /// <b>Nothing a car drives by</b>: the box and a light's hold are in its grant (TLT-1). What else is said of the box
    /// is read where it is asked — <see cref="CommittedToTheBox"/>, <see cref="BoxIsOurs"/>, <see cref="LightAheadM(int)"/>.
    /// </remarks>
    void ReadTheBoxAhead(int car, float progressM)
    {
        Cars.ToTheBoxM[car] = float.PositiveInfinity;

        // Which way through it is comes from the *geometry the car's own line enters*, and never from
        // the lane under the car: mid-turn the nearest lane is already the one leading out.
        var ahead = LaneAheadSlot(car, progressM);
        var movement = Cars.JoinsOf(car)[ahead];
        Cars.TurningAtTheBox[car] = movement != RoadGraph.NoConnector
            && _roads.FirmOnConnector(movement) != ClaimPriority.FirmStraight;

        if (!HasABox(movement)) return;

        if (CountsRedBarCrossings) NoteBarCrossing(car, ahead, Cars.ChainOf(car)[ahead], progressM);

        var mouthM = Cars.LaneEndsOf(car)[ahead];
        Cars.ToTheBoxM[car] = progressM >= mouthM ? 0f : mouthM - (progressM + Cars.BuildOf(car).NoseAheadOfAxleM);
    }

    /// <summary>A movement with no ground under it is not a movement: two lanes meeting at a point have no box.</summary>
    bool HasABox(int movement) => movement != RoadGraph.NoConnector && _roads.ConnectorLengthM(movement) > 0f;

    /// <summary>
    /// Where on its line the box this car's line enters begins — the end of the lane it is running down — or false where
    /// its line enters none.
    /// </summary>
    bool TheBoxAhead(int car, out float mouthM)
    {
        mouthM = float.PositiveInfinity;
        if (!DrivesTheRouteLine(car)) return false;

        var ahead = LaneAheadSlot(car, Cars.ProgressM[car]);
        if (!HasABox(Cars.JoinsOf(car)[ahead])) return false;

        mouthM = Cars.LaneEndsOf(car)[ahead];
        return true;
    }

    /// <summary>
    /// <b>Whether this car is in the box ahead or can no longer stop short of it</b>: the claim's metres are the line's
    /// as it was when the claims were laid, so what it committed to is carried over as a reach from the nose it was laid
    /// from, which the ground covered since has walked on.
    /// </summary>
    internal bool CommittedToTheBox(int car)
    {
        if (!TheBoxAhead(car, out var mouthM)) return false;

        var noseM = Cars.ProgressM[car] + Cars.BuildOf(car).NoseAheadOfAxleM;
        var committedM = Cars.CommittedToM[car] - Cars.ClaimFromM[car];
        return Cars.ProgressM[car] >= mouthM || noseM - Cars.CoveredSinceClaimM[car] + committedM > mouthM;
    }

    /// <summary>Whether the road this car was granted reaches into the box ahead, or it is already inside — for the read-out.</summary>
    internal bool BoxIsOurs(int car)
    {
        if (!TheBoxAhead(car, out var mouthM)) return false;

        var noseM = Cars.ProgressM[car] + Cars.BuildOf(car).NoseAheadOfAxleM;
        return Cars.ProgressM[car] >= mouthM
            || noseM + MathF.Min(Cars.GrantLeftM(car), Cars.HorizonM[car] - Cars.CoveredSinceClaimM[car]) > mouthM;
    }

    /// <summary>
    /// <b>How far in front of this car's nose a light holds its road</b> (<see cref="LightAheadM(int, float)"/>), read where
    /// it is asked — infinity off the route's own line.
    /// </summary>
    internal float LightAheadM(int car) =>
        DrivesTheRouteLine(car)
            ? LightAheadM(car, Cars.ProgressM[car] + Cars.BuildOf(car).NoseAheadOfAxleM)
            : float.PositiveInfinity;

    /// <summary>
    /// Whether a car is driving the route's own line forwards (<see cref="DriveTheRoute"/>): the one line with a junction
    /// and a light ahead of it — a manoeuvre's piece has neither, and a car off its line drives none.
    /// </summary>
    bool DrivesTheRouteLine(int car) =>
        Cars.Line[car].LaneCount > 0
        && Cars.Action[car] is CarAction.Follow or CarAction.Overtake or CarAction.Switch or CarAction.BackUp;

    /// <summary>
    /// <b>Whether a car crossing a painted bar on a red is counted</b> (<see cref="RedBarCrossings"/>): an instrument's
    /// question, so the live town does not watch for it.
    /// </summary>
    public bool CountsRedBarCrossings { get; set; }

    /// <summary>
    /// Which lane of the chain the car is on — the one its own line is running down, which past a
    /// junction's boundary is already the lane it turned into.
    /// </summary>
    int LaneAheadSlot(int car, float progressM)
    {
        var starts = Cars.LaneStartsOf(car);
        var ahead = 0;
        while (ahead < Cars.Line[car].LaneCount - 1 && progressM >= starts[ahead + 1]) ahead++;

        return ahead;
    }

    /// <summary>
    /// <b>How far in front of this car's nose a light holds its road</b>, out to a queue's length of it — or
    /// infinity. Read off the planned layer like everything else in front of it (TER-4c.5): a light's hold is
    /// ground on the ways of the car's line, laid under the lights' own roster.
    /// </summary>
    /// <remarks>
    /// <b>The hold is found once a line and kept while the lights stand as they did</b> (<see cref="FindTheLightAhead"/>,
    /// <see cref="LightsStandAsFound"/>), and only measured each tick, to the bit what reading the ways under the reach
    /// every tick answers: a light's hold changes only at a laying, a way's list is kept in the order of its near edges,
    /// and a nose that has not gone back finds nothing nearer than what it found before. A nose that has gone back, or
    /// past the far end of what it found, looks again.
    /// </remarks>
    float LightAheadM(int car, float noseM)
    {
        var lights = _lightsAhead;
        if (lights.Line[car] != Cars.LinesLaid[car] || noseM < lights.FromNoseM[car] || !LightsStandAsFound(car))
        {
            FindTheLightAhead(car, noseM);
        }

        if (float.IsNaN(lights.WayFromM[car])) return float.PositiveInfinity;

        // What WaysAlong and AheadPlanned make of the one way the hold is on, for a reach from this nose.
        var lineFromM = MathF.Max(noseM, lights.WayFromM[car]);
        var lineToM = MathF.Min(noseM + QueueReachM(car), lights.WayToM[car]);
        if (!(lineToM > lineFromM)) return float.PositiveInfinity;

        var wayFromM = lineFromM - lights.WayFromM[car];
        if (lights.HeldToM[car] <= wayFromM)
        {
            FindTheLightAhead(car, noseM);
            return LightAheadM(car, noseM);
        }

        if (lights.HeldFromM[car] > lineToM - lights.WayFromM[car]) return float.PositiveInfinity;

        return MathF.Max(0f, (lineFromM + (lights.HeldFromM[car] - wayFromM)) - noseM);
    }

    /// <summary>
    /// <b>The nearest light's hold on this car's line past its nose</b>, however far — kept with where on the line the way
    /// it is on runs, for <see cref="LightAheadM"/> to measure until the next laying or the next line.
    /// </summary>
    void FindTheLightAhead(int car, float noseM)
    {
        var lights = _lightsAhead;
        lights.Laying[car] = _layings;
        lights.Line[car] = Cars.LinesLaid[car];
        lights.FromNoseM[car] = noseM;
        lights.WayFromM[car] = LightsAhead.None;
        lights.FromSlot[car] = LaneAheadSlot(car, noseM - Cars.BuildOf(car).NoseAheadOfAxleM);
        lights.ToSlot[car] = Cars.Line[car].LaneCount - 1;

        Span<LineWay> ways = stackalloc LineWay[MostWaysAlongALine];
        Span<WaySpan> spans = stackalloc WaySpan[MostWaysAlongALine];
        var count = _ground.WaysAlong(car, noseM, float.PositiveInfinity, ways, spans);
        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref ways[index];
            if (!_occupancy.FirstSecondary(way.Way, way.FromM, way.ToM, ClaimPriority.Signal, out var heldFromM, out var heldToM))
            {
                continue;
            }

            lights.WayFromM[car] = spans[index].StartM;
            lights.WayToM[car] = spans[index].EndM;
            lights.HeldFromM[car] = heldFromM;
            lights.HeldToM[car] = heldToM;
            lights.ToSlot[car] = SlotOfTheLane(car, lights.FromSlot[car], _ways.RoadLaneOf(way.Way));
            return;
        }
    }

    /// <summary>
    /// <b>Whether what the lights hold on the lanes a car looked down for its light is as it was when it looked</b>
    /// (<see cref="SignalHolds.ChangedAt"/>): from the lane its nose was on to the one the hold was found on, or to the
    /// line's end where none was — so the same look now finds the same hold. A look taken while a laying stood
    /// unanswered is no look.
    /// </summary>
    bool LightsStandAsFound(int car)
    {
        var lights = _lightsAhead;
        var foundAt = lights.Laying[car];
        if (foundAt < 0 || (foundAt & 1) != 0) return false;

        var chain = Cars.ChainOf(car);
        for (var slot = lights.FromSlot[car]; slot <= lights.ToSlot[car]; slot++)
        {
            if (_signalHolds.ChangedAt(chain[slot]) > foundAt) return false;
        }

        return true;
    }

    /// <summary>The slot of the chain from <paramref name="fromSlot"/> on that is this lane, or the last.</summary>
    int SlotOfTheLane(int car, int fromSlot, int lane)
    {
        var chain = Cars.ChainOf(car);
        var last = Cars.Line[car].LaneCount - 1;
        for (var slot = fromSlot; slot < last; slot++)
        {
            if (chain[slot] == lane) return slot;
        }

        return last;
    }

    /// <summary>
    /// <b>Each car's nearest light's hold, as <see cref="FindTheLightAhead"/> last found it</b>: the laying and the line it
    /// was found on, the nose it was found from and the lanes it looked down, the line's metres the way under it spans
    /// and the way's own metres of the hold.
    /// </summary>
    internal sealed class LightsAhead(int cars)
    {
        /// <summary>A <see cref="WayFromM"/> that says no light holds the line anywhere past the nose, read with <see cref="float.IsNaN"/>.</summary>
        public const float None = float.NaN;

        public readonly long[] Laying = Filled(cars, -1L);

        public readonly uint[] Line = new uint[cars];

        public readonly float[] FromNoseM = new float[cars];

        public readonly float[] WayFromM = new float[cars];

        public readonly float[] WayToM = new float[cars];

        public readonly float[] HeldFromM = new float[cars];

        public readonly float[] HeldToM = new float[cars];

        /// <summary>The slots of the chain it looked down: from the lane its axle was on to the one the hold is on, or the last.</summary>
        public readonly int[] FromSlot = new int[cars];

        public readonly int[] ToSlot = new int[cars];

        static long[] Filled(int count, long value)
        {
            var array = new long[count];
            Array.Fill(array, value);
            return array;
        }
    }

    /// <summary>
    /// Where a lane's own painted bar falls on this car's line, or infinity where the lane has none.
    /// </summary>
    float BarOnLineM(int car, int ahead, int lane)
    {
        var barAlongM = _furniture.StopBarAlongM(lane);
        return float.IsPositiveInfinity(barAlongM) ? float.PositiveInfinity : _ground.OnTheLineM(car, ahead, barAlongM);
    }

    /// <summary>
    /// The soak's own invariant, counted where it happens: a car whose nose was behind a painted
    /// bar last tick and is past it now, on an approach showing red. A figure taken anywhere else would
    /// be a sample rather than the event.
    /// </summary>
    void NoteBarCrossing(int car, int ahead, int lane, float progressM)
    {
        var barOnLineM = BarOnLineM(car, ahead, lane);
        if (float.IsPositiveInfinity(barOnLineM))
        {
            _behindTheBar[car] = false;
            return;
        }

        // Measured where a light's hold begins — the paint's near edge — and read at the rear axle, so a nose
        // brought to rest over the edge is a car that stopped and not one that crossed. A car on a call is not
        // held by the light at all (AMB-4), so it cannot be in breach of it.
        var behind = progressM < barOnLineM - (_furniture.StopBarThicknessM(lane) * 0.5f);
        if (_behindTheBar[car] && !behind && !Cars.BlueLight[car] &&
            _signals.ForApproach(lane, _elapsedS) == SignalColour.Red)
        {
            RedBarCrossings++;
            LastRedBarCrossing = new RedBarCrossing(car, Cars.PositionM[car], Cars.VelocityMps[car].Length());
        }

        _behindTheBar[car] = behind;
    }
}
