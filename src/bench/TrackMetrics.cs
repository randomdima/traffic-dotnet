using System.Numerics;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// A walker a car put on the ground: which one, where it happened, whether it got up again, and <b>how far
/// into the run
/// it happened</b> — which is what makes it a thing that can be looked at, since a place with no time
/// beside it is a place a <c>--shot</c> cannot be aimed at.
/// </summary>
internal readonly record struct Knock(int Person, Vector2 AtM, bool Killed, float AtS);

/// <summary>
/// What one shape of road costs one kind of car, taken off the proving ground while it is running.
/// </summary>
/// <param name="Passes">
/// How many passes over the shape were counted. <b>A pass nobody else was in the way of</b> — a car held
/// by the one in front is a car the road is no longer the reason for.
/// </param>
/// <param name="TopMps">The fastest the car went <b>on the shape itself</b>, over the passes counted.</param>
/// <param name="HoldMps">
/// And the slowest it went there, meaned over the passes it drove <em>through</em> — a pass it was brought
/// to rest on says what the pacer did and not what the radius did.
/// </param>
/// <param name="Stops">How many of those passes ended at a standstill, which is the pacer's doing.</param>
/// <param name="SlowM">
/// The ground between going onto the brakes for this shape and being as slow as it made the car. <b>The
/// shape's braking distance</b>: a whole stop where somebody had stepped into the road at the end of it, and
/// the run down to the corner speed where nobody had.
/// </param>
/// <param name="SlowFromMps">What it was doing when it went onto them, against <paramref name="SlowToMps"/> when it stopped slowing.</param>
/// <param name="AccelS">
/// And the other half: how long, and how far, getting from the slowest the shape made it back up to speed
/// took. <b>What the shape cost on the way out</b>, which is where a tight corner is dearer than a fast one.
/// </param>
/// <param name="SlowedAtMps2">
/// What the car actually slowed at, <c>(u²−v²)/2d</c> <b>worked out for each slowing and meaned over
/// them</b>. Reconstructed from the meaned speeds and the meaned distance instead, it was a mean of means
/// and answered for no pass that ever happened: a lap that mixes a stop from 60 m/s with a dab into a
/// corner reads as one hard stop nobody made.
/// </param>
/// <param name="OffLineM">
/// The furthest the rear axle ever ran from the line the shape offered it (CAR-4a). <b>It is what makes the
/// rest of the row mean anything</b>: a speed taken by a body that has left its lane is a speed on ground
/// the road never offered.
/// </param>
internal readonly record struct SectionFigures(
    int Passes, float TopMps, float HoldMps, int Stops, int Slowings, float SlowM, float SlowFromMps, float SlowToMps,
    int Pulls, float AccelS, float AccelM, float OffLineM, float SlowedAtMps2)
{
    public bool Any => Passes > 0;
}

/// <summary>
/// <b>The proving ground read while it is being driven</b>: for every shape on the lap and every kind of
/// drivetrain, the speed the shape allows, the ground it takes to slow down to it and the time it takes to
/// get back up to speed afterwards.
/// </summary>
/// <remarks>
/// <para>
/// <b>It only watches.</b> Nothing here asks a car for anything: somebody paces the road at the end of each
/// shape and the cars stop for them of their own accord, so what is written down is the same laps anybody
/// opening <c>--map Track</c> is looking at. A rig that staged the stop would be measuring the rig.
/// </para>
/// <para>
/// <b>Every pass is a measurement, stopped or not.</b> The figures are read off the shape's own slowest
/// point rather than off a standstill — the ground down to it, the speeds either side of it, the run back
/// up from it — so a shape says what it costs whether or not somebody happened to be in the road at the end
/// of it. Somebody being there only makes the slowest point zero.
/// </para>
/// <para>
/// <b>A leg is what a pass is bounded by</b>: the link that runs up to a shape and the shape itself,
/// between the node at either end. The leg's own fastest point is where the slowing into the shape is
/// measured from and where the run back up out of the shape before it ends, which is what keeps both of
/// them local to one shape.
/// </para>
/// <para>
/// <b>Traffic is named rather than avoided, and the pacers are not traffic.</b> The cars share one lap, so
/// they queue behind whoever stopped first and catch one another on the links; a pass during which the
/// driver's own reason for its speed was another <em>car</em> is thrown away. A body in the road is what
/// the shape is measured against, and a pass held up by one is the measurement rather than the loss of it.
/// The count of passes that survived is printed beside the figures, and a section whose count is low is a
/// section nobody should be quoting.
/// </para>
/// </remarks>
internal sealed class TrackMetrics
{
    /// <summary>Rear, all four, front — in the order the share of drive on the front axle runs.</summary>
    public const int Drivetrains = 3;

    public const int Rear = 0;

    public const int All = 1;

    public const int Front = 2;

    static readonly string[] DrivetrainNames = ["rear", "all", "front"];

    /// <summary>
    /// How many shapes the lap carries. <b>A fact about the plan rather than about a town</b>, so a panel
    /// can be laid out before any town is standing.
    /// </summary>
    public static int ShapeCount { get; } = Counted();

    static int Counted()
    {
        var shapes = 0;
        foreach (var section in TrackPlan.Sections)
        {
            if (section.IsShape) shapes++;
        }

        return shapes;
    }

    readonly SimConfig _config;
    readonly float _lapM = TrackPlan.LapM();
    readonly int[] _shapeRoads;
    readonly int[] _shapeOfRoad;
    readonly Tally[] _tally;
    readonly Watch[] _watch;

    /// <summary>Whether each walker was already off its feet last tick, so a knock is counted once and not every tick of the stumble.</summary>
    readonly bool[] _wasDown;

    public TrackMetrics(SimConfig config, TownWorld world)
    {
        _config = config;
        _shapeRoads = new int[ShapeCount];
        _shapeOfRoad = new int[TrackPlan.Roads];
        Array.Fill(_shapeOfRoad, NoShape);

        var at = 0;
        foreach (var section in TrackPlan.Sections)
        {
            if (!section.IsShape) continue;

            _shapeOfRoad[section.Road] = at;
            _shapeRoads[at++] = section.Road;
        }

        _tally = new Tally[ShapeCount * Drivetrains];
        _watch = new Watch[world.Cars.Count];
        for (var car = 0; car < _watch.Length; car++) _watch[car] = new Watch();

        _wasDown = new bool[world.People.Count];
    }

    /// <summary>
    /// Whether this town is one these figures are about. <b>Either proving ground</b>: the two are the same
    /// lap and differ only in who is standing on it, which is the whole reason their tables are comparable.
    /// Every other map has no shapes to name.
    /// </summary>
    public static bool Measures(TownWorld world) =>
        string.Equals(world.Plan.Name, TrackPlan.Name, StringComparison.Ordinal)
        || string.Equals(world.Plan.Name, TrackPlan.DrunkName, StringComparison.Ordinal);

    /// <summary>How many shapes the lap carries, which is how many sections the panel and the probe print.</summary>
    public int Shapes => _shapeRoads.Length;

    public TrackSection SectionOf(int shape) => TrackPlan.Sections[_shapeRoads[shape]];

    public static string DrivetrainName(int drivetrain) => DrivetrainNames[drivetrain];

    /// <summary>How long the cars have been watched, which is what a thin count of passes is read against.</summary>
    public float WatchedS { get; private set; }

    /// <summary>
    /// How many times a car has put one of the pacers on the ground while it was being watched. <b>Anything
    /// but zero and none of the rest of this is a measurement</b> — it is a picture of a town that could not
    /// stop for what was in front of it, and a body left lying in a lane blocks that lane for good.
    /// </summary>
    public long Knocks { get; private set; }

    /// <summary>And how many of them did not get up again, which is the same event at its worst.</summary>
    public long Killed { get; private set; }

    /// <summary>Where the last of them happened, so a count above zero says which pacer it was.</summary>
    public Knock LastKnock { get; private set; }

    /// <summary>
    /// <b>What getting round the lap cost the drivers</b>: swerves taken, back-offs reversed out of, and
    /// legs given up on. They are the difference between a lap with a slow thing on it and a lap without
    /// one, and a lap where the last of them is not zero is a lap with a car parked across it for good.
    /// </summary>
    public long Swerves { get; private set; }

    public long BackOffs { get; private set; }

    public long GivenUp { get; private set; }

    /// <summary>
    /// And how many of the cars are wrecked, which is the same reading at its worst: a broken car is a
    /// permanent obstruction on a lap nothing can clear, so every figure after the one that made it is a
    /// figure about a shorter lap.
    /// </summary>
    public int Wrecked { get; private set; }

    /// <summary>How many cars are being watched.</summary>
    public int Cars => _watch.Length;

    /// <summary>
    /// How far round the lap a car has been carried, in laps. <b>What a thin count of passes is read
    /// against</b>: few passes over many laps is traffic, and few passes over few laps is a car that is not
    /// driving at all.
    /// </summary>
    public float Laps(int car) => _watch[car].MovedM / _lapM;

    /// <summary>One shape as one kind of car found it.</summary>
    public SectionFigures Figures(int shape, int drivetrain) => _tally[(shape * Drivetrains) + drivetrain].Row();

    /// <summary>One shape as every car found it, which is the figure on the section's own header.</summary>
    public SectionFigures Figures(int shape)
    {
        var whole = default(Tally);
        for (var drivetrain = 0; drivetrain < Drivetrains; drivetrain++)
        {
            whole.Add(_tally[(shape * Drivetrains) + drivetrain]);
        }

        return whole.Row();
    }

    /// <summary>
    /// One tick of the town. <b>Every car every tick</b>: a peak speed and a lift onto the brakes both
    /// happen inside one decision interval, and a sample taken a frame at a time would miss them.
    /// </summary>
    public void Saw(TownWorld world)
    {
        WatchedS = world.ElapsedS;
        Swerves = world.SwervesTaken;
        BackOffs = world.BackOffsTaken;
        GivenUp = world.LegsSettled + world.CarsAbandoned;

        Wrecked = 0;
        for (var car = 0; car < _watch.Length && car < world.Cars.Count; car++)
        {
            if (world.Cars.Broken[car]) Wrecked++;
        }

        for (var person = 0; person < _wasDown.Length && person < world.People.Count; person++)
        {
            var down = world.People.Dead[person] || world.People.OffFeetForS[person] > 0f;
            if (down && !_wasDown[person])
            {
                Knocks++;
                if (world.People.Dead[person]) Killed++;
                LastKnock = new Knock(
                    person, world.People.PositionM[person], world.People.Dead[person], world.ElapsedS);
            }

            _wasDown[person] = down;
        }

        for (var car = 0; car < _watch.Length && car < world.Cars.Count; car++) Saw(world, car);
    }

    void Saw(TownWorld world, int car)
    {
        var watch = _watch[car];
        var alongMps = world.Cars.AlongMps[car];
        var atM = world.Cars.PositionM[car];
        var nowS = world.ElapsedS;

        if (watch.Seen) watch.MovedM += (atM - watch.AtM).Length();
        (watch.AtM, watch.Seen) = (atM, true);

        // What is limiting the car is what says whether any of this is about the road at all. Another car
        // in the way is the one thing this rig cannot subtract, and it is counted against whichever
        // measurement was in progress rather than against the whole lap — a car that queued on the run-up
        // and then had the shape to itself still drove the shape on its own.
        if (HeldByTraffic(world, car))
        {
            watch.LegDirty = true;
            if (watch.OnShape) watch.ShapeDirty = true;
        }

        var road = RoadUnder(world, car);
        if (road >= 0 && road != watch.Road)
        {
            Crossed(watch, world, car, road);
            watch.Road = road;
        }

        // The leg's own fastest point is where the slowing into this shape is measured from and where the
        // run back up to speed out of the shape before it ends. Going faster again unmakes the slowing
        // that had begun: what is wanted is the first touch of the brakes after the fastest point, and
        // there is no fastest point yet while the car is still gaining.
        if (alongMps > watch.LegTopMps)
        {
            watch.LegTopMps = alongMps;
            watch.LegTopAtM = watch.MovedM;
            watch.LegTopAtS = nowS;
            watch.BrakeFromM = float.NaN;
            watch.SlowM = float.NaN;
        }

        Slowing(watch, world, car, alongMps);

        if (watch.OnShape) OnTheShape(watch, world, car, alongMps, nowS);
    }

    /// <summary>
    /// The shape's own two speeds, and the ground it took to arrive at the slower of them.
    /// </summary>
    /// <remarks>
    /// <b>The slowing is anchored on the pedal at one end and on the shape's slowest point at the other.</b>
    /// It is read off the pedal and not off the reason: a car slowing into the end of a shape is bound by
    /// the corner one moment and by the body standing in the road the next, and a figure measured from
    /// whichever of them won would be a measurement of the tie-break. The anchor is the <em>first</em> touch of the brakes
    /// after the leg's fastest point, so a corner taken in two applications is one slowing and not the last
    /// dab of it.
    /// </remarks>
    void OnTheShape(Watch watch, TownWorld world, int car, float alongMps, float nowS)
    {
        watch.OffLineM = MathF.Max(watch.OffLineM, world.Cars.OffLineM[car]);
        watch.ShapeTopMps = MathF.Max(watch.ShapeTopMps, alongMps);
        if (alongMps <= _config.Driving.StopSpeedMps) watch.StoppedOnShape = true;

        if (alongMps >= watch.ShapeLowMps) return;

        watch.ShapeLowMps = MathF.Max(alongMps, 0f);
        watch.LowAtM = watch.MovedM;
        watch.LowAtS = nowS;
    }

    /// <summary>
    /// A node crossed. <b>The lap's legs are cut where its nodes are</b>, so leaving a shape for the link
    /// after it is the end of one measurement and the beginning of the next.
    /// </summary>
    void Crossed(Watch watch, TownWorld world, int car, int road)
    {
        // The first road a car is seen on is one it was already part way along, so there is nothing behind
        // it to close: a leg begins at the first node it is watched over.
        var leftShape = watch.Road < 0 ? NoShape : _shapeOfRoad[watch.Road];
        if (leftShape != NoShape)
        {
            if (watch.OnShape) Commit(watch, world, car, leftShape);

            watch.OnShape = false;
            watch.LegDirty = false;
            watch.LegTopMps = 0f;
            watch.LegTopAtM = float.NaN;
            watch.LegTopAtS = float.NaN;
        }

        if (_shapeOfRoad[road] == NoShape) return;

        watch.OnShape = true;
        watch.ShapeDirty = false;
        watch.ShapeTopMps = 0f;
        watch.ShapeLowMps = float.PositiveInfinity;
        watch.OffLineM = 0f;
        watch.StoppedOnShape = false;
    }

    /// <summary>
    /// One whole pass, into the shape it was about — <b>and, with it, the run back up to speed out of the
    /// shape before</b>, which ends here because the fastest the car got before slowing again is the fastest
    /// this leg went.
    /// </summary>
    void Commit(Watch watch, TownWorld world, int car, int shape)
    {
        var drivetrain = Drivetrain(world.Cars.DrivenFrontShare[car]);
        if (watch.PullingOutOf != NoShape && !watch.LegDirty && watch.LegTopAtM > watch.PullAwayFromM)
        {
            ref var before = ref _tally[(watch.PullingOutOf * Drivetrains) + drivetrain];
            before.Pulls++;
            before.AccelSumS += watch.LegTopAtS - watch.PullAwayFromS;
            before.AccelSumM += watch.LegTopAtM - watch.PullAwayFromM;
        }

        if (watch.ShapeDirty || !float.IsFinite(watch.ShapeLowMps))
        {
            // Nothing about this pass is quotable, and the run out of it cannot be either: what that run
            // would be measured from is a slowest point this pass never established.
            watch.PullingOutOf = NoShape;
            return;
        }

        ref var tally = ref _tally[(shape * Drivetrains) + drivetrain];
        tally.Passes++;
        tally.TopBestMps = MathF.Max(tally.TopBestMps, watch.ShapeTopMps);
        tally.WorstOffLineM = MathF.Max(tally.WorstOffLineM, watch.OffLineM);
        if (watch.StoppedOnShape)
        {
            tally.Stops++;
        }
        else
        {
            tally.Held++;
            tally.HoldSumMps += watch.ShapeLowMps;
        }

        if (!float.IsNaN(watch.SlowM))
        {
            tally.Slowings++;
            tally.SlowSumM += watch.SlowM;
            tally.SlowFromSumMps += watch.SlowFromMps;
            tally.SlowToSumMps += watch.SlowToMps;
            tally.SlowedAtSumMps2 +=
                ((watch.SlowFromMps * watch.SlowFromMps) - (watch.SlowToMps * watch.SlowToMps))
                / (2f * MathF.Max(watch.SlowM, 1e-3f));
        }

        (watch.PullingOutOf, watch.PullAwayFromM, watch.PullAwayFromS) = (shape, watch.LowAtM, watch.LowAtS);
    }

    /// <summary>
    /// The leg's own slowing, from the first touch of the brakes after its fastest point to the moment the
    /// driver came off them again — or to a standstill, where somebody had stepped into the road at the end
    /// of the leg.
    /// </summary>
    /// <remarks>
    /// <b>Read off the pedal and not off the reason.</b> A car slowing into the end of a shape is bound by
    /// the corner one moment and by the body standing in the road the next, and a figure measured from
    /// whichever of them won would be a measurement of the tie-break. A release shorter than one reaction interval is pedal
    /// modulation and not a release, so a corner taken in two applications is one slowing rather than two.
    /// </remarks>
    void Slowing(Watch watch, TownWorld world, int car, float alongMps)
    {
        if (!float.IsNaN(watch.SlowM)) return;

        if (world.Cars.Command[car].BrakeMps2 > 0f)
        {
            if (float.IsNaN(watch.BrakeFromM))
            {
                watch.BrakeFromM = watch.MovedM;
                watch.BrakeFromMps = alongMps;
            }

            watch.OffBrakeS = 0f;
            watch.OffBrakeAtM = watch.MovedM;
            watch.OffBrakeAtMps = alongMps;

            // A car the brakes have brought to rest has finished slowing whatever the pedal is doing: it
            // will be held there for as long as somebody is in the way, and none of that is braking distance.
            if (alongMps > _config.Driving.StopSpeedMps) return;

            watch.SlowM = watch.MovedM - watch.BrakeFromM;
            watch.SlowFromMps = watch.BrakeFromMps;
            watch.SlowToMps = 0f;
            return;
        }

        if (float.IsNaN(watch.BrakeFromM)) return;

        watch.OffBrakeS += _config.TickSeconds;
        if (watch.OffBrakeS <= _config.CarReactionS) return;

        watch.SlowM = watch.OffBrakeAtM - watch.BrakeFromM;
        watch.SlowFromMps = watch.BrakeFromMps;
        watch.SlowToMps = MathF.Max(watch.OffBrakeAtMps, 0f);
    }

    /// <summary>
    /// Whether what is limiting this car is other traffic. <b>The people pacing the road are the instrument
    /// and not the traffic</b>: a car stopping for one is the stop the shape is measured by, and throwing
    /// that pass away would throw away every stop the lap has.
    /// </summary>
    /// <remarks>
    /// <b>The driver's own reading says which it is</b>, which is what the book naming everything on a lane
    /// bought: a walk of the fleet asking whether the body in front was one of the people was a search for
    /// an answer the car had already been given.
    /// </remarks>
    static bool HeldByTraffic(TownWorld world, int car) => world.Cars.Hold[car] switch
    {
        DrivingHold.Reserved => true,
        DrivingHold.Headway => world.Cars.Context[car].Ahead != HeadwayKind.Walker,
        _ => false,
    };

    /// <summary>Which stretch of the lap the car is driving, or −1 where it is on no lane at all.</summary>
    static int RoadUnder(TownWorld world, int car)
    {
        var lane = world.Cars.LaneOf(car);
        return lane < 0 ? -1 : world.Roads.LaneRoad[lane];
    }

    /// <summary>Which kind of car this is, off the one figure that differs between the cars on the lap.</summary>
    public static int Drivetrain(float drivenFrontShare) =>
        drivenFrontShare <= 0.25f ? Rear : drivenFrontShare >= 0.75f ? Front : All;

    const int NoShape = -1;

    /// <summary>What one shape has cost one kind of car so far, as sums rather than means: a mean of means is nobody's figure.</summary>
    struct Tally
    {
        public int Passes;
        public float TopBestMps;
        public int Held;
        public float HoldSumMps;
        public int Stops;
        public int Slowings;
        public float SlowSumM;
        public float SlowFromSumMps;
        public float SlowToSumMps;
        public float SlowedAtSumMps2;
        public int Pulls;
        public float AccelSumS;
        public float AccelSumM;
        public float WorstOffLineM;

        public void Add(in Tally other)
        {
            Passes += other.Passes;
            TopBestMps = MathF.Max(TopBestMps, other.TopBestMps);
            Held += other.Held;
            HoldSumMps += other.HoldSumMps;
            Stops += other.Stops;
            Slowings += other.Slowings;
            SlowSumM += other.SlowSumM;
            SlowFromSumMps += other.SlowFromSumMps;
            SlowToSumMps += other.SlowToSumMps;
            SlowedAtSumMps2 += other.SlowedAtSumMps2;
            Pulls += other.Pulls;
            AccelSumS += other.AccelSumS;
            AccelSumM += other.AccelSumM;
            WorstOffLineM = MathF.Max(WorstOffLineM, other.WorstOffLineM);
        }

        public readonly SectionFigures Row() => new(
            Passes, TopBestMps, Over(HoldSumMps, Held), Stops, Slowings, Over(SlowSumM, Slowings),
            Over(SlowFromSumMps, Slowings), Over(SlowToSumMps, Slowings), Pulls, Over(AccelSumS, Pulls),
            Over(AccelSumM, Pulls), WorstOffLineM, Over(SlowedAtSumMps2, Slowings));

        static float Over(float sum, int count) => count <= 0 ? 0f : sum / count;
    }

    /// <summary>
    /// One car's leg in progress: where it is, what the leg has reached, and what the shape it is on has
    /// held it to.
    /// </summary>
    sealed class Watch
    {
        public bool Seen;
        public Vector2 AtM;
        public float MovedM;
        public int Road = -1;

        public bool LegDirty = true;
        public float LegTopMps;
        public float LegTopAtM = float.NaN;
        public float LegTopAtS = float.NaN;

        public bool OnShape;
        public bool ShapeDirty = true;
        public float ShapeTopMps;
        public float ShapeLowMps = float.PositiveInfinity;
        public float LowAtM = float.NaN;
        public float LowAtS = float.NaN;
        public float OffLineM;
        public bool StoppedOnShape;

        public float SlowM = float.NaN;
        public float SlowFromMps;
        public float SlowToMps;

        public float OffBrakeS;
        public float OffBrakeAtM;
        public float OffBrakeAtMps;

        /// <summary>The shape whose run back up to speed is still being timed, and where that run began.</summary>
        public int PullingOutOf = NoShape;

        public float PullAwayFromM = float.NaN;
        public float PullAwayFromS = float.NaN;

        /// <summary>The first touch of the brakes since the leg was last at its fastest, which is what a slowing is measured from.</summary>
        public float BrakeFromM = float.NaN;

        public float BrakeFromMps;
    }
}
