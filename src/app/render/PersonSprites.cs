using System.Numerics;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.World.Town;

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
/// <para>
/// <b>Written in the order drawn</b> (PER-31, <see cref="Depth"/>), as the device's emit puts them
/// (<c>sprites_order.comp</c>).
/// </para>
/// </remarks>
internal static class PersonSprites
{
    /// <summary>
    /// Where a walker is drawn among the others (PER-31): every body lying down before any standing — it is flat on the
    /// ground — then the further its feet are down the picture the later, then across it. Read off the instance as drawn,
    /// so the host and the device rank one set of figures.
    /// </summary>
    /// <remarks>
    /// The feet are town <c>+y</c> of the quad whatever the town's turn (OBS-1c): an upright quad is built in the town's
    /// axes and turned with it, so the order needs no camera. The device breaks a tie of all three by roster order and the
    /// host's sort does not; two walkers tie only standing on one point to the bit.
    /// </remarks>
    internal readonly record struct Depth(bool Upright, float FeetM, float AcrossM) : IComparable<Depth>
    {
        public static Depth Of(in SpriteInstance walker, int firstDownSheet) => walker.Sheet >= firstDownSheet
            ? new Depth(false, walker.CentreM.Y, walker.CentreM.X)
            : new Depth(true, walker.CentreM.Y + walker.HalfSizeM.Y, walker.CentreM.X);

        public int CompareTo(Depth other)
        {
            if (Upright != other.Upright) return Upright ? 1 : -1;
            var feet = FeetM.CompareTo(other.FeetM);
            return feet != 0 ? feet : AcrossM.CompareTo(other.AcrossM);
        }
    }

    /// <summary>
    /// Fills <paramref name="into"/> in the order drawn and answers how many instances were written. A roster larger
    /// than the buffer is truncated rather than grown: the buffer is laid at the town's own capacity,
    /// so a truncation is a bug in the laying and not a case to handle at sixty hertz.
    /// </summary>
    /// <param name="drawn">The people a frame may show (<see cref="TownWorld.PeopleIn"/>); each is culled to the view again.</param>
    /// <param name="standingBelowMps">How slow a body is drawn standing (<c>SimConfig.Person.DrawnStandingBelowMps</c>).</param>
    /// <param name="depths">Room for each walker written's <see cref="Depth"/>, which they are sorted by.</param>
    public static int Fill(
        PersonFleet people, ReadOnlySpan<int> drawn, PersonCatalog catalog, ReadOnlySpan<float> frameAspects,
        int firstDownSheet, float standingBelowMps, Vector2 viewCentreM, Vector2 viewSpanM, Span<SpriteInstance> into,
        Span<Depth> depths, SpriteRings rings = default)
    {
        var written = 0;
        var halfView = viewSpanM * 0.5f;
        var room = Math.Min(into.Length, depths.Length);

        foreach (var person in drawn)
        {
            if (written >= room) break;

            // PHY-7: somebody inside a building or a car is not rendered. Only the container is.
            if (people.Inside[person].Any) continue;

            var body = Body(people, person, catalog, frameAspects, firstDownSheet, standingBelowMps, rings.OfPerson(person), out var reachM);
            var offset = body.CentreM - viewCentreM;
            if (MathF.Abs(offset.X) > halfView.X + reachM.X || MathF.Abs(offset.Y) > halfView.Y + reachM.Y) continue;

            depths[written] = Depth.Of(body, firstDownSheet);
            into[written++] = body;
        }

        depths[..written].Sort(into[..written]);
        return written;
    }

    /// <summary>The one quad a person on the street is drawn as, wherever it is.</summary>
    /// <param name="reachM">How far the quad reaches from its middle along each axis, whichever way it is turned.</param>
    static SpriteInstance Body(
        PersonFleet people, int person, PersonCatalog catalog, ReadOnlySpan<float> frameAspects, int firstDownSheet,
        float standingBelowMps, SpriteRing ring, out Vector2 reachM)
    {
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
        // A turned quad reaches its own half-diagonal whichever way it is pointing.
        reachM = down ? new Vector2(halfSizeM.Length()) : halfSizeM;

        if (down)
        {
            // One frame turned to the heading, on a car's terms: a body along the ground has a
            // direction, and the picture is drawn with the head along +x.
            return new SpriteInstance(centreM, halfSizeM, SpriteCell.Whole, SpriteTint.Plain, (uint)sheet, headingRad, ring: ring);
        }

        var row = PersonCatalog.FacingRow(headingRad);
        // Standing shows the stance, which is the cycle's own first column: a walker whose body has stopped must
        // not be left mid-stride, whether or not it still means to walk.
        var column = people.Walking[person] && people.VelocityMps[person].LengthSquared() >= standingBelowMps * standingBelowMps
            ? PersonCatalog.WalkColumn(people.DistanceWalkedM[person], heightM)
            : 0;

        // Upright, always: the art draws every facing and none of it turns with the body.
        return new SpriteInstance(
            centreM, halfSizeM, SpriteCell.Of(column, row, PersonCatalog.WalkColumns, PersonCatalog.FacingRows),
            SpriteTint.Plain, (uint)sheet, 0f, ring: ring);
    }
}
