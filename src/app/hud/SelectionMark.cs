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

    public static void Draw(ref ScreenDraw draw, TownWorld world, SimConfig config, float pixelsPerMetre)
    {
        if (pixelsPerMetre <= 0f) return;

        // One shape a unit and the same shape however many there are (CTL-1b): a group is read off the
        // brackets standing on each of its members, not off a hull drawn round the lot.
        foreach (var selection in world.Selected)
        {
            One(ref draw, world, config, selection, pixelsPerMetre);
        }
    }

    static void One(
        ref ScreenDraw draw, TownWorld world, SimConfig config, Selection selection, float pixelsPerMetre)
    {
        Vector2 centreM;
        Vector2 sizeM;
        float headingRad;
        if (selection.Kind == SelectionKind.Car)
        {
            ref readonly var build = ref world.Cars.BuildOf(selection.Index);
            centreM = world.Cars.PositionM[selection.Index];
            sizeM = new Vector2(build.LengthM, build.WidthM);
            headingRad = world.Cars.HeadingRad[selection.Index];
        }
        else
        {
            var person = selection.Index;
            // PHY-7: somebody inside a building or a car is not drawn, and there is nothing on screen to
            // wrap. The container is what a reader can see and what a click would have picked.
            if (world.People.Inside[person].Any) return;

            var variant = world.People.Variant[person] % PersonCatalog.Shared.SheetCount;
            centreM = world.People.PositionM[person];
            sizeM = new Vector2(config.PersonDiameterM, PersonCatalog.Shared.Variants[variant].HeightM);
            headingRad = 0f;
        }

        Brackets(ref draw, centreM, sizeM, headingRad, pixelsPerMetre, Theme.SelectionMark);
    }

    /// <summary>
    /// The brackets themselves, round any box the picture holds: the selected unit wears them, and so does
    /// a thing the selection is on its way <em>into</em> (CTL-1a) — one shape said twice, because a goal
    /// that is a building or a car is marked by wrapping it exactly as the unit is.
    /// </summary>
    public static void Brackets(
        ref ScreenDraw draw, Vector2 centreM, Vector2 sizeM, float headingRad, float pixelsPerMetre, Vector4 colour)
    {
        var forward = Heading.Unit(headingRad);
        var right = Heading.RightOf(forward);
        var strokeM = StrokePx / pixelsPerMetre;
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
