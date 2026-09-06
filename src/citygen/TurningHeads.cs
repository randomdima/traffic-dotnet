using System.Numerics;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The heads roads stop at</b> (TER-5a): where a road runs out with nothing carrying on from it, the
/// ground round its last point is wider than the road, because a car that drives in has to be able to work
/// itself round on the spot and there is no other arm to overrun into.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the one piece of ground a junction record still supplies, and it supplies it to a road.</b>
/// Everywhere else the tarmac inside a box is the band its own movements sweep (<see cref="LaneLines"/>) —
/// there is no disc, no ring and no shape a junction has of its own. A dead end has no movements to sweep
/// it: the turn that takes a car back out is a manoeuvre the driver makes (`P-19`, TER-5f) and not a line
/// the town lays, so the room for it has to be a shape, and the plan records how big it is exactly as it
/// records how wide a road is.
/// </para>
/// <para>
/// <b>Read off the arms and never off a flag.</b> A junction is a head when exactly one arm meets it, which
/// is <see cref="RoadCuts.ArmsPerJunction"/>'s question — so a map that joins a second road to a former dead
/// end stops having one without anything being edited.
/// </para>
/// </remarks>
internal readonly record struct TurningHeads(Vector2[] CentreM, float[] RadiusM)
{
    public int Count => CentreM.Length;

    public static TurningHeads Of(GroundPieces ground)
    {
        var arms = RoadCuts.ArmsPerJunction(ground);
        var heads = 0;
        foreach (var at in arms)
        {
            if (at == 1) heads++;
        }

        var centreM = new Vector2[heads];
        var radiusM = new float[heads];
        var head = 0;
        for (var junction = 0; junction < arms.Length; junction++)
        {
            if (arms[junction] != 1) continue;

            centreM[head] = ground.Junctions.CentreM[junction];
            radiusM[head] = ground.Junctions.RadiusM[junction];
            head++;
        }

        return new TurningHeads(centreM, radiusM);
    }
}
