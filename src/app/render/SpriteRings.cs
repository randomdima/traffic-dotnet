using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Render;

/// <summary>The ring a unit's picture wears, as its instance carries it above its sheet (<see cref="SpriteInstance.Ring"/>).</summary>
internal enum SpriteRing : byte
{
    None,

    /// <summary>CTL-1: picked out by the reader.</summary>
    Selected,

    /// <summary>CTL-5d: the car somebody else has the wheel of, and the reader has not picked out.</summary>
    SecondHand,
}

/// <summary>
/// <b>Which units wear a ring this frame</b> (CTL-1, CTL-5d), as the host's fill and the device's emit both read it
/// (<c>sprites.glsl</c>'s <c>Rings</c>): a word naming the car somebody else has the wheel of, then a bit a unit — the
/// walkers' to their roster's capacity, the cars' to theirs, then the rail vehicles' as the view holds them.
/// </summary>
/// <remarks>
/// <para>
/// <b>The ring is drawn by the unit's own sprite</b> — the quad, the sheet and the cell its body is drawn with — grown
/// by the ring's width and banded just outside the picture's solid part (<c>quad.glsl</c>, <c>sprite.frag</c>). So it
/// follows the very frame on screen, a walker's stride and a car's tilt with it, and there is no second answer to what a
/// unit looks like to drift from the first.
/// </para>
/// <para>
/// <b>A shape and not a tint.</b> A brighter sprite says "selected" only against the sprite beside it: on a white van,
/// at a district framing, or in a queue of one make it says nothing at all. A ring is readable off a single unit, and it
/// leaves the art the colour it was drawn. One ring a unit however many are picked out (CTL-1b): a group is read off the
/// ring round each of its members, not off a hull drawn round the lot.
/// </para>
/// </remarks>
internal readonly ref struct SpriteRings(ReadOnlySpan<uint> words, int people, int cars)
{
    readonly ReadOnlySpan<uint> _words = words;

    public SpriteRing OfPerson(int person) => Marked(0, person) ? SpriteRing.Selected : SpriteRing.None;

    public SpriteRing OfCar(int car) =>
        Marked(WordsFor(people), car) ? SpriteRing.Selected
        : !_words.IsEmpty && (int)_words[0] == car ? SpriteRing.SecondHand
        : SpriteRing.None;

    /// <param name="wagon">The rail vehicle's index in the view (<see cref="WorldView.Wagons"/>).</param>
    public SpriteRing OfWagon(int wagon) => Marked(WordsFor(people) + WordsFor(cars), wagon) ? SpriteRing.Selected : SpriteRing.None;

    bool Marked(int from, int unit) => !_words.IsEmpty && ((_words[1 + from + (unit >> 5)] >> (unit & 31)) & 1u) != 0;

    static int WordsFor(int units) => (units + 31) >> 5;

    /// <summary>How many words a town's rings take.</summary>
    public static int Length(int people, int cars, int wagons) => 1 + WordsFor(people) + WordsFor(cars) + WordsFor(wagons);

    /// <summary>The town's for <paramref name="world"/>'s selection and second hand, into <paramref name="into"/>, <see cref="Length"/> long.</summary>
    public static void Mark(TownWorld world, Span<uint> into)
    {
        into.Clear();
        into[0] = (uint)world.HandDrivenCar;
        var carsFrom = 1 + WordsFor(world.People.Capacity);
        var wagonsFrom = carsFrom + WordsFor(world.Cars.Capacity);
        foreach (var unit in world.Selected)
        {
            switch (unit.Kind)
            {
                case SelectionKind.Person:
                    Set(into, 1, unit.Index);
                    break;
                case SelectionKind.Car:
                    Set(into, carsFrom, unit.Index);
                    break;
                case SelectionKind.Locomotive or SelectionKind.Wagon:
                    Set(into, wagonsFrom, world.WagonOf(unit));
                    break;
            }
        }
    }

    /// <summary>
    /// One unit picked out and nothing else — a player's own (MP-9): a walker, a car, or a rail vehicle by its slot — into
    /// <paramref name="into"/>, <see cref="Length"/> long for <paramref name="people"/> walkers and <paramref name="cars"/>
    /// cars; nothing at all for no unit.
    /// </summary>
    public static void MarkOne(Span<uint> into, int people, int cars, Selection unit)
    {
        into.Clear();
        into[0] = uint.MaxValue;
        if (!unit.Any) return;

        var carsFrom = 1 + WordsFor(people);
        switch (unit.Kind)
        {
            case SelectionKind.Person:
                Set(into, 1, unit.Index);
                break;
            case SelectionKind.Car:
                Set(into, carsFrom, unit.Index);
                break;
            case SelectionKind.Wagon:
                Set(into, carsFrom + WordsFor(cars), unit.Index);
                break;
        }
    }

    static void Set(Span<uint> into, int from, int unit) => into[from + (unit >> 5)] |= 1u << (unit & 31);
}

/// <summary>
/// <b>How a ring is drawn</b> (<see cref="CameraView.Rings"/>): the selection's hue, the second hand's and the dark edge's,
/// each packed as <see cref="SpriteTint"/> packs a tint, then the ring's width and the edge's in interface pixels as two
/// halves — so it reads on a lawn as well as on a road, at any zoom.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct RingLook(uint Selected, uint SecondHand, uint Edge, uint WidthsPx)
{
    /// <summary>
    /// The look at one frame's zoom, its ring <see cref="ViewFigures.SelectionRingPx"/> or
    /// <see cref="ViewFigures.SelectionRingM"/> of the town, whichever is wider; the zoom read as the vertex stage reads
    /// it (<c>quad.glsl</c>'s <c>pxPerM</c>); the selection's in <paramref name="selected"/>, which a player's page makes its
    /// own colour (MP-15).
    /// </summary>
    public static RingLook At(ViewFigures view, Vector2 clipPerM, Vector2 uiPx, Vector4 selected)
    {
        var pxPerM = MathF.Abs(clipPerM.X) * uiPx.X * 0.5f;
        var ringPx = MathF.Max(view.SelectionRingPx, view.SelectionRingM * pxPerM);
        return new(
            SpriteTint.Pack(selected), SpriteTint.Pack(Theme.SecondHandMark), SpriteTint.Pack(Theme.MarkOutline),
            Halves.Pack(new Vector2(ringPx, view.SelectionRingEdgePx)));
    }
}
