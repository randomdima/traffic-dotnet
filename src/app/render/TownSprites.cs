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

    /// <summary>The two brushes a mark is stamped through, the two pictures every lamp is drawn through, a tyre's rubber, a crossing's barrier, its housing and its X light dark and lit, and a puff of smoke.</summary>
    const int GroundworkSheets = 10;

    TownSprites(PersonCatalog people, CarCatalog cars, BuildingCatalog buildings, PropCatalog props, SheetSource[] sheets)
    {
        People = people;
        Cars = cars;
        Buildings = buildings;
        Props = props;
        Sheets = sheets;
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

    /// <summary>The sheets in slot order — every walker's look, then every car's, then the tow arms, the roofs, the prop looks, the two head strips, the two mark brushes, the lamp's lens and glow, and the rubber.</summary>
    public SheetSource[] Sheets { get; }

    /// <summary>The widest tyre any car of the catalogue or the nominal car runs on: a frame that would leave it out leaves every tyre out, unasked.</summary>
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

    PersonSprites.Depth[] _walkerDepths = [];

    int[] _carsInView = [];

    /// <summary>One frame's width over its height, per slot. Only a walker's quad is shaped by it; a car's is its own footprint.</summary>
    public float[] Aspects { get; }

    /// <summary>Where the cars start, which is past the walkers' sheets.</summary>
    public int FirstCarSheet => People.SheetCount;

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

    /// <summary>What a tyre is drawn with (<see cref="CarSheets.RubberSheet"/>).</summary>
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

        // Every car look twice over, because it has two: the one it is and the one it becomes, a wreck. Going
        // down is then a different number in an instance and nothing else. A walker has one, and a body in the
        // road is drawn off it (PersonSprites).
        var beams = cars.BeamSpritePaths;
        var trains = TrainCatalog.Shared.Vehicles;
        var firstTrain = people.SheetCount + (cars.SheetCount * 2) + beams.Length + buildings.Count + props.Count
                         + buildings.FanLooks.Length + HeadSheets + GroundworkSheets;
        var sheets = new SheetSource[firstTrain + trains.Length];
        for (var variant = 0; variant < people.SheetCount; variant++) sheets[variant] = SheetSource.File(people.Variants[variant].SheetPath);

        var firstCar = people.SheetCount;
        var widestTyreM = config.Tyre.WheelWidthM;
        var widestGlowM = 0f;
        var carReachM = 0f;
        var mostLenses = 0;
        var mostExhausts = 0;
        var mostWheels = 0;
        for (var variant = 0; variant < cars.SheetCount; variant++)
        {
            ref readonly var look = ref cars.Variants[variant];
            var build = CarBuild.Of(config, look);
            widestTyreM = MathF.Max(widestTyreM, build.WheelWidthM);
            mostWheels = Math.Max(mostWheels, build.WheelCount);
            carReachM = MathF.Max(carReachM, (look.FootprintM * look.WreckScale).Length());
            mostLenses = Math.Max(mostLenses, cars.LensesOf(variant).Length);
            mostExhausts = Math.Max(mostExhausts, cars.ExhaustsOf(variant).Length);
            foreach (var lens in cars.LensesOf(variant)) widestGlowM = MathF.Max(widestGlowM, LampSprites.GlowM(lens, config));

            sheets[firstCar + variant] = SheetSource.File(look.SpritePath);
            sheets[firstCar + cars.SheetCount + variant] = SheetSource.File(look.WreckSpritePath);
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
            sheets[firstBuilding + buildings.Count + variant] =
                SheetSource.File(props.Variants[variant].SpritePath) with { OutlineArtPx = config.View.PropOutlineArtPx };
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

        return new TownSprites(people, cars, buildings, props, sheets)
        {
            WidestTyreM = widestTyreM,
            WidestGlowM = widestGlowM,
            CarReachM = carReachM,
            MostLensesOnALook = mostLenses,
            MostWheelsOnALook = mostWheels,
            Smoke = SmokeSprites.Look(config),
            Chimney = ChimneySprites.Look(config, buildings),
            MostExhaustsOnALook = mostExhausts,
            Fans = FanTable.Of(buildings, firstBuilding, TrainCatalog.Shared, firstTrain),
        };
    }

    /// <summary>
    /// <b>The sheets as a player's page packs them</b> (MP-9): a roof or a prop <paramref name="plan"/> stands none of not
    /// packed at all, and every other sheet whole. Every slot keeps its number, so nothing that names a sheet changes.
    /// </summary>
    public SheetSource[] SheetsAsShown(CityPlan plan, BuildingUses uses)
    {
        var shown = (SheetSource[])Sheets.Clone();
        var roofs = new bool[Buildings.Count];
        for (var building = 0; building < plan.Buildings.Count; building++) roofs[BuildingRoofs.Of(plan, Buildings, uses, building).Variant] = true;
        for (var variant = 0; variant < Buildings.Count; variant++) shown[FirstBuildingSheet + variant] = shown[FirstBuildingSheet + variant] with { Unused = !roofs[variant] };

        var props = new bool[Props.Count];
        for (var prop = 0; prop < plan.Props.Count; prop++) props[plan.Props.Look[prop]] = true;
        for (var variant = 0; variant < Props.Count; variant++) shown[FirstPropSheet + variant] = shown[FirstPropSheet + variant] with { Unused = !props[variant] };

        return shown;
    }

    /// <summary>
    /// One frame's aspect for the walk sheets, which are a grid, and the whole picture's for everything
    /// else. Off the packing alone, so a probe that draws nothing measures the same quads. And where the fans'
    /// looks were packed, into <see cref="Fans"/>.
    /// </summary>
    public void ReadAspects(SheetAtlas atlas)
    {
        var places = atlas.Places;
        for (var slot = 0; slot < FirstCarSheet; slot++)
        {
            Aspects[slot] = (places[slot].WidthPx / PersonCatalog.WalkColumns)
                / (places[slot].HeightPx / PersonCatalog.FacingRows);
        }

        for (var slot = FirstCarSheet; slot < Aspects.Length; slot++) Aspects[slot] = places[slot].WidthPx / places[slot].HeightPx;

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
        MarksAbove = plan.Roads.AnyAbove;
    }

    public void Clear()
    {
        Standing = StandingSprites.Nothing;
        Scenery = ScenerySprites.Nothing;
        Chimneys = [];
        MarksAbove = false;
    }

    /// <summary>
    /// <b>Whether the town's marks are drawn a second time, over its level above</b> (PHY-1b): only where a road is on one,
    /// since only a car on a deck lays them there — so a town with none goes without a draw that reads its whole ring.
    /// </summary>
    public bool MarksAbove { get; private set; }

    /// <summary>
    /// How many instances each run of the town needs at most, which is what the buffers are laid for — none of which
    /// needs the town stood up to be known. Under: the whole ring of marks and as much scenery as a frame draws.
    /// Standing: every building and prop. Over: a body a spawn, a body, four tyres, a tow arm and every lens with
    /// its glow a car its people own or its plan stands (<see cref="TownWorld.CarsOfThePlan"/>), and the heads a lit
    /// junction can stand (<see cref="SignalHeads.MostFor"/>). <b>Above</b> (PHY-1a): every car's again, since any of
    /// them may be on a bridge, and every wagon's again where a track is on one — and none in a town with nothing above
    /// its ground.
    /// </summary>
    /// <param name="mostMoversSent">
    /// For a player of a served town, the most movers a snapshot carries (MP-1): what moves is never more than that of
    /// any kind, and a town it steps nothing of marks no ground, so neither is laid room for the whole town.
    /// </param>
    public SpriteRoom RoomFor(CityPlan plan, SimConfig config, int? mostMoversSent = null)
    {
        var cars = TownWorld.CarsOfThePlan(plan);
        var walkers = plan.Spawns.Count;
        var wagons = Trains.WagonsFor(RailGraph.Of(plan.Rails, plan.WorldSizeM), config.Train);
        var marks = config.Marks.Capacity;
        if (mostMoversSent is { } most)
        {
            (cars, walkers, wagons, marks) = (Math.Min(cars, most), most, Math.Min(wagons, most), 0);
        }

        var carSprites = cars * CarSpritesEach;
        return new SpriteRoom(
            ScenerySprites.CapacityFor(plan, config.View.SceneryMostDrawn),
            StandingSprites.CapacityFor(plan),
            walkers + carSprites + SignalHeads.MostFor(plan) + wagons + CrossingSprites.MostFor(plan, config),
            (plan.Roads.AnyAbove ? walkers + carSprites : 0)
            + (Array.Exists(plan.Rails.Level, static level => level != CityGen.Map.TownMap.RailArrays.AtGrade) ? wagons : 0),
            Math.Max(1, marks),
            Math.Max(1, plan.Scenery.Count),
            Math.Max(1, ScenerySprites.RowsFor(plan.WorldSizeM, config.Grid.Main)));
    }

    /// <summary>
    /// A body, its tyres (<see cref="CarSprites.FillTyres"/>), a tow arm, and every lens with its glow — <b>the
    /// catalogue's most wheels and lenses on one look</b>, and not every one any look may carry
    /// (<see cref="TyreModel.MostWheels"/>, <see cref="CarLamps.MostLenses"/>), which over a city's fleet is half the room
    /// again for parts nothing draws. The smoke's room is the device's emit's own (<see cref="PuffsEach"/>): no fill draws a
    /// puff.
    /// </summary>
    int CarSpritesEach => 1 + MostWheelsOnALook + 1 + (2 * MostLensesOnALook);

    /// <summary>The most wheels any one look of the catalogue stands on.</summary>
    public int MostWheelsOnALook { get; private init; }

    /// <summary>The most puffs of smoke one car has out at once (CAR-54): its pipes', the catalogue's most on one look.</summary>
    public int PuffsEach => MostExhaustsOnALook * Smoke.Tailpipe.PuffsEach;

    /// <summary>The most lenses any one look of the catalogue carries.</summary>
    public int MostLensesOnALook { get; private init; }

    /// <summary>How the marks are drawn, through the two brushes this list carries (<see cref="MarkSprites.Look"/>).</summary>
    public MarkLook MarkLook => MarkSprites.Look(RubberBrushSheet, SoilBrushSheet);

    /// <summary>
    /// <b>The scenery, as instances</b>: what lies under everything that stands but the marks, which both heads draw out of
    /// their ring (<c>mark.vert</c>). What a page draws it as; the desktop draws it out of its own rows.
    /// </summary>
    public int FillUnder(Vector2 viewCentreM, Vector2 viewSpanM, Span<SpriteInstance> under) =>
        Scenery.Fill(viewCentreM, viewSpanM, under);

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
        // Who may be in the frame, off the solver's grids rather than out of the whole town (TownWorld.CarsIn).
        if (_peopleInView.Length < world.People.Capacity) _peopleInView = new int[world.People.Capacity];
        if (_carsInView.Length < world.Cars.Capacity) _carsInView = new int[world.Cars.Capacity];
        var reachM = (viewSpanM * 0.5f) + new Vector2(CarReachM);
        var wagons = world.World.View.Wagons;
        var ringWords = SpriteRings.Length(world.People.Capacity, world.Cars.Capacity, wagons.Length);
        if (_rings.Length < ringWords) _rings = new uint[ringWords];
        SpriteRings.Mark(world, _rings.AsSpan(0, ringWords));

        return FillMoving(
            new MovingScene
            {
                Cars = world.Cars,
                People = world.People,
                CarsDrawn = world.CarsIn(viewCentreM - reachM, viewCentreM + reachM, _carsInView),
                PeopleDrawn = world.PeopleIn(viewCentreM - reachM, viewCentreM + reachM, _peopleInView),
                Wagons = wagons,
                Hitches = world.Hitches,
                Levelled = world.Levelled,
                ElapsedS = world.ElapsedS,
                Heads = world.Heads,
                Signals = world.Signals,
                CrossingGear = world.CrossingGear,
                Rings = new SpriteRings(_rings, world.People.Capacity, world.Cars.Capacity),
            },
            config, viewCentreM, viewSpanM, pixelsPerMetre, over, above);
    }

    /// <summary>
    /// <b>What moves, from whoever holds it</b> (<see cref="MovingScene"/>): the town's own fleets on a run that steps it,
    /// or a player's copy of its area on a run that is served one (MP-7) — the same pictures from the same fields either way.
    /// </summary>
    public (int Over, int Above) FillMoving(
        in MovingScene scene, SimConfig config, Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre,
        Span<SpriteInstance> over, Span<SpriteInstance> above)
    {
        var leastPartM = config.View.CarPartLeastPx / pixelsPerMetre;
        if (_walkerDepths.Length < scene.People.Capacity) _walkerDepths = new PersonSprites.Depth[scene.People.Capacity];
        var cars = scene.CarsDrawn;
        var wagons = scene.Wagons;
        var rings = scene.Rings;

        // The walkers parted by level as the cars are below (PHY-1b): those on the ground ranked by depth, and the few on
        // the level above in the order listed, as the device lays them (sprites_write.comp).
        var (walkersOnTheGround, walkersAbove) = scene.Levelled ? WalkersByLevel(scene.People, scene.PeopleDrawn) : (0, 0);
        var overCount = PersonSprites.Fill(
            scene.People, scene.Levelled ? _peopleOnTheGround.AsSpan(0, walkersOnTheGround) : scene.PeopleDrawn, People,
            Aspects, config.Person.DrawnStandingBelowMps, viewCentreM, viewSpanM, over, _walkerDepths, rings);

        // <b>The cars parted by level once</b>, in the order they were listed, so each level's five passes walk its own
        // cars rather than every car eight times over asking each which it is on.
        var (ground, aboveCars) = scene.Levelled ? ByLevel(scene.Cars, cars) : (0, 0);
        var onTheGround = scene.Levelled ? _carsOnTheGround.AsSpan(0, ground) : cars;
        var onTheLevelAbove = _carsAbove.AsSpan(0, aboveCars);
        overCount += FillCars(
            scene.Cars, scene.Hitches, wagons, onTheGround, config, scene.ElapsedS, viewCentreM, viewSpanM, leastPartM,
            over[overCount..], CarSprites.EveryLevel, rings);

        // The trains over the cars: a car on the crossing a train is on is under it.
        overCount += TrainSprites.Fill(wagons, Rolling, FirstTrainSheet, viewCentreM, viewSpanM, onABridge: false, over[overCount..], rings);

        // The heads last of all: a signal hangs over the carriageway, so nothing driving under it passes
        // in front of it.
        overCount += SignalSprites.Fill(
            scene.Heads, scene.Signals, scene.ElapsedS, config, FirstHeadSheet, viewCentreM, viewSpanM, over[overCount..]);

        // And the crossings' gear with them: an arm stands up off the road, and an X light hangs over it as a head does.
        overCount += CrossingSprites.Fill(
            scene.CrossingGear, scene.ElapsedS, config, new CrossingSheets(BarrierSheet, BarrierDriveSheet, CrossingLightSheet, CrossingLitSheet),
            viewCentreM, viewSpanM, over[overCount..]);

        var aboveCount = scene.Levelled
            ? PersonSprites.Fill(
                scene.People, _peopleAbove.AsSpan(0, walkersAbove), People, Aspects, config.Person.DrawnStandingBelowMps,
                viewCentreM, viewSpanM, above, _walkerDepths, rings, byDepth: false)
            : 0;
        aboveCount += scene.Levelled
            ? FillCars(
                scene.Cars, scene.Hitches, wagons, onTheLevelAbove, config, scene.ElapsedS, viewCentreM, viewSpanM, leastPartM,
                above[aboveCount..], CarSprites.EveryLevel, rings)
            : 0;

        // And the wagons on a bridge over the bridge, as the cars on one are (RAIL-8).
        aboveCount += TrainSprites.Fill(wagons, Rolling, FirstTrainSheet, viewCentreM, viewSpanM, onABridge: true, above[aboveCount..], rings);
        return (overCount, aboveCount);
    }

    /// <summary>Which units wear a ring (<see cref="SpriteRings"/>), marked afresh each frame; laid the town's length the first time.</summary>
    uint[] _rings = [];

    /// <summary>
    /// The cars listed, parted into those on the ground and those on the level above (<see cref="CarFleet.LevelOf"/>),
    /// each in the order listed, into <see cref="_carsOnTheGround"/> and <see cref="_carsAbove"/>; a car on neither is in
    /// neither, as neither run draws it. Answers how many each holds.
    /// </summary>
    (int Ground, int Above) ByLevel(CarFleet fleet, ReadOnlySpan<int> cars)
    {
        if (_carsOnTheGround.Length < cars.Length) _carsOnTheGround = new int[fleet.Capacity];
        if (_carsAbove.Length < cars.Length) _carsAbove = new int[fleet.Capacity];

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

    /// <summary>The walkers listed, parted by level as <see cref="ByLevel"/> parts the cars (<see cref="PersonFleet.LevelOf"/>).</summary>
    (int Ground, int Above) WalkersByLevel(PersonFleet fleet, ReadOnlySpan<int> people)
    {
        if (_peopleOnTheGround.Length < people.Length) _peopleOnTheGround = new int[fleet.Capacity];
        if (_peopleAbove.Length < people.Length) _peopleAbove = new int[fleet.Capacity];

        var (ground, above) = (0, 0);
        foreach (var person in people)
        {
            var level = fleet.LevelOf(person);
            if (level == CityPlan.RoadArrays.Ground) _peopleOnTheGround[ground++] = person;
            else if (level == CityPlan.RoadArrays.Over) _peopleAbove[above++] = person;
        }

        return (ground, above);
    }

    /// <summary>The frame's walkers parted by level (<see cref="WalkersByLevel"/>).</summary>
    int[] _peopleOnTheGround = [];

    int[] _peopleAbove = [];

    /// <summary>The cars of one level, every part of each in the order it is seen from above.</summary>
    /// <param name="drawn">The cars the frame may show (<see cref="TownWorld.CarsIn"/>).</param>
    /// <param name="wagons">The rail vehicles as the view shows them, which an arm may be holding.</param>
    /// <param name="leastPartM">The narrowest tyre or glow this frame draws; a pass whose widest is narrower is not walked at all.</param>
    public int FillCars(
        CarFleet cars, TowHitches hitches, ReadOnlySpan<WagonView> wagons, ReadOnlySpan<int> drawn, SimConfig config, float elapsedS,
        Vector2 viewCentreM, Vector2 viewSpanM, float leastPartM, Span<SpriteInstance> into, int level, SpriteRings rings = default)
    {
        // The exhausts' smoke (CAR-54) is the device's alone (SmokeSprites): this fill draws none, and the device's
        // emit draws it ahead of the tyres. The tyres before the bodies, so a car's own bodywork is drawn over them and
        // what shows is the rubber standing proud of the arch — which is what a wheel looks like from above.
        var written = WidestTyreM < leastPartM
            ? 0
            : CarSprites.FillTyres(cars, drawn, RubberSheet, leastPartM, viewCentreM, viewSpanM, into, level);

        written += CarSprites.Fill(cars, drawn, Cars, FirstCarSheet, viewCentreM, viewSpanM, into[written..], level, rings);

        // The arm over both bodies: it stands on the truck's deck and its fork is above the nose of what it
        // is holding, so a tow drawn under either of them is an arm running through a car (EVA-5).
        written += CarSprites.FillBeams(
            cars, drawn, Cars, hitches, wagons, FirstBeamSheet, config.View.CarTiltEyeHeightM, viewCentreM, viewSpanM,
            into[written..], level);

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
    /// <b>Where a point of a car's body is drawn</b>, the body tilted on its springs (CAR-56, <see cref="BodyTilt.OnBodyM"/>):
    /// what is bolted to the body — its lamps, its arm — is placed so. Its tyres are not, and every cull is the untilted
    /// body's.
    /// </summary>
    public static Vector2 OnBodyM(CarFleet cars, int car, Vector2 forward, Vector2 atBodyM, float tiltEyeHeightM)
    {
        var drawnM = BodyTilt.OnBodyM(atBodyM, BodyTilt.AsDrawn(cars.TiltRad[car]), tiltEyeHeightM);
        return cars.PositionM[car] + (forward * drawnM.X) + (Heading.RightOf(forward) * drawnM.Y);
    }

    /// <summary>
    /// A car's tyres, drawn at the very offsets the impulses act on, the steered ones turned to the rack's angle. Plain
    /// rubber, one quad a tyre, under the body and never tilted with it (CAR-56): a tyre stands where its patch is.
    /// </summary>
    /// <remarks>
    /// <b>The steered ones at one angle</b>, which is the picture and not the model: the patches work at their own
    /// Ackermann angles (<see cref="TyreModel.Ackermann"/>), and the difference between them is not worth its trig a car
    /// a frame.
    /// </remarks>
    /// <param name="leastWidthM">The narrowest tyre this frame draws; a car whose tyres are narrower draws none.</param>
    public static int FillTyres(
        CarFleet cars, ReadOnlySpan<int> drawn, int rubberSheet, float leastWidthM, Vector2 viewCentreM,
        Vector2 viewSpanM, Span<SpriteInstance> into, int level = EveryLevel)
    {
        var written = 0;
        var halfView = viewSpanM * 0.5f;

        foreach (var car in drawn)
        {
            ref readonly var build = ref cars.BuildOf(car);
            if (written + build.WheelCount > into.Length) break;

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
            var steeredRad = headingRad + cars.Command[car].SteerRad;

            for (var wheel = 0; wheel < build.WheelCount; wheel++)
            {
                ref readonly var laid = ref build.WheelAt(wheel);
                into[written++] = new SpriteInstance(
                    centreM + (forward * laid.AtM.X) + (right * laid.AtM.Y), halfSizeM, SpriteCell.Whole, SpriteTint.Plain,
                    (uint)rubberSheet, laid.IsSteered ? steeredRad : headingRad);
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
    /// <param name="tiltEyeHeightM">How high a body's tilt is seen from, which puts the hinge on it (<see cref="OnBodyM"/>).</param>
    public static int FillBeams(
        CarFleet cars, ReadOnlySpan<int> drawn, CarCatalog catalogue, TowHitches hitches, ReadOnlySpan<WagonView> wagons,
        int firstBeamSheet, float tiltEyeHeightM, Vector2 viewCentreM, Vector2 viewSpanM, Span<SpriteInstance> into,
        int level = EveryLevel)
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
            var hingeM = OnBodyM(cars, car, forward, beam.PivotM, tiltEyeHeightM);

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

    /// <summary>
    /// The bodies, each at its middle and its tilt on its springs as its ID (<see cref="BodyTilt.Id"/>), which the vertex
    /// stage draws the plane of.
    /// </summary>
    public static int Fill(
        CarFleet cars, ReadOnlySpan<int> drawn, CarCatalog catalogue, int firstSheet, Vector2 viewCentreM,
        Vector2 viewSpanM, Span<SpriteInstance> into, int level = EveryLevel, SpriteRings rings = default)
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

            ref readonly var build = ref cars.BuildOf(car);
            var halfSizeM = new Vector2(build.LengthM, build.WidthM) * 0.5f;
            if (broken) halfSizeM *= catalogue.Variants[variant].WreckScale;

            var centreM = cars.PositionM[car];
            var reachM = halfSizeM.Length();
            var offset = centreM - viewCentreM;
            if (MathF.Abs(offset.X) > halfView.X + reachM || MathF.Abs(offset.Y) > halfView.Y + reachM) continue;

            into[written++] = new SpriteInstance(
                centreM, halfSizeM, SpriteCell.Whole, SpriteTint.Plain, (uint)(firstSheet + sheet), cars.HeadingRad[car],
                BodyTilt.Id(cars.TiltRad[car]), rings.OfCar(car));
        }

        return written;
    }
}
