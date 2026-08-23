using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.CityGen;
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
/// <b>One sheet list, one slot per look</b> — walkers, cars, wrecks, roofs, prop looks, in that order.
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

    /// <summary>The tread every wheel in the town is drawn with, and the two brushes a mark is stamped through.</summary>
    const int GroundworkSheets = 3;

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

    /// <summary>The sheets in slot order — every walker's look, then every car's, then the roofs, the prop looks, the two head strips, the tread and the two mark brushes.</summary>
    public SheetSource[] Sheets { get; }

    /// <summary>One frame's width over its height, per slot. Only a walker's quad is shaped by it; a car's is its own footprint.</summary>
    public float[] Aspects { get; }

    /// <summary>Where the roofs start in the sheet list. A building's picture is one image, not a grid.</summary>
    public int FirstBuildingSheet => People.Count + (Cars.Count * 2);

    public int FirstPropSheet => FirstBuildingSheet + Buildings.Count;

    /// <summary>The two head strips, car then pedestrian. They are the town's only sheets that are not a catalogue.</summary>
    public int FirstHeadSheet => FirstPropSheet + Props.Count;

    /// <summary>One pitch of tread, which every wheel in the town lays several times over along its own roll.</summary>
    public int TreadSheet => FirstHeadSheet + HeadSheets;

    /// <summary>The two stamps a mark is drawn through: rubber's, which has no edge, and soil's, which is nearly all edge.</summary>
    public int RubberBrushSheet => TreadSheet + 1;

    public int SoilBrushSheet => RubberBrushSheet + 1;

    public static TownSprites Load()
    {
        var people = PersonCatalog.Load();
        var cars = CarCatalog.Load();
        var buildings = BuildingCatalog.Load();
        var props = PropCatalog.Load();

        // Every walker's look, then every car's — twice over, because a car has two: the one it is and
        // the wreck it becomes. Breaking a car is then a different number in an instance and nothing else.
        var sheets = new SheetSource[
            people.Count + (cars.Count * 2) + buildings.Count + props.Count + HeadSheets + GroundworkSheets];
        for (var variant = 0; variant < people.Count; variant++) sheets[variant] = SheetSource.File(people.Variants[variant].SheetPath);
        for (var variant = 0; variant < cars.Count; variant++)
        {
            sheets[people.Count + variant] = SheetSource.File(cars.Variants[variant].SpritePath);
            sheets[people.Count + cars.Count + variant] = SheetSource.File(cars.Variants[variant].WreckSpritePath);
        }

        var firstBuilding = people.Count + (cars.Count * 2);
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

        return new TownSprites(people, cars, buildings, props, sheets);
    }

    public void ReadAspects(TownRenderer renderer)
    {
        for (var slot = 0; slot < Aspects.Length; slot++)
            Aspects[slot] = renderer.SheetFrameAspect(slot, PersonCatalog.WalkColumns, PersonCatalog.FacingRows);
        for (var slot = FirstBuildingSheet; slot < Aspects.Length; slot++) Aspects[slot] = renderer.SheetAspect(slot);
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
    public void Lay(CityPlan plan) =>
        Standing = StandingSprites.Lay(plan, Buildings, Props, FirstBuildingSheet, FirstPropSheet, Aspects);

    public void Clear() => Standing = StandingSprites.Nothing;

    /// <summary>
    /// How many instances the town needs at most, which is what the instance buffer is laid for. Two
    /// heads a crossing and one a painted bar is the bound on the signals, four tyres a car, and the
    /// whole ring of marks — none of which needs the town stood up to be known.
    /// </summary>
    public static int CapacityFor(CityPlan plan, SimConfig config) =>
        plan.Spawns.Count + (CarsIn(plan) * TyreModel.Wheels) + StandingSprites.CapacityFor(plan)
        + plan.StopLines.Count + (plan.Crosswalks.Count * 2) + config.Marks.Capacity;

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

        written += SignalSprites.Fill(world, config, FirstHeadSheet, viewCentreM, viewSpanM, into[written..]);

        written += PersonSprites.Fill(
            world.People, People, Aspects, viewCentreM, viewSpanM, world.SelectedPerson, into[written..]);

        // The tyres before the bodies, so a car's own bodywork is drawn over them and what shows is the
        // rubber standing proud of the arch — which is what a wheel looks like from above.
        written += CarSprites.FillWheels(
            world.Cars, config, TreadSheet, TreadPitchM(config), viewCentreM, viewSpanM, into[written..]);

        return written + CarSprites.Fill(
            world.Cars, Cars, People.Count, config, viewCentreM, viewSpanM, world.SelectedCar, into[written..]);
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
/// Every car is drawn at the nominal footprint and not at the one its own variant carries. The fleet's
/// per-variant figures — footprint, axles, track, drivetrain — are read (<see cref="CarCatalog"/>) and
/// none is used yet: the body, the wheelbase the wheel is turned about and the track the loads move
/// across are all the nominal car's, so drawing a variant at its own size would be a picture
/// disagreeing with what was simulated.
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
        var halfSizeM = new Vector2(config.Tyre.WheelLengthM, config.Tyre.WheelWidthM) * 0.5f;
        var pitches = pitchM > 0f ? config.Tyre.WheelLengthM / pitchM : 1f;
        var reachM = new Vector2(config.Car.LengthM, config.Car.WidthM).Length() * 0.5f;
        Span<float> steerRad = stackalloc float[TyreModel.Wheels];

        for (var car = 0; car < cars.Count && written + TyreModel.Wheels <= into.Length; car++)
        {
            var centreM = cars.PositionM[car];
            var offset = centreM - viewCentreM;
            if (MathF.Abs(offset.X) > halfView.X + reachM || MathF.Abs(offset.Y) > halfView.Y + reachM) continue;

            var headingRad = cars.HeadingRad[car];
            var forward = new Vector2(MathF.Cos(headingRad), MathF.Sin(headingRad));
            var right = new Vector2(-forward.Y, forward.X);
            TyreModel.Ackermann(config, cars.Command[car].SteerRad, steerRad);

            for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
            {
                var atBody = TyreModel.WheelAtM(config, wheel);
                var phaseM = cars.TreadPhaseM[(car * TyreModel.Wheels) + wheel];
                into[written++] = new SpriteInstance(
                    centreM + (forward * atBody.X) + (right * atBody.Y), halfSizeM,
                    new Vector2(-phaseM / MathF.Max(pitchM, 1e-6f), 0f), new Vector2(pitches, 1f),
                    PersonSprites.Plain, (uint)treadSheet, headingRad + steerRad[wheel]);
            }
        }

        return written;
    }

    public static int Fill(
        CarFleet cars, CarCatalog catalogue, int firstSheet, SimConfig config, Vector2 viewCentreM, Vector2 viewSpanM,
        int selected, Span<SpriteInstance> into)
    {
        var sheetCount = catalogue.Count;
        if (sheetCount <= 0) return 0;

        var written = 0;
        var halfView = viewSpanM * 0.5f;
        var halfSizeM = new Vector2(config.Car.LengthM, config.Car.WidthM) * 0.5f;
        var reachM = halfSizeM.Length();

        for (var car = 0; car < cars.Count && written < into.Length; car++)
        {
            var centreM = cars.PositionM[car];
            var offset = centreM - viewCentreM;
            if (MathF.Abs(offset.X) > halfView.X + reachM || MathF.Abs(offset.Y) > halfView.Y + reachM) continue;

            var variant = cars.Variant[car] % sheetCount;
            var broken = cars.Broken[car];
            into[written++] = new SpriteInstance(
                centreM, broken ? halfSizeM * catalogue.Variants[variant].WreckScale : halfSizeM, Vector2.Zero,
                Vector2.One, car == selected ? PersonSprites.Highlight : PersonSprites.Plain,
                (uint)(firstSheet + (broken ? sheetCount : 0) + variant), cars.HeadingRad[car]);
        }

        return written;
    }
}
