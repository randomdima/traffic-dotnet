using System.Numerics;
using TrafficSimulation.Agents.Person.Body;

namespace TrafficSimulation.App.Render;

/// <summary>
/// The walkers, as instances for the second pipeline: one upright quad each, the facing row picked
/// from the heading and the walk column stepped by the distance the body has actually covered — and
/// one quad lying along the ground for everybody who is down.
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
/// <para>
/// <b>A casualty is drawn the way a car is</b> (PER-18): its own sheet, one frame with the head along
/// <c>+x</c>, turned to the heading it went down at and laid out at the length a standing body's height
/// becomes. The blood is in the picture rather than in the tint, because what a body is doing is read
/// off <em>which sheet</em> is sampled everywhere else in this town too.
/// </para>
/// </remarks>
internal static class PersonSprites
{
    public static readonly Vector4 Plain = Vector4.One;

    /// <summary>
    /// Fills <paramref name="into"/> and answers how many instances were written. A roster larger
    /// than the buffer is truncated rather than grown: the buffer is laid at the town's own capacity,
    /// so a truncation is a bug in the laying and not a case to handle at sixty hertz.
    /// </summary>
    public static int Fill(
        PersonFleet people, PersonCatalog catalog, ReadOnlySpan<float> frameAspects, int firstDownSheet,
        Vector2 viewCentreM, Vector2 viewSpanM, Span<SpriteInstance> into)
    {
        var written = 0;
        var halfView = viewSpanM * 0.5f;
        var cell = new Vector2(1f / PersonCatalog.WalkColumns, 1f / PersonCatalog.FacingRows);

        for (var person = 0; person < people.Count && written < into.Length; person++)
        {
            // PHY-7: somebody inside a building or a car is not rendered. Only the container is.
            if (people.Inside[person].Any) continue;

            // Over every look and not only the walkers': a uniform is a sheet slot like any other,
            // and the one the crew was named is the one it has to be drawn in (SRV-3a).
            var variant = people.Variant[person] % catalog.SheetCount;
            var heightM = catalog.Variants[variant].HeightM;
            var headingRad = people.HeadingRad[person];
            var down = !people.IsOnItsFeet(person);

            // Standing, the height is the quad's height and the art's own frame gives its width; down,
            // that height is its length along the ground and the width follows the same picture.
            var sheet = down ? firstDownSheet + variant : variant;
            var aspect = frameAspects[sheet];
            var halfSizeM = down
                ? new Vector2(heightM, heightM / aspect) * 0.5f
                : new Vector2(heightM * aspect, heightM) * 0.5f;

            var centreM = people.PositionM[person];
            var offset = centreM - viewCentreM;
            // A turned quad reaches its own half-diagonal whichever way it is pointing.
            var reachM = down ? new Vector2(halfSizeM.Length()) : halfSizeM;
            if (MathF.Abs(offset.X) > halfView.X + reachM.X || MathF.Abs(offset.Y) > halfView.Y + reachM.Y) continue;

            if (down)
            {
                // One frame turned to the heading, on a car's terms: a body along the ground has a
                // direction, and the picture is drawn with the head along +x.
                into[written++] = new SpriteInstance(
                    centreM, halfSizeM, Vector2.Zero, Vector2.One, Plain, (uint)sheet, headingRad);
                continue;
            }

            var row = PersonCatalog.FacingRow(headingRad);
            // Standing shows the plant pose, which is the cycle's own first column: a walker that has
            // stopped must not be left mid-stride.
            var column = people.Walking[person] ? PersonCatalog.WalkColumn(people.DistanceWalkedM[person], heightM) : 0;

            // Upright, always: the art draws every facing and none of it turns with the body.
            into[written++] = new SpriteInstance(
                centreM, halfSizeM, new Vector2(column * cell.X, row * cell.Y), cell, Plain, (uint)sheet, 0f);
        }

        return written;
    }
}
