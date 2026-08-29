using System.Numerics;
using TrafficSimulation.CityGen;

namespace TrafficSimulation.World.Statics;

/// <summary>
/// One building's roof as it stands on the map: which picture it wears, how big that picture is on the
/// ground, and which way round it is turned.
/// </summary>
/// <remarks>
/// It is the whole of what a building looks like <em>and</em> the whole of where its walls are: the parts
/// a variant is collided as (<see cref="BuildingVariant.PartsM"/>) are authored in the picture's own axes,
/// so the frame that puts the picture on the ground is the frame that puts the walls there too.
/// </remarks>
internal readonly record struct BuildingRoof(int Variant, Vector2 FootprintM, float HeadingRad);

/// <summary>
/// <b>Which roof each building wears and which way round</b> — one answer, read by what draws the town
/// and by what stands its collision geometry.
/// </summary>
/// <remarks>
/// It lives here rather than beside the renderer because it is a fact about the building. The two
/// readers cannot be allowed to disagree: a picture turned one way with its walls stood the other is a
/// town where cars are stopped by nothing and drive through porches, and the disagreement is invisible
/// until somebody switches the collision layer on.
/// </remarks>
internal static class BuildingRoofs
{
    public static BuildingRoof Of(CityPlan plan, BuildingCatalog catalogue, BuildingUses uses, int building)
    {
        var centreM = plan.Buildings.CentreM[building];
        var sizeM = plan.Buildings.SizeM[building];
        var civic = CivicRoof(catalogue, uses.Of(building));

        var (variant, swapped) = civic >= 0
            ? (civic, DoorRunsAcrossTheBuilding(plan, building, centreM))
            : catalogue.Match(sizeM);

        var footprintM = catalogue.Variants[variant].FootprintM;
        if (civic >= 0) footprintM *= FitScale(footprintM, sizeM, swapped);

        // The art's own +y is its door. Laying the art across the building's axes is a quarter turn, and
        // the remaining half turn is the only choice left — which of the two opposite walls the door is on.
        var quarter = swapped ? MathF.PI * 0.5f : 0f;
        var headingRad = plan.Buildings.HeadingRad[building] + quarter;
        if (FacesAway(plan, building, centreM, headingRad)) headingRad += MathF.PI;

        return new BuildingRoof(variant, footprintM, headingRad);
    }

    /// <summary>The roof a building's use names, or −1 for the buildings that are only buildings.</summary>
    static int CivicRoof(BuildingCatalog catalogue, BuildingUse use) => use switch
    {
        BuildingUse.Hospital => catalogue.Hospital,
        BuildingUse.PoliceStation => catalogue.PoliceStation,
        BuildingUse.Depot => catalogue.RepairShop,
        _ => NoCivicRoof,
    };

    const int NoCivicRoof = -1;

    /// <summary>
    /// <b>A civic roof is fitted to its building's box rather than drawn at the size it was painted</b>
    /// (AMB-1a). Which building serves a use is a draw and not a search over sizes, so the one hospital
    /// picture has to sit on whatever plot the draw landed on; fitted inside the box on its own aspect, it
    /// stands within its plot on every map, and stretched to the box it would lean.
    /// </summary>
    static float FitScale(Vector2 footprintM, Vector2 sizeM, bool swapped) => swapped
        ? MathF.Min(sizeM.X / footprintM.Y, sizeM.Y / footprintM.X)
        : MathF.Min(sizeM.X / footprintM.X, sizeM.Y / footprintM.Y);

    /// <summary>
    /// <b>Which pair of walls a civic building's door is on</b> (AMB-1a): whether the ways in lie across
    /// the building's own axis or along it.
    /// </summary>
    /// <remarks>
    /// <b>An ordinary roof takes this from its size and a civic one cannot.</b> A matched roof is turned so
    /// that a wide picture lands on a wide building, and the half turn <see cref="FacesAway"/> adds then
    /// picks between the two walls that pair leaves — which is the right answer when the picture and the
    /// plot are the same rectangle. A civic roof is fitted rather than matched, so the pair is free, and
    /// choosing it by which way round the picture is bigger put a police station's front door on a side
    /// wall with its sign reading down the street. It is chosen by the pavement instead: the pair of walls
    /// the plan's ways in actually sit off, with the half turn settling which of the two as before.
    /// </remarks>
    static bool DoorRunsAcrossTheBuilding(CityPlan plan, int building, Vector2 centreM)
    {
        var towardsWays = TowardsTheWaysIn(plan, building, centreM);
        if (towardsWays.LengthSquared() <= 0f) return false;

        // The building's own axes at its own bearing. The art's door is its +y, so the door is across the
        // building exactly when the ways in lie more along its x than along its y.
        var headingRad = plan.Buildings.HeadingRad[building];
        var along = new Vector2(MathF.Cos(headingRad), MathF.Sin(headingRad));
        var across = new Vector2(-along.Y, along.X);

        return MathF.Abs(Vector2.Dot(towardsWays, along)) > MathF.Abs(Vector2.Dot(towardsWays, across));
    }

    /// <summary>
    /// Whether the door would face the back of the building — the wall furthest from the ways in the
    /// plan carries. A building with no way in (OBJ-4 says there is always one) keeps the plan's bearing.
    /// </summary>
    static bool FacesAway(CityPlan plan, int building, Vector2 centreM, float headingRad)
    {
        // The quad's own +y, which is where the art's door points once the instance is turned.
        var towardsWays = TowardsTheWaysIn(plan, building, centreM);
        if (towardsWays.LengthSquared() <= 0f) return false;

        var door = new Vector2(-MathF.Sin(headingRad), MathF.Cos(headingRad));
        return Vector2.Dot(door, towardsWays) < 0f;
    }

    /// <summary>Where this building's ways in stand, as one direction off its middle. Zero where it has none.</summary>
    static Vector2 TowardsTheWaysIn(CityPlan plan, int building, Vector2 centreM)
    {
        var from = plan.Buildings.EntryOffsets[building];
        var to = plan.Buildings.EntryOffsets[building + 1];

        var towardsWays = Vector2.Zero;
        for (var way = from; way < to; way++) towardsWays += plan.Buildings.EntryPointM[way] - centreM;

        return towardsWays;
    }
}
