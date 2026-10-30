using System.Numerics;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.App.Screen;
using TrafficSimulation.World.Town;

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
/// <para>
/// <b>A casualty is its own stance, reddened</b> (PER-18, <see cref="Theme.Casualty"/>): the walk sheet's
/// standing cell at the facing it went down at, so the body in the road is the walker who was there.
/// </para>
/// <para>
/// <b>Written in the order drawn</b> (PER-31, <see cref="Depth"/>), as the device's emit puts them
/// (<c>sprites_order.comp</c>).
/// </para>
/// </remarks>
internal static class PersonSprites
{
    /// <summary><see cref="Theme.Casualty"/> as an instance carries it, which the device's emit is handed too.</summary>
    public static readonly uint CasualtyTint = SpriteTint.Pack(Theme.Casualty);

    /// <summary>
    /// Where a walker is drawn among the others (PER-31): the further its feet are down the picture the later, then
    /// across it. Read off the instance as drawn, so the host and the device rank one set of figures.
    /// </summary>
    /// <remarks>
    /// The feet are town <c>+y</c> of the quad whatever the town's turn (OBS-1c): an upright quad is built in the town's
    /// axes and turned with it, so the order needs no camera. The device breaks a tie of both by roster order and the
    /// host's sort does not; two walkers tie only standing on one point to the bit.
    /// </remarks>
    internal readonly record struct Depth(float FeetM, float AcrossM) : IComparable<Depth>
    {
        public static Depth Of(in SpriteInstance walker) => new(walker.CentreM.Y + walker.HalfSizeM.Y, walker.CentreM.X);

        public int CompareTo(Depth other)
        {
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
    /// <param name="byDepth">Whether they are sorted by it (PER-31), or left in the order listed.</param>
    public static int Fill(
        PersonFleet people, ReadOnlySpan<int> drawn, PersonCatalog catalog, ReadOnlySpan<float> frameAspects,
        float standingBelowMps, Vector2 viewCentreM, Vector2 viewSpanM, Span<SpriteInstance> into,
        Span<Depth> depths, SpriteRings rings = default, bool byDepth = true)
    {
        var written = 0;
        var halfView = viewSpanM * 0.5f;
        var room = Math.Min(into.Length, depths.Length);

        foreach (var person in drawn)
        {
            if (written >= room) break;

            // PHY-7: somebody inside a building or a car is not rendered. Only the container is.
            if (people.Inside[person].Any) continue;

            var body = Body(people, person, catalog, frameAspects, standingBelowMps, rings.OfPerson(person));
            var offset = body.CentreM - viewCentreM;
            var reachM = body.HalfSizeM;
            if (MathF.Abs(offset.X) > halfView.X + reachM.X || MathF.Abs(offset.Y) > halfView.Y + reachM.Y) continue;

            depths[written] = Depth.Of(body);
            into[written++] = body;
        }

        if (byDepth) depths[..written].Sort(into[..written]);
        return written;
    }

    /// <summary>The one quad a person on the street is drawn as, wherever it is.</summary>
    static SpriteInstance Body(
        PersonFleet people, int person, PersonCatalog catalog, ReadOnlySpan<float> frameAspects, float standingBelowMps,
        SpriteRing ring)
    {
        // Over every look and not only the walkers': a uniform is a sheet slot like any other,
        // and the one the crew was named is the one it has to be drawn in (SRV-3a).
        var variant = people.Variant[person] % catalog.SheetCount;
        var heightM = catalog.Variants[variant].HeightM;
        var down = !people.IsOnItsFeet(person);

        // The height is the quad's height and the art's own frame gives its width.
        var halfSizeM = new Vector2(heightM * frameAspects[variant], heightM) * 0.5f;

        var row = PersonCatalog.FacingRow(people.HeadingRad[person]);
        // Standing shows the stance, which is the cycle's own first column: a walker whose body has stopped must
        // not be left mid-stride, whether or not it still means to walk — and a casualty is not walking.
        var column = !down && people.Walking[person]
                     && people.VelocityMps[person].LengthSquared() >= standingBelowMps * standingBelowMps
            ? PersonCatalog.WalkColumn(people.DistanceWalkedM[person], heightM)
            : 0;

        // Upright, always: the art draws every facing and none of it turns with the body.
        return new SpriteInstance(
            people.PositionM[person], halfSizeM, SpriteCell.Of(column, row, PersonCatalog.WalkColumns, PersonCatalog.FacingRows),
            down ? CasualtyTint : SpriteTint.Plain, (uint)variant, 0f, ring: ring);
    }
}
