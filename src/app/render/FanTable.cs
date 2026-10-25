using System.Numerics;
using TrafficSimulation.Agents.Train.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Statics;

namespace TrafficSimulation.App.Render;

/// <summary>
/// <b>Every fan a picture draws as the sprite stages read them</b> — a roof's (OBJ-6) and a vehicle's (TRN-13): a word a
/// sheet slot naming its run of fans, then each fan's circle and rim, then where the fans' looks were packed. Nothing else
/// is kept for a fan — no instance: the fragment stage lays one of the looks over the fan its body's picture draws, fitted
/// to its rim and turned, the look and the start the body's ID gives it (<see cref="SpriteInstance.Id"/>, <c>fans.glsl</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Laid as the shaders' uniform block reads it</b>: the runs as <c>uvec4 runs[512]</c>, four words a row, a run its
/// first fan in the low sixteen bits and how many in the high; the fans as <c>vec4 fans[2048]</c>, two rows a fan — the
/// hub and the circle's radius in the sheet's own units, nought to one across; then the rim's ellipse in the circle's own
/// units, the hub off its middle over its half-axes and the radius over them. The last <see cref="MostLooks"/> fans' rows
/// are the looks' instead (<see cref="LookRow"/>, <see cref="PutLooks"/>): a look's place on the atlas, then its layer
/// and how many looks there are.
/// </para>
/// <para>
/// <b>The art draws a fan a little out of round and off its hub</b>: the circle is as wide as the rim can reach from the
/// hub (<see cref="DrawnFan.FarthestReachM"/>), and each point of it inside the rim reads the look that share of the way
/// out from its middle to its edge along the same way, the rim taken <see cref="RimOutsetPx"/> wide of the ellipse fitted
/// through it. So the look covers all the opening the art drew, hub on hub and edge on rim, and turns round.
/// </para>
/// </remarks>
internal static class FanTable
{
    /// <summary>The fans' looks the block holds a place for, past its fans.</summary>
    public const int MostLooks = 16;

    /// <summary>The fans the block holds: it is the shaders' array length over two, less the rows the looks take.</summary>
    public const int MostFans = 1024 - MostLooks;

    /// <summary>The row of the shaders' fan array the looks' places start at: two rows a look (<c>FAN_LOOK_ROW</c>).</summary>
    public const int LookRow = MostFans * 2;

    /// <summary>The words of the block: a run a sheet slot, then eight a fan, then eight a look.</summary>
    public const int Words = SheetAtlas.Slots + ((MostFans + MostLooks) * 8);

    /// <summary>
    /// How far past the fitted rim a look is drawn, in the picture's texels: the art's own rim wanders a texel or so either
    /// side of the ellipse fitted through it, and a texel of it left outside the look shows as a nick in the edge.
    /// </summary>
    const float RimOutsetPx = 1.5f;

    /// <param name="firstRoofSheet">The slot the catalogue's first roof is drawn from (<see cref="TownSprites.FirstBuildingSheet"/>).</param>
    /// <param name="firstVehicleSheet">And its first vehicle (<see cref="TownSprites.FirstTrainSheet"/>).</param>
    public static uint[] Of(BuildingCatalog roofs, int firstRoofSheet, TrainCatalog vehicles, int firstVehicleSheet)
    {
        var words = new uint[Words];
        var fans = 0;
        for (var variant = 0; variant < roofs.Count; variant++)
        {
            ref readonly var roof = ref roofs.Variants[variant];
            Lay(words, ref fans, firstRoofSheet + variant, roof.SpritePath, roof.FootprintM, roof.Fans);
        }

        for (var vehicle = 0; vehicle < vehicles.Vehicles.Length; vehicle++)
        {
            ref readonly var drawn = ref vehicles.Vehicles[vehicle];
            Lay(words, ref fans, firstVehicleSheet + vehicle, drawn.SpritePath, drawn.FootprintM, drawn.Fans);
        }

        return words;
    }

    /// <summary>One picture's run, drawn at <paramref name="footprintM"/>; a picture not drawn yet turns none.</summary>
    static void Lay(uint[] words, ref int fans, int sheet, string? spritePath, Vector2 footprintM, DrawnFan[] drawn)
    {
        if (drawn.Length == 0 || spritePath is null) return;
        if (fans + drawn.Length > MostFans) throw new InvalidDataException(
            $"The pictures draw more than {MostFans} fans, the shaders' table less its looks (fans.glsl).");

        var (widthPx, _) = ImageHeader.Measure(spritePath);
        var outsetM = RimOutsetPx * footprintM.X / widthPx;
        words[sheet] = (uint)fans | ((uint)drawn.Length << 16);
        foreach (var fan in drawn)
        {
            var radiusM = fan.FarthestReachM + outsetM;
            var drawnToM = fan.RimM + new Vector2(outsetM);
            var at = SheetAtlas.Slots + (fans++ * 8);
            Put(words, at, (fan.AtM / footprintM) + new Vector2(0.5f), new Vector2(radiusM) / footprintM);
            Put(words, at + 4, (fan.AtM - fan.RimAtM) / drawnToM, new Vector2(radiusM) / drawnToM);
        }
    }

    /// <summary>
    /// <b>Where the fans' looks were packed</b> (<see cref="BuildingCatalog.FanLooks"/>), into the block's last rows: known
    /// only once the atlas is laid, so written into the block <see cref="Of"/> laid when it is (<see cref="TownSprites.ReadAspects"/>).
    /// </summary>
    public static void PutLooks(uint[] words, ReadOnlySpan<SheetPlace> places, int firstLookSheet, int looks)
    {
        if (looks > MostLooks) throw new InvalidDataException(
            $"Fans.json lists {looks} looks, and the shaders' table holds {MostLooks} (fans.glsl).");

        for (var look = 0; look < looks; look++)
        {
            ref readonly var place = ref places[firstLookSheet + look];
            var at = SheetAtlas.Slots + ((LookRow + (look * 2)) * 4);
            Put(words, at, place.OriginUv, place.ScaleUv);
            Put(words, at + 4, new Vector2(place.Layer, looks), Vector2.Zero);
        }
    }

    static void Put(uint[] words, int at, Vector2 first, Vector2 second)
    {
        words[at + 0] = BitConverter.SingleToUInt32Bits(first.X);
        words[at + 1] = BitConverter.SingleToUInt32Bits(first.Y);
        words[at + 2] = BitConverter.SingleToUInt32Bits(second.X);
        words[at + 3] = BitConverter.SingleToUInt32Bits(second.Y);
    }

    /// <summary>
    /// How far every fan has turned at <paramref name="tick"/>, in turns, within one: each body adds its own share to it
    /// (<c>fans.glsl</c>). Off the tick count in double, as a float of seconds hours in has lost the fraction of a turn.
    /// </summary>
    public static float TurnsAt(long tick, SimConfig config)
    {
        var turns = tick * (double)config.View.FanTurnsPerS / config.Sim.TickRateHz;
        return (float)(turns - Math.Floor(turns));
    }
}
