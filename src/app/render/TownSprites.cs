using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Evacuator;
using TrafficSimulation.Agents.Person.Body;
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
/// The sprite pipeline reads a sheet by index out of one descriptor array, so what decides which picture
/// an instance is drawn with is a number in the instance and never a bind, which is what keeps a frame
/// five crossings whatever the town is full of.
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

    /// <summary>The tread every wheel in the town is drawn with, the two brushes a mark is stamped through, and the two pictures every lamp is drawn through.</summary>
    const int GroundworkSheets = 5;

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

    /// <summary>The sheets in slot order — every walker's look and the same again lying down, then every car's, then the tow arms, the roofs, the prop looks, the two head strips, the tread, the two mark brushes, and the lamp's lens and glow.</summary>
    public SheetSource[] Sheets { get; }

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

    /// <summary>One pitch of tread, which every wheel in the town lays several times over along its own roll.</summary>
    public int TreadSheet => FirstHeadSheet + HeadSheets;

    /// <summary>The two stamps a mark is drawn through: rubber's, which has no edge, and soil's, which is nearly all edge.</summary>
    public int RubberBrushSheet => TreadSheet + 1;

    public int SoilBrushSheet => RubberBrushSheet + 1;

    /// <summary>The sheet every lit lamp in the town is drawn from (CAR-14): a row a variant, two columns a lens.</summary>
    public int LensSheet => SoilBrushSheet + 1;

    /// <summary>And the glow around the lit ones.</summary>
    public int LampGlowSheet => LensSheet + 1;

    public static TownSprites Load()
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
        for (var variant = 0; variant < cars.SheetCount; variant++)
        {
            sheets[firstCar + variant] = SheetSource.File(cars.Variants[variant].SpritePath);
            sheets[firstCar + cars.SheetCount + variant] = SheetSource.File(cars.Variants[variant].WreckSpritePath);
        }

        var firstBeam = firstCar + (cars.SheetCount * 2);
        for (var beam = 0; beam < beams.Length; beam++) sheets[firstBeam + beam] = SheetSource.File(beams[beam]);

        var firstBuilding = firstBeam + beams.Length;
        for (var variant = 0; variant < buildings.Count; variant++)
        {
            sheets[firstBuilding + variant] = SheetSource.File(buildings.Variants[variant].SpritePath);
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

        // The tread is the town's one tiling sheet and its one mipped one, for the two reasons
        // SheetSource carries; the brushes are built rather than shipped.
        sheets[firstHead + HeadSheets] = SheetSource.File(ProjectPaths.TreadFile(), repeats: true, mipped: true);
        sheets[firstHead + HeadSheets + 1] = MarkSprites.Brush(MarkSprites.RubberEdgeShare);
        sheets[firstHead + HeadSheets + 2] = MarkSprites.Brush(MarkSprites.SoilEdgeShare);
        sheets[firstHead + HeadSheets + 3] = SheetSource.File(ProjectPaths.LampAtlasFile());
        sheets[firstHead + HeadSheets + 4] = LampSprites.Glow();

        return new TownSprites(people, cars, buildings, props, sheets);
    }

    /// <summary>
    /// One frame's aspect for the walk sheets, which are a grid, and the whole picture's for everything
    /// else — a body in the road included, since that is one frame and not a grid.
    /// </summary>
    public void ReadAspects(TownRenderer renderer)
    {
        for (var slot = 0; slot < FirstDownSheet; slot++)
            Aspects[slot] = renderer.SheetFrameAspect(slot, PersonCatalog.WalkColumns, PersonCatalog.FacingRows);
        for (var slot = FirstDownSheet; slot < Aspects.Length; slot++) Aspects[slot] = renderer.SheetAspect(slot);
    }

    /// <summary>
    /// One pitch of tread as the shipped picture measures it: the image is one pitch laid across the
    /// full width of a tyre, so its aspect <em>is</em> the pitch over the tread's width. It is read back
    /// off the art rather than assumed, and a test holds it to the figure the tread phase is wrapped
    /// into (<see cref="SimConfig.Tyre.TreadPitchM"/>).
    /// </summary>
    public float TreadPitchM(SimConfig config) => config.Tyre.WheelWidthM * Aspects[TreadSheet];

    /// <summary>
    /// The town's buildings and props laid out as instances, once. Wants the aspects, so it is called
    /// after the renderer for this town exists and its sheets have been measured.
    /// </summary>
    public void Lay(CityPlan plan, BuildingUses uses) =>
        Standing = StandingSprites.Lay(plan, Buildings, uses, Props, FirstBuildingSheet, FirstPropSheet, Aspects);

    public void Clear() => Standing = StandingSprites.Nothing;

    /// <summary>
    /// How many instances the town needs at most, which is what the instance buffer is laid for. Two
    /// heads a crossing and one a painted bar is the bound on the signals, four tyres, a tow arm and
    /// every lens with its glow a car, and the whole ring of marks — none of which needs the town stood
    /// up to be known.
    /// </summary>
    public static int CapacityFor(CityPlan plan, SimConfig config) =>
        plan.Spawns.Count + (CarsIn(plan) * (TyreModel.Wheels + CarLamps.Most + 1))
        + StandingSprites.CapacityFor(plan) + plan.StopLines.Count + (plan.Crosswalks.Count * 2)
        + config.Marks.Capacity;

    static int CarsIn(CityPlan plan)
    {
        var cars = 0;
        foreach (var kind in plan.Spawns.Kind)
        {
            if (kind == SpawnKindCar) cars++;
        }

        return cars;
    }

    /// <summary>The plan's own code for a car, which is the town format's and not this file's to choose.</summary>
    const byte SpawnKindCar = 1;

    public int Fill(
        TownWorld world, SimConfig config, Vector2 viewCentreM, Vector2 viewSpanM, Span<SpriteInstance> into)
    {
        // Painter's order, and the marks are under all of it: a skid is on the road, so everything that
        // stands or drives passes over its own.
        var written = MarkSprites.Fill(world.Marks, RubberBrushSheet, SoilBrushSheet, viewCentreM, viewSpanM, into);

        written += Standing.Fill(viewCentreM, viewSpanM, into[written..]);

        written += PersonSprites.Fill(
            world.People, People, Aspects, FirstDownSheet, viewCentreM, viewSpanM, into[written..]);

        // The tyres before the bodies, so a car's own bodywork is drawn over them and what shows is the
        // rubber standing proud of the arch — which is what a wheel looks like from above.
        written += CarSprites.FillWheels(
            world.Cars, config, TreadSheet, TreadPitchM(config), viewCentreM, viewSpanM, into[written..]);

        written += CarSprites.Fill(world.Cars, Cars, FirstCarSheet, viewCentreM, viewSpanM, into[written..]);

        // The arm over both bodies: it stands on the truck's deck and its fork is above the nose of what it
        // is holding, so a tow drawn under either of them is an arm running through a car (EVA-5).
        written += CarSprites.FillBeams(
            world.Cars, Cars, world.Recovery, FirstBeamSheet, viewCentreM, viewSpanM, into[written..]);

        // The lamps over the bodies, because a lamp is a light on the bodywork: drawn under it, a brake
        // lamp is a red smudge on the road behind a car rather than anything the car is showing.
        written += LampSprites.Fill(
            world.Cars, Cars, config, LensSheet, LampGlowSheet, world.ElapsedS, world.HandDriven,
            viewCentreM, viewSpanM, into[written..]);

        // The heads last of all: a signal hangs over the carriageway, so nothing driving under it passes
        // in front of it.
        return written + SignalSprites.Fill(
            world, config, FirstHeadSheet, viewCentreM, viewSpanM, into[written..]);
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
/// </remarks>
internal static class CarSprites
{
    /// <summary>
    /// The four tyres, drawn at the very offsets the impulses act on, each turned to the angle its own
    /// rubber is working at — the front pair at their own Ackermann angles, which through a tight turn
    /// visibly differ.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A tyre is one quad of a tiling tread, and rolling it is where the slice is taken from</b>: the
    /// image is one pitch of a photographed tyre laid across the full width of the tread, so a wheel's
    /// own length is several pitches of it and the phase is an offset into the texture rather than a
    /// picture that moves. The wrap is the texture's, so a block leaving one end of the tyre re-enters
    /// at the other.
    /// </para>
    /// <para>
    /// <b>Four phases and not one</b>, because the four wheels genuinely turn at four different rates
    /// and the tread is the only thing on screen that says so: a driven pair lights up while the
    /// undriven pair is dragged along, the inside of a turn covers less ground than the outside, and a
    /// wheel that has dropped onto grass locks or spins on its own.
    /// </para>
    /// </remarks>
    public static int FillWheels(
        CarFleet cars, SimConfig config, int treadSheet, float pitchM, Vector2 viewCentreM, Vector2 viewSpanM,
        Span<SpriteInstance> into)
    {
        var written = 0;
        var halfView = viewSpanM * 0.5f;
        Span<float> steerRad = stackalloc float[TyreModel.Wheels];

        for (var car = 0; car < cars.Count && written + TyreModel.Wheels <= into.Length; car++)
        {
            var centreM = cars.PositionM[car];
            ref readonly var build = ref cars.BuildOf(car);

            // This car's own tyre, which stands outside the bodywork (CAR-12), so the cull reaches past
            // the box to it.
            var halfSizeM = new Vector2(build.WheelLengthM, build.WheelWidthM) * 0.5f;
            var pitches = pitchM > 0f ? build.WheelLengthM / pitchM : 1f;
            var reachM = new Vector2(build.LengthM, build.WidthM).Length() * 0.5f + build.WheelLengthM;
            var offset = centreM - viewCentreM;
            if (MathF.Abs(offset.X) > halfView.X + reachM || MathF.Abs(offset.Y) > halfView.Y + reachM) continue;

            var headingRad = cars.HeadingRad[car];
            var forward = new Vector2(MathF.Cos(headingRad), MathF.Sin(headingRad));
            var right = new Vector2(-forward.Y, forward.X);
            TyreModel.Ackermann(build, cars.Command[car].SteerRad, steerRad);

            for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
            {
                var atBody = TyreModel.WheelAtM(build, wheel);
                var phaseM = cars.TreadPhaseM[(car * TyreModel.Wheels) + wheel];
                into[written++] = new SpriteInstance(
                    centreM + (forward * atBody.X) + (right * atBody.Y), halfSizeM,
                    new Vector2(-phaseM / MathF.Max(pitchM, 1e-6f), 0f), new Vector2(pitches, 1f),
                    PersonSprites.Plain, (uint)treadSheet, headingRad + steerRad[wheel]);
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
        CarFleet cars, CarCatalog catalogue, RecoveryDuty recovery, int firstBeamSheet, Vector2 viewCentreM,
        Vector2 viewSpanM, Span<SpriteInstance> into)
    {
        var written = 0;
        var halfView = viewSpanM * 0.5f;

        for (var car = 0; car < cars.Count && written < into.Length; car++)
        {
            // A wrecked recovery vehicle wears its own crumpled picture, arm and all, so nothing is drawn
            // over it (CAR-14a's argument said of the whole vehicle rather than of a lens).
            if (cars.Broken[car] || catalogue.BeamOf(cars.Variant[car]) is not { } beam) continue;

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

    public static int Fill(
        CarFleet cars, CarCatalog catalogue, int firstSheet, Vector2 viewCentreM, Vector2 viewSpanM,
        Span<SpriteInstance> into)
    {
        var sheetCount = catalogue.SheetCount;
        if (sheetCount <= 0) return 0;

        var written = 0;
        var halfView = viewSpanM * 0.5f;

        for (var car = 0; car < cars.Count && written < into.Length; car++)
        {
            var centreM = cars.PositionM[car];
            ref readonly var build = ref cars.BuildOf(car);
            var halfSizeM = new Vector2(build.LengthM, build.WidthM) * 0.5f;
            var reachM = halfSizeM.Length();
            var offset = centreM - viewCentreM;
            if (MathF.Abs(offset.X) > halfView.X + reachM || MathF.Abs(offset.Y) > halfView.Y + reachM) continue;

            var variant = cars.Variant[car] % sheetCount;
            var broken = cars.Broken[car];
            into[written++] = new SpriteInstance(
                centreM, broken ? halfSizeM * catalogue.Variants[variant].WreckScale : halfSizeM, Vector2.Zero,
                Vector2.One, PersonSprites.Plain, (uint)(firstSheet + (broken ? sheetCount : 0) + variant),
                cars.HeadingRad[car]);
        }

        return written;
    }
}
