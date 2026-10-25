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
/// <b>A block of cells a variant, a column a lens and one more for each lens that burns a second light, a few blocks
/// a row</b>, and the grid is fixed rather than packed: a lens's cell is <see cref="CellOf"/>, which both the renderer
/// and the bake work out from what they already hold. Nothing is written down beside the picture, so nothing beside
/// the picture can disagree with it. Only a one-lens rear cluster burns two lights, and a look has at most
/// <see cref="SecondLights"/> of them: a second column for every lens left two thirds of every sheet clear, which is
/// the room the cell grew into.
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
    /// The side of one cell, in texels of that art. Wide enough for the longest lens the fleet draws — the police
    /// van's big bar's ends, 54 texels, and rear clusters of 41 to 44 that a 40-texel cell cut short — since a cell
    /// is square and every lamp quad is one cell.
    /// </summary>
    public const int CellPx = 56;

    /// <summary>The most lit states one lens can have: a rear cluster's red and white.</summary>
    public const int StatesPerLens = 2;

    /// <summary>The most lenses of one look that burn a second light: a rear cluster a side.</summary>
    public const int SecondLights = 2;

    /// <summary>Every lens a variant may draw, and a second cell for each that burns two lights: the cells one look owns.</summary>
    public const int Columns = CarLamps.MostLenses + SecondLights;

    /// <summary>
    /// How many looks stand side by side in one row of the sheet: as many as stay under an atlas page's 4096 texels,
    /// so the sheet is as square as the page it is packed on.
    /// </summary>
    public const int LooksARow = 6;

    /// <summary>The sheet's width in cells.</summary>
    public const int SheetColumns = Columns * LooksARow;

    /// <summary>The sheet's height in cells, for a catalogue of <paramref name="looks"/>.</summary>
    public static int SheetRows(int looks) => (looks + LooksARow - 1) / LooksARow;

    /// <summary>
    /// The cell a look's lens burns <paramref name="state"/> in: its look's block of <see cref="Columns"/> cells,
    /// <see cref="LooksARow"/> blocks a row, and within it the lens's own column, or for its second light the column
    /// past the lenses kept for the <paramref name="lenses"/> of the look that burn one, in their order.
    /// </summary>
    public static (int Column, int Row) CellOf(int variant, int lens, int state, ReadOnlySpan<CarLens> lenses)
    {
        var within = lens;
        if (state > 0)
        {
            within = CarLamps.MostLenses;
            for (var before = 0; before < lens; before++)
            {
                if (StatesOf(lenses[before].Fitting) > 1) within++;
            }
        }

        return ((variant % LooksARow * Columns) + within, variant / LooksARow);
    }

    /// <summary>
    /// Which lens and state of a look a column within its block is: the inverse of <see cref="CellOf"/>, for whoever
    /// reads a drawn cell back.
    /// </summary>
    public static (int Lens, int State) LensAt(int within, ReadOnlySpan<CarLens> lenses)
    {
        if (within < CarLamps.MostLenses) return (within, 0);

        var second = within - CarLamps.MostLenses;
        for (var lens = 0; lens < lenses.Length; lens++)
        {
            if (StatesOf(lenses[lens].Fitting) > 1 && second-- == 0) return (lens, 1);
        }

        throw new ArgumentOutOfRangeException(nameof(within), within, "no lens of the look burns a second light there");
    }

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
        CarLampFitting.BeaconRed => CarLamp.BeaconRed,
        CarLampFitting.BeaconAmber => CarLamp.BeaconAmber,
        CarLampFitting.Head => CarLamp.Head,
        CarLampFitting.Brake => CarLamp.Brake,
        CarLampFitting.Reverse => CarLamp.Reverse,
        _ => CarLamp.BeaconBlue,
    };

    /// <summary>
    /// How many cells a fitting owns. Only a one-lens rear cluster burns two lights, red braking and pale reversing;
    /// every other lamp burns the colour its glass is, and leaves its second column empty.
    /// </summary>
    public static int StatesOf(CarLampFitting fitting) => fitting is CarLampFitting.Rear ? StatesPerLens : 1;

    /// <summary>Whether a fitting is one end of a beacon bar, which burns and spills harder than a road lamp.</summary>
    public static bool IsBeacon(CarLampFitting fitting) =>
        fitting is CarLampFitting.BeaconRed or CarLampFitting.BeaconBlue or CarLampFitting.BeaconAmber;

    /// <summary>Which of a lens's cells is the one showing this colour, read off <see cref="ColourAt"/> so the two cannot drift.</summary>
    public static int StateOf(CarLampFitting fitting, CarLamp colour) => ColourAt(fitting, 0) == colour ? 0 : 1;
}
