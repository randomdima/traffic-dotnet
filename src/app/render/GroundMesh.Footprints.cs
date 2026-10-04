using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.CityGen;

namespace TrafficSimulation.App.Render;

/// <summary>The buildings a survey maps, drawn as their footprints.</summary>
internal sealed partial class GroundMesh
{
    /// <summary>
    /// <b>A footprint OSM maps, in one flat grey and no texture</b> (GEN-57): the survey's own outline under the
    /// prefabs it is worn as, grey so that where they part from it reads against their colours.
    /// </summary>
    static readonly Vector3 MappedRoof = new(0.62f, 0.62f, 0.60f);

    /// <summary>And one a machine traced off imagery where OSM maps none, a shade darker: the same kind of thing, less sure.</summary>
    static readonly Vector3 TracedRoof = new(0.56f, 0.56f, 0.55f);

    /// <summary>
    /// <b>How much lighter the tallest roofs are drawn than the lowest</b>: a roof nearer the sun reads lighter from
    /// above, so a town's tall blocks stand out of its houses without a height being drawn.
    /// </summary>
    const float TallestRoofLighter = 0.25f;

    /// <summary>How tall a building is drawn lightest at, and anything taller the same: a ten-storey block.</summary>
    const float TallestShadedM = 30f;

    /// <summary>
    /// <b>Every footprint, filled</b> (<see cref="CityPlan.Footprints"/>): its outline less its courtyards, its roof the
    /// lighter the taller it stands.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Laid unwelded</b>: no footprint shares a corner with anything else on the ground, so a corner is only ever
    /// asked for once and a hundred thousand buildings would fill the welding table for nothing.
    /// </para>
    /// <para>
    /// <b>One with no courtyard is clipped ear by ear</b> (<see cref="Polygon"/>), a handful of corners each; only one
    /// with a hole in it is cut as a shell, which costs a city's worth of footprints tens of seconds.
    /// </para>
    /// </remarks>
    void Footprints(CityPlan.FootprintArrays footprints)
    {
        var welding = _welding;
        _welding = false;
        var rings = new List<Vector2[]>();
        for (var footprint = 0; footprint < footprints.Count; footprint++)
        {
            var roof = footprints.Traced[footprint] ? TracedRoof : MappedRoof;
            var tall = footprints.HeightM.Length > 0 ? MathF.Min(footprints.HeightM[footprint] / TallestShadedM, 1f) : 0f;
            var tint = roof * (1f + (TallestRoofLighter * tall));
            var outline = footprints.RingOffsets[footprint];
            if (footprints.RingOffsets[footprint + 1] == outline + 1)
            {
                Polygon(footprints.Rings.RingOf(outline), Surface.Paint, tint);
                continue;
            }

            rings.Clear();
            for (var ring = outline; ring < footprints.RingOffsets[footprint + 1]; ring++)
            {
                // The fill reads which kind a ring is off its hand: an outline positive, a courtyard negative.
                var pointsM = footprints.Rings.RingOf(ring).ToArray();
                if (SignedArea(pointsM) > 0f != (ring == outline)) Array.Reverse(pointsM);
                rings.Add(pointsM);
            }

            Shell(CollectionsMarshal.AsSpan(rings), Surface.Paint, tint);
        }

        _welding = welding;
    }
}
