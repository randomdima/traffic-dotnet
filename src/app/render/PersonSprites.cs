using System.Numerics;
using TrafficSimulation.Agents.Person.Body;

namespace TrafficSimulation.App.Render;

/// <summary>
/// The walkers, as instances for the second pipeline: one upright quad each, the facing row picked
/// from the heading and the walk column stepped by the distance the body has actually covered.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing is emitted for a body that is not on screen.</b> The cull is the whole reason this
/// takes a view rectangle: a town of five hundred walkers at a district framing has a dozen of them
/// on screen, and the instance count is what the indirect draw reads, so the recording never changes
/// whichever number it is.
/// </para>
/// <para>
/// The stride length is a relation rather than a figure: one eight-frame cycle covers one stride and
/// the column is stepped by distance rather than by time, but nothing states what a stride <em>is</em>.
/// What is used here is one cycle per the variant's own height — off shipped data rather than a number
/// invented for the purpose.
/// </para>
/// </remarks>
internal static class PersonSprites
{
    /// <summary>
    /// A selected walker is drawn brighter, by the inverse of the factor an edge is drawn darker by —
    /// the same relation the painted marks on the ground use, so there is one idea of "stands out" in
    /// the whole picture rather than two.
    /// </summary>
    public static readonly Vector4 Highlight = new(1f / 0.58f, 1f / 0.58f, 1f / 0.62f, 1f);

    public static readonly Vector4 Plain = Vector4.One;

    /// <summary>
    /// Fills <paramref name="into"/> and answers how many instances were written. A roster larger
    /// than the buffer is truncated rather than grown: the buffer is laid at the town's own capacity,
    /// so a truncation is a bug in the laying and not a case to handle at sixty hertz.
    /// </summary>
    public static int Fill(
        PersonFleet people, PersonCatalog catalog, ReadOnlySpan<float> frameAspects, Vector2 viewCentreM,
        Vector2 viewSpanM, int selected, Span<SpriteInstance> into)
    {
        var written = 0;
        var halfView = viewSpanM * 0.5f;

        for (var person = 0; person < people.Count && written < into.Length; person++)
        {
            // PHY-7: somebody inside a building or a car is not rendered. Only the container is.
            if (people.Inside[person].Any) continue;

            var variant = people.Variant[person] % catalog.Count;
            var heightM = catalog.Variants[variant].HeightM;
            var halfSizeM = new Vector2(heightM * frameAspects[variant] * 0.5f, heightM * 0.5f);

            var centreM = people.PositionM[person];
            var offset = centreM - viewCentreM;
            if (MathF.Abs(offset.X) > halfView.X + halfSizeM.X || MathF.Abs(offset.Y) > halfView.Y + halfSizeM.Y) continue;

            var row = PersonCatalog.FacingRow(people.HeadingRad[person]);
            // Standing shows the plant pose, which is the cycle's own first column: a walker that has
            // stopped must not be left mid-stride.
            var column = people.Walking[person] ? PersonCatalog.WalkColumn(people.DistanceWalkedM[person], heightM) : 0;

            var cell = new Vector2(1f / PersonCatalog.WalkColumns, 1f / PersonCatalog.FacingRows);
            // Upright, always: the art draws every facing and none of it turns with the body.
            into[written++] = new SpriteInstance(
                centreM, halfSizeM, new Vector2(column * cell.X, row * cell.Y), cell,
                person == selected ? Highlight : Plain, (uint)variant, 0f);
        }

        return written;
    }
}
