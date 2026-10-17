using System.Diagnostics;
using TrafficSimulation.Agents.TrafficLight.Body;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Routing;
using TrafficSimulation.World.Statics;

namespace TrafficSimulation.World.Town;

internal sealed partial class TownWorld
{
    /// <summary>
    /// <b>A town laid a stage at a time</b> (<see cref="Staged{T}"/>): what it stands on first (<see cref="Groundwork"/>),
    /// each of whose stages a page paints after, and then the town stood on it in one block.
    /// </summary>
    /// <param name="worldMaker">
    /// The world the town stands on where it is not the managed one, made as the town is laid (<see cref="IWorldMaker"/>).
    /// </param>
    public static Staged<TownWorld> Laying(CityPlan plan, SimConfig config, IWorldMaker? worldMaker = null) =>
        new(made => Laying(plan, config, worldMaker, made));

    static IEnumerable<string> Laying(CityPlan plan, SimConfig config, IWorldMaker? worldMaker, Action<TownWorld> made)
    {
        var groundwork = new Groundwork(plan, config, worldMaker);
        foreach (var stage in groundwork.Lay()) yield return stage;

        made(new TownWorld(groundwork, standStatics: true, agentSeed: null));
    }

    /// <summary>
    /// <b>What a town stands on, laid ahead of it</b>: its networks, its zebras, bars and lights, its ways in one numbering
    /// and the lines they are travelled on, and their marks and atlas — the open's long half, laid a stage at a time
    /// (<see cref="Lay"/>) so a page paints between stages, and handed whole to the town's constructor.
    /// </summary>
    sealed class Groundwork(CityPlan plan, SimConfig config, IWorldMaker? worldMaker)
    {
        public CityPlan Plan => plan;

        public SimConfig Config => config;

        public IWorldMaker? WorldMaker => worldMaker;

        /// <summary>When the town began to be laid, which its standing time is measured from.</summary>
        public long StartedAt { get; } = Stopwatch.GetTimestamp();

        public RoadGraph Roads { get; private set; } = null!;

        public bool Levelled { get; private set; }

        public double RoadsMs { get; private set; }

        public BayStreets BayStreets { get; private set; } = null!;

        public DrivingNetwork Driving { get; private set; } = null!;

        public PavementLanes PavementLanes { get; private set; } = null!;

        public Crossings Zebras { get; private set; } = null!;

        public StopBars Bars { get; private set; } = null!;

        public SignalService Signals { get; private set; } = null!;

        public SignalHeads Heads { get; private set; } = null!;

        public CrossingWays CrossingWays { get; private set; } = null!;

        public FootGraph Foot { get; private set; } = null!;

        public double FootMs { get; private set; }

        public WalkingNetwork Walking { get; private set; } = null!;

        public double WalkingMs { get; private set; }

        public TownWays Ways { get; private set; } = null!;

        public LaneFurniture Furniture { get; private set; } = null!;

        public CrossingEdges CrossingEdges { get; private set; } = null!;

        public SignalHolds SignalHolds { get; private set; } = null!;

        /// <summary>The walkers and cars the town lays room for: the plan's, and a crew and a vehicle for every service bay.</summary>
        public int Walkers { get; private set; }

        public int Drivers { get; private set; }

        public BuildingUses Uses { get; private set; } = null!;

        public WayLines Lines { get; private set; } = null!;

        /// <summary>The host's atlas, where the town has no world made for it that files its own.</summary>
        public RibbonAtlas? Atlas { get; private set; }

        public WayCrossings Marks { get; private set; } = null!;

        public double AtlasMs { get; private set; }

        /// <summary>Every stage laid at once, for a town stood in one call.</summary>
        public static Groundwork Through(CityPlan plan, SimConfig config, IWorldMaker? worldMaker)
        {
            var laid = new Groundwork(plan, config, worldMaker);
            foreach (var _ in laid.Lay())
            {
            }

            return laid;
        }

        /// <summary>Each stage laid, said as it passes (<see cref="OpenWatch.Passed"/>).</summary>
        public IEnumerable<string> Lay()
        {
            var roadsAt = Stopwatch.GetTimestamp();
            Roads = RoadGraph.Build(plan, config);
            Levelled = Array.Exists(plan.Roads.Level, level => level != CityPlan.RoadArrays.Ground);
            RoadsMs = Stopwatch.GetElapsedTime(roadsAt).TotalMilliseconds;

            // The bays' streets come before the network that prices them: the one movement a route may make that
            // no junction admits is a turn back — at a car park (GEN-4l) or at a dead end — and whether a frontage
            // has a bay to make one in is a question about which bays it works.
            BayStreets = BayStreets.Build(plan, Roads);

            // Laid with the town rather than on demand: a structure the tick reads belongs to the town's
            // own standing cost.
            Driving = DrivingNetwork.Build(Roads, BayStreets.WhereALegMayTurn(Roads, BayStreets, config.TurnAtALotWithinM), config);
            yield return OpenWatch.Passed("town: roads");

            // Both networks are read by the tick — cars over one, walkers over the other — so both are laid
            // with the town rather than the first time something asks.
            var footAt = Stopwatch.GetTimestamp();
            PavementLanes = PavementLanes.Of(plan, config);

            // <b>The town's zebras, once</b> (TER-6): where the walk is crossed is where the road is crossed,
            // so the walk that is cut at them, the lanes that carry them and the bands under them are all read
            // off this one laying.
            var ends = plan.Paving(config).RoadEnds(config);
            Zebras = Crossings.Lay(plan, config, ends.CrossedM);

            // <b>And its bars, off what each arm holds behind</b> rather than where it is crossed (WLK-10a): a street
            // crossed once midway is still held at both of its ends.
            Bars = StopBars.Lay(plan.Paving(config).Lanes, Crossings.Lay(plan, config, ends.HeldM), config, Zebras);
            Signals = SignalService.Build(plan, Roads, Zebras, config);
            Heads = SignalHeads.Place(Bars, Zebras, Signals, config);
            CrossingWays = CrossingWays.Of(plan, PavementLanes, Zebras, config);
            Foot = FootGraph.Build(PavementLanes, CrossingWays, config);
            FootMs = Stopwatch.GetElapsedTime(footAt).TotalMilliseconds;
            yield return OpenWatch.Passed("town: pavement");

            var walkingAt = Stopwatch.GetTimestamp();
            Walking = WalkingNetwork.Build(Foot, config);
            WalkingMs = Stopwatch.GetElapsedTime(walkingAt).TotalMilliseconds;

            // <b>And then the one table all of them are numbered in</b> (TER-4c.2, <see cref="TownWays"/>). It is
            // laid last of the networks because it is laid over them: what it takes from each is a run of
            // lengths, so no network below it learns that the others are there.
            Ways = TownWays.Of(Roads, PavementLengthsM(Walking, out var mitreLengthM), mitreLengthM);
            Furniture = LaneFurniture.Project(Bars, Roads);
            CrossingEdges = CrossingEdges.Of(Zebras, Walking.Foot, config.Grid.Main);
            SignalHolds = SignalHolds.Of(Signals, Bars, CrossingEdges, Roads, Ways);

            // The service vehicles are laid on top of the plan's own spawns: a vehicle for every bay of every
            // hospital's, station's and depot's apron (AMB-2, SRV-2), which is why the counts have to be answerable
            // from the plan alone. A building that turns out to have no bay near it leaves its slots unused, which
            // is what makes the rosters the fleets' own counts rather than these capacities. Which buildings they
            // are is the map's (GEN-9). <b>The car roster grows by the vehicles, and the walker roster by the crew each
            // one carries</b> (SRV-3) — laid now, since a roster is never grown once the town stands.
            Uses = BuildingUses.Of(plan);
            var served = (Uses.Hospitals.Count + Uses.PoliceStations.Count + Uses.Depots.Count) * config.Service.ApronBays;
            Walkers = PeopleIn(plan) + (served * CrewAboard(config));
            Drivers = CarsOfThePlan(plan) + served;

            // <b>The ground of every way at once</b> (TER-4c.4): which ribbons cover which ground, and which share
            // it. Laid over the one numbering, so it comes after every network that numbers a way, after the roster it
            // files the walk for — and between two collections (CollectTheLay).
            yield return OpenWatch.Passed("town: walking, the ways");
            CollectTheLay(plan, config);
            yield return OpenWatch.Passed("town: collected before the atlas");

            var atlasAt = Stopwatch.GetTimestamp();
            Lines = new WayLines(Roads, Walking, Ways, CrossingEdges, config);
            void AfterTheMarks()
            {
                OpenWatch.Passed("town: the atlas's marks");
                CollectTheLay(plan, config);
                OpenWatch.Passed("town: collected after the marks");
            }

            // <b>Filed by the world that keeps it, where it has one</b> — begun before the marks, so the device files while the host
            // works them out, and the furniture checked on the device against what it filed; on the host whole otherwise.
            if (worldMaker is null)
            {
                Atlas = RibbonAtlas.Lay(Lines, config.RibbonLevel, config.RibbonTouchM, anyoneWalks: Walkers > 0, AfterTheMarks);
                Marks = Atlas.Marks;
                RefuseFurnitureOnTheRoad(Atlas);
            }
            else if (worldMaker.Marker is not { } marker)
            {
                var lengthM = FileTheAtlas(worldMaker.Atlas, anyoneWalks: Walkers > 0);
                Marks = RibbonAtlas.MarksOf(Lines, lengthM, config.RibbonLevel, config.RibbonTouchM);
                AfterTheMarks();
            }
            else
            {
                // <b>And the marks worked out there too</b>, the host waiting on the device's answer a stage at a time — a
                // frame each on a page, which cannot wait.
                var lengthM = FileTheAtlas(worldMaker.Atlas, anyoneWalks: Walkers > 0);
                using var marking = marker.Mark(
                    RibbonMarks.Filing(Lines, config.RibbonLevel.CellM, config.RibbonTouchM, config.RibbonLevel.Grid.Main));
                yield return OpenWatch.Passed("town: the marks on the device");
                while (!marking.Answered()) yield return "town: the marks on the device";

                Marks = RibbonMarks.OfRows(Lines, marking.Rows, lengthM);
                AfterTheMarks();
            }

            AtlasMs = Stopwatch.GetElapsedTime(atlasAt).TotalMilliseconds;
            yield return OpenWatch.Passed("town: the atlas filed");
            CollectTheLay(plan, config);
            yield return OpenWatch.Passed("town: collected after the atlas");
        }

        /// <summary>
        /// <b>The town's furniture stands on no ground the traffic drives</b> (TER-4c.4), and a town that puts a
        /// prop there is refused rather than driven round: a prop is not a body, holds no reservation, and a
        /// driver would be told nothing of it.
        /// </summary>
        /// <remarks>A device that files the atlas asks it of its own (<see cref="FurnitureForTheDevice"/>).</remarks>
        void RefuseFurnitureOnTheRoad(RibbonAtlas atlas)
        {
            Span<WayCover> under = stackalloc WayCover[MostWaysUnderABody];
            for (var prop = 0; prop < plan.Props.Count; prop++)
            {
                var count = atlas.UnderDisc(plan.Props.CentreM[prop], plan.Props.RadiusM[prop], under);
                for (var at = 0; at < count; at++)
                {
                    if (!Ways.IsDriven(under[at].Way)) continue;

                    throw new InvalidOperationException(
                        $"prop {prop} at {plan.Props.CentreM[prop]} stands on way {under[at].Way}, which the traffic drives");
                }
            }
        }

        /// <summary>The same refusal handed to a device that files the atlas itself: every prop, and which ways are driven.</summary>
        AtlasFurniture FurnitureForTheDevice()
        {
            var driven = new bool[Ways.Count];
            for (var way = 0; way < driven.Length; way++) driven[way] = Ways.IsDriven(way);

            return new AtlasFurniture(plan.Props.CentreM, plan.Props.RadiusM, plan.Props.Count, driven);
        }

        /// <summary>
        /// <b>The atlas begun on the device that files it</b>, and the furniture handed over to be checked against it — in a
        /// method of its own, so the pieces it laid are let go of with its frame. Every way's length, which the marks are
        /// worked out over.
        /// </summary>
        float[] FileTheAtlas(IAtlasFiler filer, bool anyoneWalks)
        {
            var filing = RibbonAtlas.Filing(Lines, config.RibbonLevel, anyoneWalks);
            filer.File(filing, FurnitureForTheDevice());
            return filing.LengthM;
        }
    }
}
