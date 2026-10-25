using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Evacuator;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.TrafficLight.Body;
using TrafficSimulation.Agents.Train.Body;
using TrafficSimulation.Agents.Train.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Rail;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Terrain;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Render;

/// <summary>
/// Every look the town is drawn in, and the one pass that turns a town into instances: what stands
/// still first, the walkers over it and the cars over them.
/// </summary>
/// <remarks>
/// <para>
/// <b>One sheet list, one slot per look</b> — walkers, cars, wrecks, tow arms, roofs, prop looks, in that
/// order.
/// The sprite pipeline reaches a sheet by its index into the atlas's table of places
/// (<see cref="SheetAtlas"/>), so what decides which picture an instance is drawn with is a number in the
/// instance and never a bind, which is what keeps a frame's crossings flat whatever the town is full of.
/// </para>
/// <para>
/// <b>The order the four kinds are written in is painter's order and nothing more</b>: a walker passes
/// in front of a building and behind nothing, and a walker under a car is hidden by it.
/// </para>
/// </remarks>
internal sealed class TownSprites
{
    /// <summary>The car head's strip and the pedestrian head's.</summary>
    const int HeadSheets = 2;

    /// <summary>The two brushes a mark is stamped through, the two pictures every lamp is drawn through, a steered tyre's rubber, a crossing's barrier, its housing and its X light dark and lit, and a puff of smoke.</summary>
    const int GroundworkSheets = 10;

    TownSprites(
        PersonCatalog people, CarCatalog cars, BuildingCatalog buildings, PropCatalog props, SheetSource[] sheets,
        Vector2[] carSheetScales)
    {
        People = people;
        Cars = cars;
        Buildings = buildings;
        Props = props;
        Sheets = sheets;
        CarSheetScales = carSheetScales;
        Aspects = new float[sheets.Length];
    }

    public PersonCatalog People { get; }

    public CarCatalog Cars { get; }

    public BuildingCatalog Buildings { get; }

    public PropCatalog Props { get; }

    /// <summary>The town's standing geometry, laid when a plan is opened and empty until one is.</summary>
    public StandingSprites Standing { get; private set; } = StandingSprites.Nothing;

    /// <summary>And its scenery, laid with it.</summary>
    public ScenerySprites Scenery { get; private set; } = ScenerySprites.Nothing;

    /// <summary>The sheets in slot order — every walker's look and the same again lying down, then every car's, then the tow arms, the roofs, the prop looks, the two head strips, the two mark brushes, the lamp's lens and glow, and the rubber.</summary>
    public SheetSource[] Sheets { get; }

    /// <summary>How much larger than its art's own box each car sheet is drawn, from <see cref="FirstCarSheet"/> on, intact then wrecked (<see cref="CarSheets"/>).</summary>
    public Vector2[] CarSheetScales { get; }

    /// <summary>The widest tyre any car of the catalogue or the nominal car runs on: a frame that would leave it out leaves every steered tyre out, unasked.</summary>
    public float WidestTyreM { get; private init; }

    /// <summary>And the widest glow any lens of the catalogue is drawn with (<see cref="LampSprites.GlowM"/>), for the lamps.</summary>
    public float WidestGlowM { get; private init; }

    /// <summary>
    /// How far past its body any part of a car may be drawn, which a frame widens its box by before asking who is in
    /// it: the catalogue's longest diagonal, wreck and all — over twice any car's half of it, which is room for its
    /// tyres, its glows and its tow arm, and more than any walker stands.
    /// </summary>
    public float CarReachM { get; private init; }

    /// <summary>The frame's people and cars, refilled every frame (<see cref="TownWorld.CarsIn"/>); each laid once a roster's capacity.</summary>
    int[] _peopleInView = [];

    int[] _carsInView = [];

    /// <summary>One frame's width over its height, per slot. Only a walker's quad is shaped by it; a car's is its own footprint.</summary>
    public float[] Aspects { get; }

    /// <summary>Where the bodies in the road start: one frame a look, laid straight after that look's walk sheet block.</summary>
    public int FirstDownSheet => People.SheetCount;

    /// <summary>And where the cars start, which is past both of the walkers' blocks.</summary>
    public int FirstCarSheet => People.SheetCount * 2;

    /// <summary>Where the tow arms start (EVA-5) — a short run, because a beam is a picture almost no look has.</summary>
    public int FirstBeamSheet => FirstCarSheet + (Cars.SheetCount * 2);

    /// <summary>Where the roofs start in the sheet list. A building's picture is one image, not a grid.</summary>
    public int FirstBuildingSheet => FirstBeamSheet + Cars.BeamSpritePaths.Length;

    public int FirstPropSheet => FirstBuildingSheet + Buildings.Count;

    /// <summary>Where the fans' looks start (OBJ-6, TRN-13): never an instance's, only read inside a fan (<see cref="FanTable.PutLooks"/>).</summary>
    public int FirstFanLookSheet => FirstPropSheet + Props.Count;

    /// <summary>The two head strips, car then pedestrian. They are the town's only sheets that are not a catalogue.</summary>
    public int FirstHeadSheet => FirstFanLookSheet + Buildings.FanLooks.Length;

    /// <summary>The two stamps a mark is drawn through: rubber's, which has no edge, and soil's, which is nearly all edge.</summary>
    public int RubberBrushSheet => FirstHeadSheet + HeadSheets;

    public int SoilBrushSheet => RubberBrushSheet + 1;

    /// <summary>The sheet every lit lamp in the town is drawn from (CAR-14): a row a variant, two columns a lens.</summary>
    public int LensSheet => SoilBrushSheet + 1;

    /// <summary>And the glow around the lit ones.</summary>
    public int LampGlowSheet => LensSheet + 1;

    /// <summary>What a steered tyre is drawn with (<see cref="CarSheets.RubberSheet"/>).</summary>
    public int RubberSheet => LampGlowSheet + 1;

    /// <summary>A level crossing's barrier arm, the housing it pivots at, and its X light dark and lit (RAIL-7), the railway's art (<see cref="RailLooks"/>).</summary>
    public int BarrierSheet => RubberSheet + 1;

    public int BarrierDriveSheet => BarrierSheet + 1;

    public int CrossingLightSheet => BarrierDriveSheet + 1;

    public int CrossingLitSheet => CrossingLightSheet + 1;

    /// <summary>What every puff of a car's exhaust (CAR-54) and of a chimney's (OBJ-7) is drawn through (<see cref="SmokeSprites.Sheet"/>).</summary>
    public int SmokeSheet => CrossingLitSheet + 1;

    /// <summary>Where the rolling stock starts (TRN-4): a sheet a vehicle of the catalogue, last of all so nothing before moves.</summary>
    public int FirstTrainSheet => SmokeSheet + 1;

    /// <summary>How the exhausts' smoke is drawn (CAR-54).</summary>
    public SmokeLook Smoke { get; private init; }

    /// <summary>The most tailpipes any one look of the catalogue ends in.</summary>
    public int MostExhaustsOnALook { get; private init; }

    /// <summary>How the chimneys' smoke is drawn (OBJ-7), through the exhausts' picture.</summary>
    public ChimneyLook Chimney { get; private init; }

    /// <summary>Every pot of the town's chimneys (OBJ-7), laid with its buildings and none until then.</summary>
    public ChimneyRow[] Chimneys { get; private set; } = [];

    /// <summary>The rolling stock the trains are drawn in.</summary>
    public TrainCatalog Rolling { get; } = TrainCatalog.Shared;

    /// <summary>
    /// The fans the roofs and the rolling stock draw (OBJ-6, TRN-13) as the sprite stages read them, each drawn over in a
    /// look of <see cref="BuildingCatalog.FanLooks"/> as its body is drawn.
    /// </summary>
    public required uint[] Fans { get; init; }

    /// <summary>
    /// The committed lamp sheet, or the one cut for a told fleet (<see cref="FleetList.LampAtlasFile"/>): the
    /// sheet's rows are a catalogue's looks, and the committed one's are the shipped catalogue's.
    /// </summary>
    static string LampSheetFile()
    {
        if (!FleetList.IsTold) return ProjectPaths.LampAtlasFile();

        return FleetList.LampAtlasFile
               ?? throw new InvalidOperationException("A told fleet is drawn with its own lamp sheet: cut it first.");
    }

    public static TownSprites Load(SimConfig config)
    {
        var people = PersonCatalog.Load();
        var cars = CarCatalog.Load();
        var buildings = BuildingCatalog.Load();
        var props = PropCatalog.Load();

        // Every look twice over, walkers and cars alike, because both have two: the one it is and the one
        // it becomes — a body in the road, a wreck. Going down is then a different number in an instance
        // and nothing else.
        var beams = cars.BeamSpritePaths;
        var trains = TrainCatalog.Shared.Vehicles;
        var firstTrain = (people.SheetCount * 2) + (cars.SheetCount * 2) + beams.Length + buildings.Count + props.Count
                         + buildings.FanLooks.Length + HeadSheets + GroundworkSheets;
        var sheets = new SheetSource[firstTrain + trains.Length];
        for (var variant = 0; variant < people.SheetCount; variant++)
        {
            sheets[variant] = SheetSource.File(people.Variants[variant].SheetPath);
            sheets[people.SheetCount + variant] = SheetSource.File(people.Variants[variant].DownSheetPath);
        }

        var firstCar = people.SheetCount * 2;
        var carScales = new Vector2[cars.SheetCount * 2];
        var widestTyreM = config.Tyre.WheelWidthM;
        var widestGlowM = 0f;
        var carReachM = 0f;
        var mostLenses = 0;
        var mostExhausts = 0;
        for (var variant = 0; variant < cars.SheetCount; variant++)
        {
            ref readonly var look = ref cars.Variants[variant];
            var build = CarBuild.Of(config, look);
            widestTyreM = MathF.Max(widestTyreM, build.WheelWidthM);
            carReachM = MathF.Max(carReachM, (look.FootprintM * look.WreckScale).Length());
            mostLenses = Math.Max(mostLenses, cars.LensesOf(variant).Length);
            mostExhausts = Math.Max(mostExhausts, cars.ExhaustsOf(variant).Length);
            foreach (var lens in cars.LensesOf(variant)) widestGlowM = MathF.Max(widestGlowM, LampSprites.GlowM(lens, config));

            sheets[firstCar + variant] = CarSheets.WithTyres(
                look.SpritePath, look.FootprintM, build, out carScales[variant]);
            sheets[firstCar + cars.SheetCount + variant] = CarSheets.WithTyres(
                look.WreckSpritePath, look.FootprintM * look.WreckScale, build, out carScales[cars.SheetCount + variant]);
        }

        var firstBeam = firstCar + (cars.SheetCount * 2);
        for (var beam = 0; beam < beams.Length; beam++) sheets[firstBeam + beam] = SheetSource.File(beams[beam]);

        var firstBuilding = firstBeam + beams.Length;
        for (var variant = 0; variant < buildings.Count; variant++)
        {
            sheets[firstBuilding + variant] = buildings.Variants[variant].SpritePath is { } sprite
                ? SheetSource.File(sprite)
                : PrefabSprites.Plain(buildings.Variants[variant]);
        }

        for (var variant = 0; variant < props.Count; variant++)
        {
            sheets[firstBuilding + buildings.Count + variant] = SheetSource.File(props.Variants[variant].SpritePath);
        }

        var firstFanLook = firstBuilding + buildings.Count + props.Count;
        for (var look = 0; look < buildings.FanLooks.Length; look++) sheets[firstFanLook + look] = SheetSource.File(buildings.FanLooks[look]);

        var heads = ProjectPaths.SignalHeadFiles();
        var firstHead = firstFanLook + buildings.FanLooks.Length;
        for (var strip = 0; strip < HeadSheets; strip++)
        {
            sheets[firstHead + strip] = SheetSource.File(heads[strip]);
        }

        sheets[firstHead + HeadSheets] = MarkSprites.Brush(MarkSprites.RubberEdgeShare);
        sheets[firstHead + HeadSheets + 1] = MarkSprites.Brush(MarkSprites.SoilEdgeShare);
        sheets[firstHead + HeadSheets + 2] = SheetSource.File(LampSheetFile());
        sheets[firstHead + HeadSheets + 3] = LampSprites.Glow(LampSheetFile(), cars, config);
        sheets[firstHead + HeadSheets + 4] = CarSheets.RubberSheet();
        var rail = RailLooks.Shared;
        sheets[firstHead + HeadSheets + 5] = SheetSource.File(rail.Barrier.SpritePath);
        sheets[firstHead + HeadSheets + 6] = SheetSource.File(rail.Drive.SpritePath);
        sheets[firstHead + HeadSheets + 7] = SheetSource.File(rail.Light.SpritePath);
        sheets[firstHead + HeadSheets + 8] = SheetSource.File(rail.Light.LitSpritePath!);
        sheets[firstHead + HeadSheets + 9] = SmokeSprites.Sheet();
        for (var vehicle = 0; vehicle < trains.Length; vehicle++) sheets[firstTrain + vehicle] = TrainSprites.Of(trains[vehicle]);

        return new TownSprites(people, cars, buildings, props, sheets, carScales)
        {
            WidestTyreM = widestTyreM,
            WidestGlowM = widestGlowM,
            CarReachM = carReachM,
            MostLensesOnALook = mostLenses,
            Smoke = SmokeSprites.Look(config),
            Chimney = ChimneySprites.Look(config, buildings),
            MostExhaustsOnALook = mostExhausts,
            Fans = FanTable.Of(buildings, firstBuilding, TrainCatalog.Shared, firstTrain),
        };
    }

    /// <summary>
    /// One frame's aspect for the walk sheets, which are a grid, and the whole picture's for everything
    /// else — a body in the road included, since that is one frame and not a grid. Off the packing alone,
    /// so a probe that draws nothing measures the same quads. And where the fans' looks were packed, into <see cref="Fans"/>.
    /// </summary>
    public void ReadAspects(SheetAtlas atlas)
    {
        var places = atlas.Places;
        for (var slot = 0; slot < FirstDownSheet; slot++)
        {
            Aspects[slot] = (places[slot].WidthPx / PersonCatalog.WalkColumns)
                / (places[slot].HeightPx / PersonCatalog.FacingRows);
        }

        for (var slot = FirstDownSheet; slot < Aspects.Length; slot++) Aspects[slot] = places[slot].WidthPx / places[slot].HeightPx;

        FanTable.PutLooks(Fans, places, FirstFanLookSheet, Buildings.FanLooks.Length);
    }

    /// <summary>
    /// The town's buildings, props and scenery laid out as instances, and its chimneys that smoke, once. A prop is drawn at
    /// its look's own size (GEN-6e), so nothing here reads the sheets' aspects.
    /// </summary>
    public void Lay(CityPlan plan, BuildingUses uses, SimConfig config)
    {
        Standing = StandingSprites.Lay(plan, Buildings, uses, Props, FirstBuildingSheet, FirstPropSheet, config.Grid.Main);
        Scenery = ScenerySprites.Lay(plan.Scenery, plan.WorldSizeM, Props, FirstPropSheet, config.Grid.Main, config.View.SceneryMostDrawn);
        Chimneys = ChimneySprites.Pots(plan, Buildings, uses, config.Chimneys);
    }

    public void Clear()
    {
        Standing = StandingSprites.Nothing;
        Scenery = ScenerySprites.Nothing;
        Chimneys = [];
    }

    /// <summary>
    /// How many instances each run of the town needs at most, which is what the buffers are laid for — none of which
    /// needs the town stood up to be known. Under: the whole ring of marks and as much scenery as a frame draws.
    /// Standing: every building and prop. Over: a body a spawn, a body, two steered tyres, a tow arm and every lens with
    /// its glow a car its people own or its plan stands (<see cref="TownWorld.CarsOfThePlan"/>), and the heads a lit
    /// junction can stand (<see cref="SignalHeads.MostFor"/>). <b>Above</b> (PHY-1a): every car's again, since any of
    /// them may be on a bridge, and every wagon's again where a track is on one — and none in a town with nothing above
    /// its ground.
    /// </summary>
    public SpriteRoom RoomFor(CityPlan plan, SimConfig config)
    {
        var carSprites = TownWorld.CarsOfThePlan(plan) * CarSpritesEach;
        var wagons = Trains.WagonsFor(RailGraph.Of(plan.Rails, plan.WorldSizeM), config.Train);
        return new SpriteRoom(
            config.Marks.Capacity + ScenerySprites.CapacityFor(plan, config.View.SceneryMostDrawn),
            StandingSprites.CapacityFor(plan),
            plan.Spawns.Count + carSprites + SignalHeads.MostFor(plan) + wagons + CrossingSprites.MostFor(plan, config),
            (Array.Exists(plan.Roads.Level, static level => level != CityPlan.RoadArrays.Ground) ? carSprites : 0)
            + (Array.Exists(plan.Rails.Level, static level => level != CityGen.Map.TownMap.RailArrays.AtGrade) ? wagons : 0),
            Math.Max(1, config.Marks.Capacity),
            Math.Max(1, plan.Scenery.Count),
            Math.Max(1, ScenerySprites.RowsFor(plan.WorldSizeM, config.Grid.Main)));
    }

    /// <summary>
    /// A body with its rear tyres painted on (<see cref="CarSheets"/>), the two it steers, a tow arm, and every lens with
    /// its glow — <b>the catalogue's most lenses on one look</b>, and not every lens any look may carry
    /// (<see cref="CarLamps.MostLenses"/>), which over a city's fleet is half the room again for lenses nothing draws.
    /// The smoke's room is the device's emit's own (<see cref="PuffsEach"/>): no fill draws a puff.
    /// </summary>
    int CarSpritesEach => 1 + TyreModel.SteeredWheels + 1 + (2 * MostLensesOnALook);

    /// <summary>The most puffs of smoke one car has out at once (CAR-54): its pipes', the catalogue's most on one look.</summary>
    public int PuffsEach => MostExhaustsOnALook * Smoke.Tailpipe.PuffsEach;

    /// <summary>The most lenses any one look of the catalogue carries.</summary>
    public int MostLensesOnALook { get; private init; }

    /// <summary>How the marks are drawn, through the two brushes this list carries (<see cref="MarkSprites.Look"/>).</summary>
    public MarkLook MarkLook => MarkSprites.Look(RubberBrushSheet, SoilBrushSheet);

    /// <summary>
    /// <b>What lies under everything that stands, as instances</b> — the marks, then the scenery, in painter's order: a
    /// skid is on the road, so everything that stands or drives passes over its own, and nothing of the town's comes near
    /// the scenery but its own neighbours. What a page draws them as; the desktop draws both out of their own rows.
    /// </summary>
    public int FillUnder(DriftMarks marks, Vector2 viewCentreM, Vector2 viewSpanM, Span<SpriteInstance> under)
    {
        var underCount = MarkSprites.Fill(marks, RubberBrushSheet, SoilBrushSheet, viewCentreM, viewSpanM, under);
        return underCount + Scenery.Fill(viewCentreM, viewSpanM, under[underCount..]);
    }

    /// <summary>
    /// The town as instances, in the runs of <see cref="SpriteRoom"/>: the stretch of the buildings and props laid on the
    /// device that the view can see, <paramref name="over"/> everything that moves on the ground — all of it drawn under
    /// the bridges over its roads — and <paramref name="above"/> the cars on those bridges, drawn over them (TER-7b).
    /// What lies under it all is the renderer's (<see cref="TownRenderer.FillUnder"/>).
    /// </summary>
    /// <param name="pixelsPerMetre">The frame's scale in interface pixels, which decides what is too small to draw.</param>
    public SpriteCounts Fill(
        TownWorld world, SimConfig config, Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre,
        Span<SpriteInstance> over, Span<SpriteInstance> above)
    {
        var still = FillStill(viewCentreM, viewSpanM);
        var (overCount, aboveCount) = FillMoving(world, config, viewCentreM, viewSpanM, pixelsPerMetre, over, above);
        return still with { Over = overCount, Above = aboveCount };
    }

    /// <summary>
    /// <b>All of <see cref="Fill"/> but what moves</b>: the stretch of the buildings and props the view can see — what a
    /// frame fills where the device emits the rest.
    /// </summary>
    public SpriteCounts FillStill(Vector2 viewCentreM, Vector2 viewSpanM)
    {
        var (standingFirst, standingCount) = Standing.Range(viewCentreM, viewSpanM);
        return new SpriteCounts(0, standingFirst, standingCount, 0, 0);
    }

    /// <summary>
    /// <b>What moves</b>, the two runs <see cref="Fill"/> writes every frame past the marks and the scenery: the walkers,
    /// the cars and the signal heads over the ground, and the cars on the level above. It is what the device emits in
    /// its place (<c>app/render/kernels/sprites.glsl</c>), and is held to.
    /// </summary>
    public (int Over, int Above) FillMoving(
        TownWorld world, SimConfig config, Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre,
        Span<SpriteInstance> over, Span<SpriteInstance> above)
    {
        var leastPartM = config.View.CarPartLeastPx / pixelsPerMetre;

        // Who may be in the frame, off the solver's grids rather than out of the whole town (TownWorld.CarsIn).
        if (_peopleInView.Length < world.People.Capacity) _peopleInView = new int[world.People.Capacity];
        if (_carsInView.Length < world.Cars.Capacity) _carsInView = new int[world.Cars.Capacity];
        var reachM = (viewSpanM * 0.5f) + new Vector2(CarReachM);
        var people = world.PeopleIn(viewCentreM - reachM, viewCentreM + reachM, _peopleInView);
        var cars = world.CarsIn(viewCentreM - reachM, viewCentreM + reachM, _carsInView);

        var overCount = PersonSprites.Fill(
            world.People, people, People, Aspects, FirstDownSheet, viewCentreM, viewSpanM, over);

        // <b>The cars parted by level once</b>, in the order they were listed, so each level's five passes walk its own
        // cars rather than every car eight times over asking each which it is on.
        var (ground, aboveCars) = world.Levelled ? ByLevel(world.Cars, cars) : (0, 0);
        var onTheGround = world.Levelled ? _carsOnTheGround.AsSpan(0, ground) : cars;
        var onTheLevelAbove = _carsAbove.AsSpan(0, aboveCars);
        overCount += FillCars(
            world.Cars, world.Hitches, world.World.View.Wagons, onTheGround, config, world.ElapsedS, viewCentreM, viewSpanM, leastPartM,
            over[overCount..], CarSprites.EveryLevel);

        // The trains over the cars: a car on the crossing a train is on is under it.
        overCount += TrainSprites.Fill(
            world.World.View.Wagons, Rolling, FirstTrainSheet, viewCentreM, viewSpanM, onABridge: false, over[overCount..]);

        // The heads last of all: a signal hangs over the carriageway, so nothing driving under it passes
        // in front of it.
        overCount += SignalSprites.Fill(world, config, FirstHeadSheet, viewCentreM, viewSpanM, over[overCount..]);

        // And the crossings' gear with them: an arm stands up off the road, and an X light hangs over it as a head does.
        overCount += CrossingSprites.Fill(
            world.CrossingGear, world.ElapsedS, config, new CrossingSheets(BarrierSheet, BarrierDriveSheet, CrossingLightSheet, CrossingLitSheet),
            viewCentreM, viewSpanM, over[overCount..]);

        var aboveCount = world.Levelled
            ? FillCars(
                world.Cars, world.Hitches, world.World.View.Wagons, onTheLevelAbove, config, world.ElapsedS, viewCentreM, viewSpanM, leastPartM, above,
                CarSprites.EveryLevel)
            : 0;

        // And the wagons on a bridge over the bridge, as the cars on one are (RAIL-8).
        aboveCount += TrainSprites.Fill(
            world.World.View.Wagons, Rolling, FirstTrainSheet, viewCentreM, viewSpanM, onABridge: true, above[aboveCount..]);
        return (overCount, aboveCount);
    }

    /// <summary>
    /// The cars listed, parted into those on the ground and those on the level above (<see cref="CarFleet.LevelOf"/>),
    /// each in the order listed, into <see cref="_carsOnTheGround"/> and <see cref="_carsAbove"/>; a car on neither is in
    /// neither, as neither run draws it. Answers how many each holds.
    /// </summary>
    (int Ground, int Above) ByLevel(CarFleet fleet, ReadOnlySpan<int> cars)
    {
        if (_carsOnTheGround.Length < cars.Length) _carsOnTheGround = new int[_carsInView.Length];
        if (_carsAbove.Length < cars.Length) _carsAbove = new int[_carsInView.Length];

        var (ground, above) = (0, 0);
        foreach (var car in cars)
        {
            var level = fleet.LevelOf(car);
            if (level == CityPlan.RoadArrays.Ground) _carsOnTheGround[ground++] = car;
            else if (level == CityPlan.RoadArrays.Over) _carsAbove[above++] = car;
        }

        return (ground, above);
    }

    /// <summary>The frame's cars parted by level (<see cref="ByLevel"/>), laid a roster's capacity each the first time.</summary>
    int[] _carsOnTheGround = [];

    int[] _carsAbove = [];

    /// <summary>The cars of one level, every part of each in the order it is seen from above.</summary>
    /// <param name="drawn">The cars the frame may show (<see cref="TownWorld.CarsIn"/>).</param>
    /// <param name="wagons">The rail vehicles as the view shows them, which an arm may be holding.</param>
    /// <param name="leastPartM">The narrowest tyre or glow this frame draws; a pass whose widest is narrower is not walked at all.</param>
    public int FillCars(
        CarFleet cars, TowHitches hitches, ReadOnlySpan<WagonView> wagons, ReadOnlySpan<int> drawn, SimConfig config, float elapsedS,
        Vector2 viewCentreM, Vector2 viewSpanM, float leastPartM, Span<SpriteInstance> into, int level)
    {
        // The exhausts' smoke (CAR-54) is the device's alone (SmokeSprites): this fill draws none, and the device's
        // emit draws it ahead of the steered tyres. The steered tyres before the bodies, so a car's own bodywork is drawn
        // over them and what shows is the rubber standing proud of the arch — which is what a wheel looks like from above.
        var written = WidestTyreM < leastPartM
            ? 0
            : CarSprites.FillFrontTyres(cars, drawn, RubberSheet, leastPartM, viewCentreM, viewSpanM, into, level);

        written += CarSprites.Fill(
            cars, drawn, Cars, FirstCarSheet, CarSheetScales, viewCentreM, viewSpanM, into[written..], level);

        // The arm over both bodies: it stands on the truck's deck and its fork is above the nose of what it
        // is holding, so a tow drawn under either of them is an arm running through a car (EVA-5).
        written += CarSprites.FillBeams(
            cars, drawn, Cars, hitches, wagons, FirstBeamSheet, viewCentreM, viewSpanM, into[written..], level);

        // The lamps over the bodies, because a lamp is a light on the bodywork: drawn under it, a brake
        // lamp is a red smudge on the road behind a car rather than anything the car is showing.
        if (WidestGlowM < leastPartM) return written;

        return written + LampSprites.Fill(
            cars, drawn, Cars, config, LensSheet, LampGlowSheet, elapsedS, viewCentreM, viewSpanM, into[written..], level,
            leastPartM);
    }
}

/// <summary>
/// The cars, as instances for the same pipeline the walkers use: one quad each, turned to the heading
/// the body is actually at.
/// </summary>
/// <remarks>
/// <para>
/// A car's art is one frame with its nose along <c>+x</c>, so there is no cell to pick and no cycle to
/// step: the whole of what the picture shows is the quad's rotation, and that rotation is <em>solver
/// output</em> rather than intent, because a car turns when its tyres turn it.
/// </para>
/// <para>
/// A wreck is the same instance with a different sheet — the variant's own crumpled art, cut from the
/// same tile at the same place on it, at its slightly wider box. The body is kept, so nothing about the
/// quad moves; only which picture is stretched over it changes.
/// </para>
/// <para>
/// <b>Every car is drawn at the footprint it is simulated at</b> — its own build's (CAR-12a), which is the
/// box the solver was handed and the size the picture itself was drawn to. Drawing every variant at the
/// nominal car's size stretches a 3.4 m hatchback over four metres, and — because the art fills its own
/// sheet edge to edge — spreads the bodywork out over the tyres until none of them shows (CAR-12).
/// </para>
/// <para>
/// <b>Every fill walks the cars it is handed, not the fleet</b>: those a frame may show, in the order they are
/// drawn (<see cref="TownWorld.CarsIn"/>). Each is culled to the view again, since a car near the box asked
/// about may still stand outside it.
/// </para>
/// </remarks>
internal static class CarSprites
{
    /// <summary>A level no car is on, asked for as every car of every level — a town with nothing above its ground.</summary>
    public const int EveryLevel = -1;

    /// <summary>Whether a car is one of the level asked for (<see cref="CarFleet.LevelOf"/>).</summary>
    public static bool IsOn(CarFleet cars, int car, int level) => level == EveryLevel || cars.LevelOf(car) == level;

    /// <summary>
    /// The front pair, drawn at the very offsets the impulses act on and turned to the rack's angle. Plain
    /// rubber, one quad a tyre; the rear pair never steers and is painted into the car's own sheet
    /// (<see cref="CarSheets"/>).
    /// </summary>
    /// <remarks>
    /// <b>Both at one angle</b>, which is the picture and not the model: the patches work at their own Ackermann
    /// angles (<see cref="TyreModel.Ackermann"/>), and the difference between them is not worth its trig a car a
    /// frame.
    /// </remarks>
    /// <param name="leastWidthM">The narrowest tyre this frame draws; a car whose tyres are narrower draws none.</param>
    public static int FillFrontTyres(
        CarFleet cars, ReadOnlySpan<int> drawn, int rubberSheet, float leastWidthM, Vector2 viewCentreM,
        Vector2 viewSpanM, Span<SpriteInstance> into, int level = EveryLevel)
    {
        var written = 0;
        var halfView = viewSpanM * 0.5f;

        foreach (var car in drawn)
        {
            if (written + TyreModel.SteeredWheels > into.Length) break;

            ref readonly var build = ref cars.BuildOf(car);
            if (build.WheelWidthM < leastWidthM || !IsOn(cars, car, level)) continue;

            var centreM = cars.PositionM[car];

            // The tyre stands outside the bodywork (CAR-12), so the cull reaches past the box to it.
            var halfSizeM = new Vector2(build.WheelLengthM, build.WheelWidthM) * 0.5f;
            var reachM = (new Vector2(build.LengthM, build.WidthM).Length() * 0.5f) + build.WheelLengthM;
            var offset = centreM - viewCentreM;
            if (MathF.Abs(offset.X) > halfView.X + reachM || MathF.Abs(offset.Y) > halfView.Y + reachM) continue;

            var headingRad = cars.HeadingRad[car];
            var forward = Heading.Unit(headingRad);
            var right = Heading.RightOf(forward);
            var tyreRad = headingRad + cars.Command[car].SteerRad;

            for (var wheel = 0; wheel < TyreModel.SteeredWheels; wheel++)
            {
                var atBody = TyreModel.WheelAtM(build, wheel);
                into[written++] = new SpriteInstance(
                    centreM + (forward * atBody.X) + (right * atBody.Y), halfSizeM, SpriteCell.Whole, SpriteTint.Plain,
                    (uint)rubberSheet, tyreRad);
            }
        }

        return written;
    }

    /// <summary>
    /// <b>The tow arms</b> (EVA-5): one quad a vehicle that carries one, hinged where its own body carries
    /// the hinge and pointing at whatever it has on the fork — straight back along the deck when it has
    /// nothing. It is the one part of a vehicle in this town drawn as a picture of its own, because it is
    /// the one part that moves against the body it is bolted to.
    /// </summary>
    /// <remarks>
    /// <b>It asks the coupling where the fork is rather than knowing</b> (<see cref="TowHitches.ForkAlongM"/>), so the
    /// arm on screen cannot drift from the arm the tow is spent along; the picture's reach and the length
    /// the coupling is held at are one number in one file (<see cref="CarTowBeam.ReachM"/>). What it holds may be a
    /// car or a rail vehicle, read where the view shows it.
    /// </remarks>
    public static int FillBeams(
        CarFleet cars, ReadOnlySpan<int> drawn, CarCatalog catalogue, TowHitches hitches, ReadOnlySpan<WagonView> wagons,
        int firstBeamSheet, Vector2 viewCentreM, Vector2 viewSpanM, Span<SpriteInstance> into, int level = EveryLevel)
    {
        var written = 0;
        var halfView = viewSpanM * 0.5f;

        foreach (var car in drawn)
        {
            if (written >= into.Length) break;

            // A wrecked recovery vehicle wears its own crumpled picture, arm and all, so nothing is drawn
            // over it (CAR-14a's argument said of the whole vehicle rather than of a lens).
            if (!IsOn(cars, car, level) || cars.Broken[car] || catalogue.BeamOf(cars.Variant[car]) is not { } beam) continue;

            var towed = hitches.Load(car);
            var arm = beam.Drawn(towed >= 0);
            var halfSizeM = arm.SizeM * 0.5f;
            var reachM = halfSizeM.Length() + MathF.Abs(arm.HingeAtM);
            var offset = cars.PositionM[car] - viewCentreM;
            if (MathF.Abs(offset.X) > halfView.X + reachM || MathF.Abs(offset.Y) > halfView.Y + reachM) continue;

            var forward = Heading.Unit(cars.HeadingRad[car]);
            var hingeM = cars.PositionM[car] + (forward * beam.PivotM.X) + (Heading.RightOf(forward) * beam.PivotM.Y);

            // The arm points at what it is holding, and along the deck when it is holding nothing. A pair
            // the solver has driven exactly on top of each other has no direction, and stows rather than
            // picking one.
            var alongTheArm = Vector2.Zero;
            if (towed >= 0)
            {
                var (loadM, loadHeadingRad) = hitches.IsACar(towed)
                    ? (cars.PositionM[towed], cars.HeadingRad[towed])
                    : (wagons[towed - hitches.Cars].PositionM, wagons[towed - hitches.Cars].HeadingRad);
                alongTheArm = loadM + (Heading.Unit(loadHeadingRad) * hitches.ForkAlongM(car)) - hingeM;
            }

            var pointing = alongTheArm.LengthSquared() > 1e-6f ? Vector2.Normalize(alongTheArm) : -forward;

            into[written++] = new SpriteInstance(
                hingeM - (pointing * arm.HingeAtM), halfSizeM, SpriteCell.Whole, SpriteTint.Plain,
                (uint)(firstBeamSheet + catalogue.BeamSlotOf(cars.Variant[car], towed >= 0)),
                Trig.Atan2(pointing.Y, pointing.X));
        }

        return written;
    }

    /// <param name="sheetScales">
    /// How much larger than its art's own box each car sheet is drawn, intact then wrecked
    /// (<see cref="TownSprites.CarSheetScales"/>): the margin its tyres are painted into.
    /// </param>
    public static int Fill(
        CarFleet cars, ReadOnlySpan<int> drawn, CarCatalog catalogue, int firstSheet, ReadOnlySpan<Vector2> sheetScales,
        Vector2 viewCentreM, Vector2 viewSpanM, Span<SpriteInstance> into, int level = EveryLevel)
    {
        var sheetCount = catalogue.SheetCount;
        if (sheetCount <= 0) return 0;

        var written = 0;
        var halfView = viewSpanM * 0.5f;

        foreach (var car in drawn)
        {
            if (written >= into.Length) break;
            if (!IsOn(cars, car, level)) continue;

            var variant = cars.Variant[car] % sheetCount;
            var broken = cars.Broken[car];
            var sheet = (broken ? sheetCount : 0) + variant;

            var centreM = cars.PositionM[car];
            ref readonly var build = ref cars.BuildOf(car);
            var artHalfM = new Vector2(build.LengthM, build.WidthM) * 0.5f;
            if (broken) artHalfM *= catalogue.Variants[variant].WreckScale;
            var halfSizeM = artHalfM * sheetScales[sheet];
            var reachM = halfSizeM.Length();
            var offset = centreM - viewCentreM;
            if (MathF.Abs(offset.X) > halfView.X + reachM || MathF.Abs(offset.Y) > halfView.Y + reachM) continue;

            into[written++] = new SpriteInstance(
                centreM, halfSizeM, SpriteCell.Whole, SpriteTint.Plain, (uint)(firstSheet + sheet),
                cars.HeadingRad[car]);
        }

        return written;
    }
}
