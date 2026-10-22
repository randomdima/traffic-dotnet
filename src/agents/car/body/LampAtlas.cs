using System.Numerics;

namespace TrafficSimulation.Agents.Car.Body;

/// <summary>
/// Where a lit lamp's picture is in the town's one lamp sheet, and what colour each lamp burns.
/// <b>A lit lamp is the car's own texels driven emissive</b> (CAR-14a) — those its zone map paints as the lens, cut
/// from the variant's sprite at its own resolution by <see cref="LampAtlasBake"/> — so the layout the sheet is indexed by
/// and the layout it is cut by are this one file.
/// </summary>
/// <remarks>
/// <para>
/// <b>A block of cells a variant, two columns a lens, a few blocks a row</b>, and the grid is fixed rather than
/// packed: a lens's cell is <see cref="CellOf"/>, which both the renderer and the bake work out from numbers they
/// already hold. Nothing is written down beside the picture, so nothing beside the
/// picture can disagree with it. The cost is the columns an indicator does not use, which is transparent
/// texels in a sheet a few hundred pixels square.
/// </para>
/// <para>
/// <b>Only the lit states are here.</b> An unlit lens is the section of bodywork the artist drew, already
/// in the car's own sprite and already on screen — drawing a second copy of it over the first is what
/// made the old shared strip read as a sticker, since the two pictures agreed about neither the pixel
/// grid nor the bezel.
/// </para>
/// </remarks>
internal static class LampAtlas
{
    /// <summary>
    /// What every car sprite in the fleet is drawn at, and the resolution a lamp is therefore cut and
    /// drawn at. It is the fleet's own figure rather than a choice made here — <c>CarArtTests</c> holds
    /// the sprites to it — and it is why a cut lamp lands on the texel grid the body is drawn on.
    /// </summary>
    public const float ArtPxPerM = 96f;

    /// <summary>
    /// The side of one cell, in texels of that art. Wide enough for the largest lens the fleet draws —
    /// the ambulance's beacon, 27 × 37 — since a cell is square and every lamp quad is one cell.
    /// </summary>
    public const int CellPx = 40;

    /// <summary>The most lit states one lens can have: a rear cluster's red and white, a beacon end's two colours.</summary>
    public const int StatesPerLens = 2;

    /// <summary>Every lens a variant may draw, at two states each: the cells one look owns, whatever the fleet holds.</summary>
    public const int Columns = CarLamps.MostLenses * StatesPerLens;

    /// <summary>
    /// How many looks stand side by side in one row of the sheet. A row a look made the sheet one cell a look tall,
    /// which passed an atlas page's 4096 texels at 103 looks (the hundred-car fleet's batch 10).
    /// </summary>
    public const int LooksARow = 4;

    /// <summary>The sheet's width in cells.</summary>
    public const int SheetColumns = Columns * LooksARow;

    /// <summary>The sheet's height in cells, for a catalogue of <paramref name="looks"/>.</summary>
    public static int SheetRows(int looks) => (looks + LooksARow - 1) / LooksARow;

    /// <summary>
    /// The cell a look's lens burns <paramref name="state"/> in: its look's block of <see cref="Columns"/> cells,
    /// <see cref="LooksARow"/> blocks a row, and within it a column a lens's state.
    /// </summary>
    public static (int Column, int Row) CellOf(int variant, int lens, int state) =>
        ((variant % LooksARow * Columns) + (lens * StatesPerLens) + state, variant / LooksARow);

    /// <summary>One cell, as the body it is cut from measures it. The quad every lamp is drawn as.</summary>
    public static float CellM => CellPx / ArtPxPerM;

    /// <summary>
    /// What each lamp burns, which is the light itself: the colour the cut texels are driven to and the
    /// colour of the spill around them. <b>One table</b> — a lamp whose glass and whose glow disagreed
    /// about the colour would be a lens with somebody else's light around it.
    /// </summary>
    public static Vector4 ColourOf(CarLamp lamp) => lamp switch
    {
        CarLamp.Brake or CarLamp.BeaconRed => new Vector4(1f, 0.07f, 0.02f, 1f),
        CarLamp.Reverse => new Vector4(1f, 0.96f, 0.88f, 1f),
        CarLamp.BeaconBlue => new Vector4(0.05f, 0.40f, 1f, 1f),

        // Warmer than a reversing lamp, so the nose and the tail of a car backing up are not one light.
        CarLamp.Head => new Vector4(1f, 0.92f, 0.74f, 1f),

        // The indicator's amber and a works beacon's are the one colour: both are an amber lamp. It sits
        // at the orange end of amber because the vehicles that carry most of it are painted yellow — a
        // works bar the shade of the cab it is bolted to is a bar nobody sees.
        _ => new Vector4(1f, 0.40f, 0f, 1f),
    };

    /// <summary>
    /// The colour a fitting's <paramref name="state"/>-th cell burns. <b>This is the layout</b>: the bake
    /// fills the cells by walking it, and <see cref="StateOf"/> reads it back the other way round.
    /// </summary>
    public static CarLamp ColourAt(CarLampFitting fitting, int state) => fitting switch
    {
        CarLampFitting.Rear => state == 0 ? CarLamp.Brake : CarLamp.Reverse,
        CarLampFitting.Indicator => CarLamp.Indicator,
        CarLampFitting.BeaconRed => state == 0 ? CarLamp.BeaconRed : CarLamp.BeaconBlue,
        CarLampFitting.BeaconAmber => CarLamp.BeaconAmber,
        CarLampFitting.Head => CarLamp.Head,
        CarLampFitting.Brake => CarLamp.Brake,
        CarLampFitting.Reverse => CarLamp.Reverse,
        _ => state == 0 ? CarLamp.BeaconBlue : CarLamp.BeaconRed,
    };

    /// <summary>
    /// How many cells a fitting owns. Only a one-lens rear cluster and a beacon end burn two colours; the rest
    /// leave their second column empty.
    /// </summary>
    public static int StatesOf(CarLampFitting fitting) =>
        fitting is CarLampFitting.Rear or CarLampFitting.BeaconRed or CarLampFitting.BeaconBlue ? StatesPerLens : 1;

    /// <summary>Which of a lens's cells is the one showing this colour, read off <see cref="ColourAt"/> so the two cannot drift.</summary>
    public static int StateOf(CarLampFitting fitting, CarLamp colour) => ColourAt(fitting, 0) == colour ? 0 : 1;
}
