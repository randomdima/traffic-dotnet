using System.Numerics;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Hud;

/// <summary>
/// <b>CTL-1: the selected unit is marked on the town.</b> Four corner brackets standing just outside
/// its box, laid in the unit's own frame — so a car's brackets turn with the car and a walker's, whose
/// picture never turns, stand upright.
/// </summary>
/// <remarks>
/// <para>
/// <b>A shape and not a tint.</b> A brighter sprite says "selected" only against the sprite beside it:
/// on a white van, at a district framing, or in a queue of one make it says nothing at all. Brackets
/// are readable off a single unit, and they leave the art the colour it was drawn — which is the whole
/// of what the picture is for.
/// </para>
/// <para>
/// <b>It wraps the box the unit is drawn at</b> — the car's own build (CAR-12a), the walker's own
/// variant height — so nothing here can drift from what is on screen underneath it.
/// </para>
/// </remarks>
internal static class SelectionMark
{
    /// <summary>How far outside the box the brackets stand, and how much of a side each arm covers, as shares of that side — so a truck and a hatchback are wrapped the same way.</summary>
    const float ClearanceShare = 0.14f;

    const float ArmShare = 0.3f;

    /// <summary>The stroke, and the least the brackets ever stand off the body, in screen pixels divided by the zoom — as the ruler's tape is: a mark a metre thick covers the car it wraps.</summary>
    const float StrokePx = 2f;

    const float LeastClearancePx = 3f;

    /// <summary>The stroke the outline under a mark is drawn at, which is what makes it read on any paint.</summary>
    const float OutlinePx = StrokePx + 2f;

    public static void Draw(ref ScreenDraw draw, TownWorld world, SimConfig config, float pixelsPerMetre)
    {
        if (pixelsPerMetre <= 0f) return;

        // One shape a unit and the same shape however many there are (CTL-1b): a group is read off the
        // brackets standing on each of its members, not off a hull drawn round the lot.
        foreach (var selection in world.Selected)
        {
            One(ref draw, world, config, selection, pixelsPerMetre);
        }

        SecondHand(ref draw, world, config, pixelsPerMetre);
    }

    /// <summary>
    /// <b>CTL-5d: the car somebody else has the wheel of, marked as well.</b> The same brackets in a hue of
    /// its own, so a reader watching a bot drive can see which car is its — and nothing at all when the
    /// reader has that car picked out themselves, since the selection is the answer they asked for.
    /// </summary>
    static void SecondHand(ref ScreenDraw draw, TownWorld world, SimConfig config, float pixelsPerMetre)
    {
        if (world.HandDrivenCar < 0) return;

        var car = new Selection(SelectionKind.Car, world.HandDrivenCar);
        if (world.IsSelected(SelectionKind.Car, car.Index)) return;

        if (!BoxOf(world, config, car, out var centreM, out var sizeM, out var headingRad)) return;

        // Outlined, and the selection's own mark is not: this one lands on a car nobody chose, so it has to
        // read against whatever that car is painted — the first pink car under a pink mark is invisible.
        Brackets(ref draw, centreM, sizeM, headingRad, pixelsPerMetre, Theme.MarkOutline, OutlinePx);
        Brackets(ref draw, centreM, sizeM, headingRad, pixelsPerMetre, Theme.SecondHandMark);
    }

    static void One(
        ref ScreenDraw draw, TownWorld world, SimConfig config, Selection selection, float pixelsPerMetre)
    {
        if (!BoxOf(world, config, selection, out var centreM, out var sizeM, out var headingRad)) return;

        Brackets(ref draw, centreM, sizeM, headingRad, pixelsPerMetre, Theme.SelectionMark);
    }

    /// <summary>
    /// The box a selected unit is drawn at — the car's own build (CAR-12a), the walker's own variant
    /// height — so nothing drawn against a unit can drift from what is on screen underneath it.
    /// </summary>
    /// <returns>
    /// Whether the unit is on the picture at all. <b>PHY-7: somebody inside a building or a car is not
    /// drawn</b>, and there is nothing on screen to wrap — the container is what a reader can see and what
    /// a click would have picked. What that unit is doing is still read off the panel in the corner
    /// (<see cref="UnitPanel"/>), which needs nothing on screen to point at.
    /// </returns>
    public static bool BoxOf(
        TownWorld world, SimConfig config, Selection selection, out Vector2 centreM, out Vector2 sizeM,
        out float headingRad)
    {
        if (selection.Kind == SelectionKind.Car)
        {
            ref readonly var build = ref world.Cars.BuildOf(selection.Index);
            centreM = world.Cars.PositionM[selection.Index];
            sizeM = new Vector2(build.LengthM, build.WidthM);
            headingRad = world.Cars.HeadingRad[selection.Index];
            return true;
        }

        var person = selection.Index;
        centreM = world.People.PositionM[person];
        headingRad = 0f;
        if (world.People.Inside[person].Any)
        {
            sizeM = default;
            return false;
        }

        var variant = world.People.Variant[person] % PersonCatalog.Shared.SheetCount;
        sizeM = new Vector2(config.PersonDiameterM, PersonCatalog.Shared.Variants[variant].HeightM);
        return true;
    }

    /// <summary>
    /// The brackets themselves, round any box the picture holds: the selected unit wears them, and so does
    /// a thing the selection is on its way <em>into</em> (CTL-1a) — one shape said twice, because a goal
    /// that is a building or a car is marked by wrapping it exactly as the unit is.
    /// </summary>
    /// <param name="strokePx">
    /// How thick the arms are drawn. <b>A second pass at a wider stroke is how a mark is outlined</b>
    /// (<see cref="SecondHand"/>), so it reads against the paint underneath it whatever colour that is.
    /// </param>
    public static void Brackets(
        ref ScreenDraw draw, Vector2 centreM, Vector2 sizeM, float headingRad, float pixelsPerMetre,
        Vector4 colour, float strokePx = StrokePx)
    {
        var forward = Heading.Unit(headingRad);
        var right = Heading.RightOf(forward);
        var strokeM = strokePx / pixelsPerMetre;
        var clearanceM = MathF.Max(MathF.Min(sizeM.X, sizeM.Y) * ClearanceShare, LeastClearancePx / pixelsPerMetre);
        var reachM = (sizeM * 0.5f) + new Vector2(clearanceM);
        var armM = reachM * ArmShare;

        for (var corner = 0; corner < 4; corner++)
        {
            var alongSign = corner is 0 or 3 ? 1f : -1f;
            var acrossSign = corner is 0 or 1 ? 1f : -1f;

            // Half a stroke past the corner both ways, so the two arms meet square instead of leaving a
            // notch out of the very corner the bracket is drawn for.
            var atM = centreM
                + (forward * ((reachM.X * alongSign) + (strokeM * 0.5f * alongSign)))
                + (right * ((reachM.Y * acrossSign) + (strokeM * 0.5f * acrossSign)));

            draw.LineM(atM, atM - (forward * (armM.X * alongSign)), strokeM, colour);
            draw.LineM(atM, atM - (right * (armM.Y * acrossSign)), strokeM, colour);
        }
    }
}
