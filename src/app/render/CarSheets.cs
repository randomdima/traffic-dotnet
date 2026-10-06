using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;

namespace TrafficSimulation.App.Render;

/// <summary>
/// A car's sheet with its rear tyres painted under the bodywork, so the pair that never steers costs the
/// frame nothing. The front pair turns and is drawn on its own (<see cref="CarSprites.FillFrontTyres"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>A tyre is a plain rectangle along the body</b>, at the offset its impulse acts on
/// (<see cref="TyreModel.WheelAtM"/>) and the size its build states. It does not roll.
/// </para>
/// <para>
/// <b>The sheet is padded and the quad grows with it</b>, because the rubber stands past the bodywork
/// (CAR-12) and the bodywork reaches the edge of its own sheet. The padding is the same both sides, so
/// the car's centre stays the quad's, and the art inside it is still drawn at its own box (CAR-12a).
/// </para>
/// </remarks>
internal static class CarSheets
{
    static readonly Texel Rubber = new(0, 0, 0, byte.MaxValue);

    /// <summary>One texel of the same rubber, which a steered tyre's quad is stretched over.</summary>
    public static SheetSource RubberSheet() => SheetSource.Generated([Rubber.R, Rubber.G, Rubber.B, Rubber.A], 1, 1);

    /// <param name="artSpanM">The box the art is drawn to, along the car and across it — the footprint, or a wreck's own.</param>
    /// <param name="drawnScale">How much larger than <paramref name="artSpanM"/> the padded sheet is drawn, per axis.</param>
    public static SheetSource WithTyres(string path, Vector2 artSpanM, in CarBuild build, out Vector2 drawnScale)
    {
        const int Painted = TyreModel.Wheels - TyreModel.SteeredWheels;
        var (widthPx, heightPx) = ImageHeader.Measure(path);
        var artPx = new Vector2(widthPx, heightPx);
        var perM = artPx / artSpanM;
        var halfTyreM = new Vector2(build.WheelLengthM, build.WheelWidthM) * 0.5f;

        var reachM = Vector2.Zero;
        for (var wheel = TyreModel.SteeredWheels; wheel < TyreModel.Wheels; wheel++)
        {
            reachM = Vector2.Max(reachM, Vector2.Abs(TyreModel.WheelAtM(build, wheel)) + halfTyreM);
        }

        var padPx = Vector2.Max(Vector2.Zero, (reachM - (artSpanM * 0.5f)) * perM);
        var padXPx = (int)MathF.Ceiling(padPx.X);
        var padYPx = (int)MathF.Ceiling(padPx.Y);
        var sheetPx = artPx + new Vector2(padXPx * 2, padYPx * 2);

        var centrePx = sheetPx * 0.5f;
        var tyres = new TexelBox[Painted];
        for (var wheel = TyreModel.SteeredWheels; wheel < TyreModel.Wheels; wheel++)
        {
            var atM = TyreModel.WheelAtM(build, wheel);
            tyres[wheel - TyreModel.SteeredWheels] =
                new TexelBox(centrePx + ((atM - halfTyreM) * perM), centrePx + ((atM + halfTyreM) * perM));
        }

        drawnScale = sheetPx / artPx;
        return SheetSource.Underlaid(path, new SheetUnderlay(padXPx, padYPx, tyres, Rubber));
    }
}
