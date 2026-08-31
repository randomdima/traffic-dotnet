using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.Agents.Car.Body;

/// <summary>
/// What a variant is worth against the nominal car, as the four multipliers its file may state: how fast
/// it will go, how hard it pulls, how hard it stops and what it holds through a corner.
/// </summary>
/// <remarks>
/// A variant that states none of them is the nominal car's engine in its own body, which is why every one
/// of them defaults to one rather than to a figure.
/// </remarks>
internal readonly record struct CarHandling(float MaxSpeed, float Acceleration, float Braking)
{
    public static CarHandling Nominal => new(1f, 1f, 1f);

    internal static CarHandling Of(HandlingFile? file) => file is null
        ? Nominal
        : new CarHandling(file.MaxSpeed ?? 1f, file.Acceleration ?? 1f, file.Braking ?? 1f);
}

/// <summary>One of an arm's two pictures (EVA-5) and the quad it is drawn on.</summary>
/// <param name="HingeAtM">Where the hinge sits along the picture's own axis, from the middle of it.</param>
internal readonly record struct CarTowArm(string SpritePath, Vector2 SizeM, float HingeAtM);

/// <summary>
/// <b>The arm a recovery vehicle pulls on</b> (EVA-5): the two pictures it is drawn in, the hinge it turns
/// about on the body, and how far past that hinge the fork reaches when it is out.
/// </summary>
/// <remarks>
/// <b>It is one rigid thing hinged at one point</b>, so the whole of its state is one direction and one
/// picture: drawn in along the body when nothing is on it, and reaching at the car it is holding when
/// something is (EVA-5). <see cref="ReachM"/> is therefore both what the coupling is held at and where the
/// <see cref="Extended"/> fork is drawn, which is the point of measuring it off the picture.
/// </remarks>
internal readonly record struct CarTowBeam(CarTowArm Extended, CarTowArm Collapsed, float ReachM, Vector2 PivotM)
{
    /// <summary>Which of the two a vehicle draws, which is only ever whether it has something on the fork.</summary>
    public CarTowArm Drawn(bool towing) => towing ? Extended : Collapsed;
}

/// <summary>
/// One car in the fleet: what it looks like, what it measures, and which wheels it drives through.
/// </summary>
/// <remarks>
/// Unlike a walker's look, a car's variant carries figures the model reads — the footprint it is drawn
/// and collided at, where its axles are, how wide its track is, and which end the drive is placed on.
/// The nominal car the junctions are sized against is nobody's actual car; these are the actual ones.
/// </remarks>
/// <param name="HalfTrackM">
/// How far a wheel stands off the car's own middle, which is half the <c>trackM</c> its file states.
/// </param>
/// <param name="CollisionSizeM">
/// <b>What this car is collided as</b> (CAR-12b): the outermost reach of the rounded box fitted inside its
/// picture, rounding included. Smaller than <paramref name="FootprintM"/>, because a footprint is the
/// rectangle a picture was drawn in and a car does not fill its corners — nor, on a police car, the width
/// its mirrors set.
/// </param>
/// <param name="CornerRadiusM">And how much of that shape's corners is rounded off.</param>
/// <param name="MaxSteeringDeg">
/// How far its front wheels turn at the stop, or nothing where its file names none and it gets the nominal
/// car's lock.
/// </param>
/// <param name="TyreFriction">What its rubber holds across the roll, or nothing for the nominal tyre.</param>
/// <param name="CgHeightM">And how high it carries its weight, or nothing for the nominal car's.</param>
/// <param name="FrontWeightShare">And how much of it stands on the front axle, or nothing for an even split.</param>
/// <param name="WheelM">
/// The size of one of its tyres, along the roll and across it, or nothing for the nominal tyre. It is what
/// the wheel is drawn at and how wide a mark it leaves.
/// </param>
/// <param name="WheelRotatingMassKg">And what one of them behaves like as a straight-line mass (J/r²).</param>
internal readonly record struct CarVariant(
    string Id, string SpritePath, string WreckSpritePath, Vector2 WreckScale, CarTowBeam? Beam, Vector2 FootprintM,
    Vector2 CollisionSizeM, float CornerRadiusM, float MassKg, float FrontAxleM, float RearAxleM, float HalfTrackM,
    int Drivetrain, float? MaxSteeringDeg, float? TyreFriction, float? CgHeightM, float? FrontWeightShare,
    Vector2? WheelM, float? WheelRotatingMassKg,
    bool Unbreakable, CarHandling Handling, CarLens[] Lenses)
{
    public float WheelbaseM => FrontAxleM - RearAxleM;

    /// <summary>How far the drive is placed on the front axle, in the terms the tyre model spends it in.</summary>
    /// <remarks>
    /// The shipped fleet writes 0 for rear, 1 for front and 2 for all four; nothing else appears in it.
    /// <b>All four is the load's own split and never an even one.</b> A differential handing half the torque
    /// to an axle carrying a third of the car would break that axle away at a third of the grip the other
    /// three wheels still had, so an even split makes a car driving every wheel worse the further its
    /// balance is from the middle — which is backwards. Placed by load, a car that drives all four puts down
    /// the whole of what it stands on whatever its balance, which is the thing driving all four is for.
    /// </remarks>
    public float DrivenFrontShare(float frontWeightShare) =>
        Drivetrain switch { 0 => 0f, 2 => frontWeightShare, _ => 1f };
}

/// <summary>
/// The fleet, read from <c>assets/…/Fleet.json</c>, and the service vehicles beside it from
/// <c>Service.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two lists and one array.</b> A town's traffic is drawn from the fleet by wrapping an index
/// (<see cref="Count"/>); a service vehicle is stood on purpose and names its own variant by id
/// (<see cref="Ambulance"/>, <see cref="Police"/>, <see cref="Evacuator"/>). One array because every look
/// is a sheet slot, and two lists because a picture nobody may be handed by accident has to be somewhere
/// the wrap cannot reach.
/// </para>
/// <para>
/// A car's sheet is one frame, nose along +x, which is why nothing here has the rows and columns
/// <see cref="Person.Body.PersonCatalog"/> needs: a car's heading is shown by turning the quad, because
/// the body itself turns and the art is drawn once.
/// </para>
/// <para>
/// A variant names a <see cref="WreckFile"/> being the wreck it becomes, cut from the same tile, so
/// breaking a car swaps one texture and moves nothing. A variant PHY-4b says cannot break names none and
/// is its own wreck sheet, because that sheet is never reached.
/// </para>
/// </remarks>
internal sealed class CarCatalog
{
    CarCatalog(CarVariant[] variants, int fleet)
    {
        Variants = variants;
        Count = fleet;
        Ambulance = IndexOf("ambulance_white");
        Police = IndexOf("police_white");
        Evacuator = IndexOf("evacuator_yellow");
        Plain = InTheFleet("sedan_silver");
        Armoured = InTheFleet("apc_olive");
        Sports = InTheFleet("sports_red");

        // The beams get a run of sheet slots of their own, because they are a picture only a look or two in
        // the catalogue has: a slot per look would lay an empty sheet for every car in the town.
        _beamSlot = new int[variants.Length];
        var beams = new List<string>();
        for (var entry = 0; entry < variants.Length; entry++)
        {
            if (variants[entry].Beam is not { } beam)
            {
                _beamSlot[entry] = NoBeam;
                continue;
            }

            _beamSlot[entry] = beams.Count;
            beams.Add(beam.Collapsed.SpritePath);
            beams.Add(beam.Extended.SpritePath);
        }

        BeamSpritePaths = [.. beams];

        int IndexOf(string id)
        {
            for (var entry = fleet; entry < variants.Length; entry++)
            {
                if (variants[entry].Id == id) return entry;
            }

            throw new InvalidDataException($"Service.json names no variant with the id '{id}'.");
        }

        int InTheFleet(string id)
        {
            for (var entry = 0; entry < fleet; entry++)
            {
                if (variants[entry].Id == id) return entry;
            }

            throw new InvalidDataException($"Fleet.json names no variant with the id '{id}'.");
        }
    }

    /// <summary>The fleet first, then the service vehicles — one array, because a sheet slot is a sheet slot.</summary>
    public CarVariant[] Variants { get; }

    /// <summary>
    /// <b>How many looks a town's traffic is drawn from</b>, which is the fleet and not everything here
    /// (AMB-3). A service vehicle is a car somebody stood on purpose; handing its picture out to the
    /// seventeenth ordinary car would put an ambulance on the school run.
    /// </summary>
    public int Count { get; }

    /// <summary>And how many looks there are altogether, which is what the sheet list is laid for.</summary>
    public int SheetCount => Variants.Length;

    /// <summary>
    /// The tow beams' own pictures, in slot order — a short run beside the sheet list rather than one slot
    /// a look, because a beam is a picture almost nothing in the catalogue has.
    /// </summary>
    public string[] BeamSpritePaths { get; }

    readonly int[] _beamSlot;

    /// <summary>
    /// Which of <see cref="BeamSpritePaths"/> this variant draws with the fork empty or loaded, or
    /// <see cref="NoBeam"/>, on the same wrap. An arm lays its two pictures side by side.
    /// </summary>
    public int BeamSlotOf(int variant, bool towing)
    {
        var slot = _beamSlot[Wrapped(variant)];
        return slot == NoBeam ? NoBeam : slot + (towing ? 1 : 0);
    }

    /// <summary>And the beam itself, or null for a look that tows nothing.</summary>
    public CarTowBeam? BeamOf(int variant) => At(variant).Beam;

    public const int NoBeam = -1;

    /// <summary>
    /// Whether this variant is a service vehicle's rather than one of the fleet's (SRV-3), which is the
    /// whole of the difference between the two lists: the wrap cannot reach past <see cref="Count"/>, so
    /// a variant at or over it was named on purpose by whoever stood the car.
    /// </summary>
    public bool IsService(int variant) => variant >= Count;

    /// <summary>Which variant an ambulance wears (AMB-3).</summary>
    public int Ambulance { get; }

    /// <summary>Which one a police car wears, and which one the evacuator does (SRV-2).</summary>
    public int Police { get; }

    public int Evacuator { get; }

    /// <summary>
    /// <b>The one look a map that stands a single make of car dresses its whole fleet in</b>, where what
    /// the map wants is for its cars to differ in nothing at all.
    /// </summary>
    public int Plain { get; }

    /// <summary>
    /// And the heaviest look the fleet ships, for a map that wants one car plainly worth escorting.
    /// <b>Named rather than drawn</b>, like every other look a map asks for by what it is.
    /// </summary>
    public int Armoured { get; }

    /// <summary>And the quickest, for a map that wants one car plainly worth watching go past.</summary>
    public int Sports { get; }

    /// <summary>
    /// The fleet, read once. <b>It is data and not a service</b> — immutable, on disk, and the same for
    /// every town — and a town is stood up often enough that reading a file per variant each time would be
    /// a cost paid by every unit test that builds one.
    /// </summary>
    public static CarCatalog Shared { get; } = Load();

    /// <summary>
    /// Whether the variant at this index is built not to break (PHY-4b). <b>The index wraps the way the
    /// sheets wrap</b> (see <c>CarSprites.Fill</c>), over every look and not only the fleet.
    /// </summary>
    public bool UnbreakableOf(int variant) => At(variant).Unbreakable;

    /// <summary>The lenses the variant at this index draws (CAR-14a), on the same wrap.</summary>
    public ReadOnlySpan<CarLens> LensesOf(int variant) => At(variant).Lenses;

    CarVariant At(int variant) => Variants[Wrapped(variant)];

    int Wrapped(int variant) => (variant % SheetCount) + (variant < 0 ? SheetCount : 0);

    public static CarCatalog Load()
    {
        var fleet = AssetJson.Catalog(VariantList("Fleet.json"));
        var service = AssetJson.Catalog(VariantList("Service.json"));

        var variants = new CarVariant[fleet.Length + service.Length];
        for (var entry = 0; entry < fleet.Length; entry++) variants[entry] = ReadVariant(fleet[entry]);
        for (var entry = 0; entry < service.Length; entry++) variants[fleet.Length + entry] = ReadVariant(service[entry]);

        return new CarCatalog(variants, fleet.Length);
    }

    static string VariantList(string file) =>
        Path.Combine(ProjectPaths.Assets, "agents", "car", "variants", "common", file);

    static CarVariant ReadVariant(string path)
    {
        var variant = AssetJson.Read(path, CarVariantJson.Default.CarVariantFile);
        var sprite = AssetJson.Beside(path, variant.Sprite);

        return new CarVariant(
            variant.Id, sprite,
            variant.Wreck is { } wreck ? AssetJson.Beside(path, wreck.Sprite) : sprite,
            variant.Wreck?.Scale ?? Vector2.One,
            variant.Beam is { } beam
                ? new CarTowBeam(Arm(beam.Extended, path), Arm(beam.Collapsed, path), beam.ReachM, beam.PivotM)
                : null,
            variant.FootprintM, variant.CollisionM?.SizeM ?? variant.FootprintM,
            variant.CollisionM?.CornerRadiusM ?? 0f, variant.MassKg, variant.FrontAxleM,
            variant.RearAxleM, variant.TrackM * 0.5f, variant.Drivetrain, variant.MaxSteeringDeg,
            variant.TyreFriction, variant.CgHeightM, variant.FrontWeightShare,
            variant.WheelM, variant.WheelRotatingMassKg, variant.Unbreakable,
            CarHandling.Of(variant.Handling), Lenses(variant, path, sprite));
    }

    static CarTowArm Arm(TowArmFile arm, string path) =>
        new(AssetJson.Beside(path, arm.Sprite), arm.SizeM, arm.HingeAtM);

    /// <param name="sprite">
    /// The picture the lenses are measured off, which is read for its size: a lens centre is snapped to
    /// that picture's texel grid (<see cref="CarLens"/>).
    /// </param>
    static CarLens[] Lenses(CarVariantFile variant, string path, string sprite)
    {
        if (variant.Lamps.Length > CarLamps.MostLenses)
        {
            throw new InvalidDataException(
                $"{path}: draws {variant.Lamps.Length} lenses, and a car may draw {CarLamps.MostLenses}.");
        }

        var artPx = ImageHeader.Measure(sprite);
        var perM = new Vector2(artPx.WidthPx / variant.FootprintM.X, artPx.HeightPx / variant.FootprintM.Y);
        var halfM = variant.FootprintM * 0.5f;
        var lenses = new CarLens[variant.Lamps.Length];
        for (var lamp = 0; lamp < lenses.Length; lamp++)
        {
            var file = variant.Lamps[lamp];
            var fitting = file.Fitting switch
            {
                "rear" => CarLampFitting.Rear,
                "indicator" => CarLampFitting.Indicator,
                "beaconRed" => CarLampFitting.BeaconRed,
                "beaconBlue" => CarLampFitting.BeaconBlue,
                "beaconAmber" => CarLampFitting.BeaconAmber,
                _ => throw new InvalidDataException($"{path}: names no lamp fitting '{file.Fitting}'."),
            };

            lenses[lamp] = new CarLens(fitting, OnTheTexelGrid(file.AtM, halfM, perM), file.SizeM);
        }

        return lenses;
    }

    /// <summary>
    /// A lens centre moved to the nearest corner of the art's own texel grid — half a texel at most,
    /// which is 5 mm of car. <b>It is what lets the cut lamp land on the texels of the dull one</b>: the
    /// cut is whole texels of the sprite and the cell it is pasted into is a whole-texel grid, so a
    /// centre that falls half a texel inside one leaves the lit lamp a half-texel step off the lens it
    /// was cut from — the one artefact a lamp cannot have, since the dull lens is on screen beside it.
    /// </summary>
    static Vector2 OnTheTexelGrid(Vector2 atBodyM, Vector2 halfM, Vector2 perM)
    {
        var centrePx = (halfM + atBodyM) * perM;
        return new Vector2(MathF.Round(centrePx.X), MathF.Round(centrePx.Y)) / perM - halfM;
    }
}
