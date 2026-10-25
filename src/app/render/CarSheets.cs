using TrafficSimulation.Runtime;

namespace TrafficSimulation.App.Render;

/// <summary>
/// The rubber every tyre is drawn with (<see cref="CarSprites.FillTyres"/>). A car's own sheet is its art as cut, with
/// no tyre painted into it: the body tilts on its springs and the tyres do not (CAR-56).
/// </summary>
/// <remarks>
/// <b>A tyre is a plain rectangle along the body</b>, at the offset its impulse acts on
/// (<see cref="Agents.Car.Body.TyreModel.WheelAtM"/>) and the size its build states. It does not roll.
/// </remarks>
internal static class CarSheets
{
    static readonly Texel Rubber = new(0, 0, 0, byte.MaxValue);

    /// <summary>One texel of rubber, which a tyre's quad is stretched over.</summary>
    public static SheetSource RubberSheet() => SheetSource.Generated([Rubber.R, Rubber.G, Rubber.B, Rubber.A], 1, 1);
}
