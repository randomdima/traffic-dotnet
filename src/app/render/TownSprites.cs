using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Evacuator;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.TrafficLight.Body;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Runtime;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Statics;
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

    /// <summary>The two brushes a mark is stamped through, the two pictures every lamp is drawn through, and a steered tyre's rubber.</summary>
    const int GroundworkSheets = 5;

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

    /// <summary>The two head strips, car then pedestrian. They are the town's only sheets that are not a catalogue.</summary>
    public int FirstHeadSheet => FirstPropSheet + Props.Count;

    /// <summary>The two stamps a mark is drawn through: rubber's, which has no edge, and soil's, which is nearly all edge.</summary>
    public int RubberBrushSheet => FirstHeadSheet + HeadSheets;

    public int SoilBrushSheet => RubberBrushSheet + 1;

    /// <summary>The sheet every lit lamp in the town is drawn from (CAR-14): a row a variant, two columns a lens.</summary>
    public int LensSheet => SoilBrushSheet + 1;

    /// <summary>And the glow around the lit ones.</summary>
    public int LampGlowSheet => LensSheet + 1;

    /// <summary>What a steered tyre is drawn with (<see cref="CarSheets.RubberSheet"/>).</summary>
    public int RubberSheet => LampGlowSheet + 1;

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
        var sheets = new SheetSource[
            (people.SheetCount * 2) + (cars.SheetCount * 2) + beams.Length + buildings.Count + props.Count
            + HeadSheets + GroundworkSheets];
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
        for (var variant = 0; variant < cars.SheetCount; variant++)
        {
            ref readonly var look = ref cars.Variants[variant];
            var build = CarBuild.Of(config, look);
            widestTyreM = MathF.Max(widestTyreM, build.WheelWidthM);
            carReachM = MathF.Max(carReachM, (look.FootprintM * look.WreckScale).Length());
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

        var heads = ProjectPaths.SignalHeadFiles();
        var firstHead = firstBuilding + buildings.Count + props.Count;
        for (var strip = 0; strip < HeadSheets; strip++)
        {
            sheets[firstHead + strip] = SheetSource.File(heads[strip]);
        }

        sheets[firstHead + HeadSheets] = MarkSprites.Brush(MarkSprites.RubberEdgeShare);
        sheets[firstHead + HeadSheets + 1] = MarkSprites.Brush(MarkSprites.SoilEdgeShare);
        sheets[firstHead + HeadSheets + 2] = SheetSource.File(ProjectPaths.LampAtlasFile());
        sheets[firstHead + HeadSheets + 3] = LampSprites.Glow();
        sheets[firstHead + HeadSheets + 4] = CarSheets.RubberSheet();

        return new TownSprites(people, cars, buildings, props, sheets, carScales)
        {
            WidestTyreM = widestTyreM,
            WidestGlowM = widestGlowM,
            CarReachM = carReachM,
        };
    }

    /// <summary>
    /// One frame's aspect for the walk sheets, which are a grid, and the whole picture's for everything
    /// else — a body in the road included, since that is one frame and not a grid. Off the packing alone,
    /// so a probe that draws nothing measures the same quads.
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
    }

    /// <summary>
    /// The town's buildings, props and scenery laid out as instances, once. Wants the aspects, so it is called
    /// after the renderer for this town exists and its sheets have been measured.
    /// </summary>
    public void Lay(CityPlan plan, BuildingUses uses, SimConfig config)
    {
        Standing = StandingSprites.Lay(
            plan, Buildings, uses, Props, FirstBuildingSheet, FirstPropSheet, Aspects, config.Grid.Main);
        Scenery = ScenerySprites.Lay(
            plan.Scenery, plan.WorldSizeM, Props, FirstPropSheet, Aspects, config.Grid.Main, config.View.SceneryMostDrawn);
    }

    public void Clear()
    {
        Standing = StandingSprites.Nothing;
        Scenery = ScenerySprites.Nothing;
    }

    /// <summary>
    /// How many instances each run of the town needs at most, which is what the buffers are laid for — none of which
    /// needs the town stood up to be known. Under: the whole ring of marks and as much scenery as a frame draws.
    /// Standing: every building and prop. Over: a body a spawn, a body, two steered tyres, a tow arm and every lens with
    /// its glow a car its people own or its plan stands (<see cref="TownWorld.CarsOfThePlan"/>), and the heads a lit
    /// junction can stand (<see cref="SignalHeads.MostFor"/>). <b>Above</b> (PHY-1a): every car's again, since any of
    /// them may be on a bridge — and none in a town with nothing above its ground.
    /// </summary>
    public static SpriteRoom RoomFor(CityPlan plan, SimConfig config)
    {
        var carSprites = TownWorld.CarsOfThePlan(plan) * CarSpritesEach;
        return new SpriteRoom(
            config.Marks.Capacity + ScenerySprites.CapacityFor(plan, config.View.SceneryMostDrawn),
            StandingSprites.CapacityFor(plan),
            plan.Spawns.Count + carSprites + SignalHeads.MostFor(plan),
            Array.Exists(plan.Roads.Level, static level => level != CityPlan.RoadArrays.Ground) ? carSprites : 0);
    }

    /// <summary>A body with its rear tyres painted on (<see cref="CarSheets"/>), the two it steers, every lens with its glow, and a tow arm.</summary>
    const int CarSpritesEach = 1 + TyreModel.SteeredWheels + CarLamps.Most + 1;

    /// <summary>
    /// The town as instances, in the runs of <see cref="SpriteRoom"/>: <paramref name="under"/> what lies under
    /// everything that stands, the stretch of the buildings and props laid on the device that the view can see,
    /// <paramref name="over"/> everything that moves on the ground — all of it drawn under the bridges over its
    /// roads — and <paramref name="above"/> the cars on those bridges, drawn over them (TER-7b).
    /// </summary>
    /// <param name="pixelsPerMetre">The frame's scale in interface pixels, which decides what is too small to draw.</param>
    public SpriteCounts Fill(
        TownWorld world, SimConfig config, Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre,
        Span<SpriteInstance> under, Span<SpriteInstance> over, Span<SpriteInstance> above)
    {
        var leastPartM = config.View.CarPartLeastPx / pixelsPerMetre;

        // Painter's order, and the marks are under all of it: a skid is on the road, so everything that
        // stands or drives passes over its own.
        var underCount = MarkSprites.Fill(world.Marks, RubberBrushSheet, SoilBrushSheet, viewCentreM, viewSpanM, under);

        // The scenery under everything that stands: nothing of the town's comes near it but its own neighbours.
        underCount += Scenery.Fill(viewCentreM, viewSpanM, under[underCount..]);
        var (standingFirst, standingCount) = Standing.Range(viewCentreM, viewSpanM);

        // Who may be in the frame, off the solver's grids rather than out of the whole town (TownWorld.CarsIn).
        if (_peopleInView.Length < world.People.Capacity) _peopleInView = new int[world.People.Capacity];
        if (_carsInView.Length < world.Cars.Capacity) _carsInView = new int[world.Cars.Capacity];
        var reachM = (viewSpanM * 0.5f) + new Vector2(CarReachM);
        var people = world.PeopleIn(viewCentreM - reachM, viewCentreM + reachM, _peopleInView);
        var cars = world.CarsIn(viewCentreM - reachM, viewCentreM + reachM, _carsInView);

        var overCount = PersonSprites.Fill(
            world.People, people, People, Aspects, FirstDownSheet, viewCentreM, viewSpanM, over);

        var onTheGround = world.Levelled ? CityPlan.RoadArrays.Ground : CarSprites.EveryLevel;
        overCount += FillCars(world, cars, config, viewCentreM, viewSpanM, leastPartM, over[overCount..], onTheGround);

        // The heads last of all: a signal hangs over the carriageway, so nothing driving under it passes
        // in front of it.
        overCount += SignalSprites.Fill(world, config, FirstHeadSheet, viewCentreM, viewSpanM, over[overCount..]);

        var aboveCount = world.Levelled
            ? FillCars(world, cars, config, viewCentreM, viewSpanM, leastPartM, above, CityPlan.RoadArrays.Over)
            : 0;
        return new SpriteCounts(underCount, standingFirst, standingCount, overCount, aboveCount);
    }

    /// <summary>The cars of one level, every part of each in the order it is seen from above.</summary>
    /// <param name="cars">The cars the frame may show (<see cref="TownWorld.CarsIn"/>).</param>
    /// <param name="leastPartM">The narrowest tyre or glow this frame draws; a pass whose widest is narrower is not walked at all.</param>
    int FillCars(
        TownWorld world, ReadOnlySpan<int> cars, SimConfig config, Vector2 viewCentreM, Vector2 viewSpanM,
        float leastPartM, Span<SpriteInstance> into, int level)
    {
        // The steered tyres before the bodies, so a car's own bodywork is drawn over them and what shows is
        // the rubber standing proud of the arch — which is what a wheel looks like from above.
        var written = WidestTyreM < leastPartM
            ? 0
            : CarSprites.FillFrontTyres(world.Cars, cars, RubberSheet, leastPartM, viewCentreM, viewSpanM, into, level);

        written += CarSprites.Fill(
            world.Cars, cars, Cars, FirstCarSheet, CarSheetScales, viewCentreM, viewSpanM, into[written..], level);

        // The arm over both bodies: it stands on the truck's deck and its fork is above the nose of what it
        // is holding, so a tow drawn under either of them is an arm running through a car (EVA-5).
        written += CarSprites.FillBeams(
            world.Cars, cars, Cars, world.Recovery, FirstBeamSheet, viewCentreM, viewSpanM, into[written..], level);

        // The lamps over the bodies, because a lamp is a light on the bodywork: drawn under it, a brake
        // lamp is a red smudge on the road behind a car rather than anything the car is showing.
        if (WidestGlowM < leastPartM) return written;

        return written + LampSprites.Fill(
            world.Cars, cars, Cars, config, LensSheet, LampGlowSheet, world.ElapsedS, world.HandDriven,
            world.HandDrivenCar, viewCentreM, viewSpanM, into[written..], level, leastPartM);
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
                    centreM + (forward * atBody.X) + (right * atBody.Y), halfSizeM, Vector2.Zero, Vector2.One,
                    PersonSprites.Plain, (uint)rubberSheet, tyreRad);
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
    /// <b>It asks the coupling where the fork is rather than knowing</b> (<see cref="TowBar.ForkM"/>), so the
    /// arm on screen cannot drift from the arm the tow is spent along; the picture's reach and the length
    /// the coupling is held at are one number in one file (<see cref="CarTowBeam.ReachM"/>).
    /// </remarks>
    public static int FillBeams(
        CarFleet cars, ReadOnlySpan<int> drawn, CarCatalog catalogue, RecoveryDuty recovery, int firstBeamSheet,
        Vector2 viewCentreM, Vector2 viewSpanM, Span<SpriteInstance> into, int level = EveryLevel)
    {
        var written = 0;
        var halfView = viewSpanM * 0.5f;

        foreach (var car in drawn)
        {
            if (written >= into.Length) break;

            // A wrecked recovery vehicle wears its own crumpled picture, arm and all, so nothing is drawn
            // over it (CAR-14a's argument said of the whole vehicle rather than of a lens).
            if (!IsOn(cars, car, level) || cars.Broken[car] || catalogue.BeamOf(cars.Variant[car]) is not { } beam) continue;

            var towed = recovery.Towing[car];
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
            var alongTheArm = towed >= 0
                ? TowBar.ForkM(
                    cars.BuildOf(towed), cars.PositionM[towed], Heading.Unit(cars.HeadingRad[towed]),
                    recovery.HeldByTheTail[towed]) - hingeM
                : Vector2.Zero;
            var pointing = alongTheArm.LengthSquared() > 1e-6f ? Vector2.Normalize(alongTheArm) : -forward;

            into[written++] = new SpriteInstance(
                hingeM - (pointing * arm.HingeAtM), halfSizeM, Vector2.Zero, Vector2.One, PersonSprites.Plain,
                (uint)(firstBeamSheet + catalogue.BeamSlotOf(cars.Variant[car], towed >= 0)),
                MathF.Atan2(pointing.Y, pointing.X));
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
                centreM, halfSizeM, Vector2.Zero, Vector2.One, PersonSprites.Plain, (uint)(firstSheet + sheet),
                cars.HeadingRad[car]);
        }

        return written;
    }
}
