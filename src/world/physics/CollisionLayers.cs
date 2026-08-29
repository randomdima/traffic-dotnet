
using System.Numerics;

namespace TrafficSimulation.World.Physics;

/// <summary>Which layer a body is on. One bit each, and the whole of what a filter is made of.</summary>
[Flags]
internal enum CollisionLayer : ulong
{
    None = 0,

    /// <summary>Buildings, props and the town's furniture: found, never looking.</summary>
    Static = 1UL << 0,

    Person = 1UL << 1,
    Car = 1UL << 2,

    /// <summary>
    /// A body already lying in the road (PHY-5b): the town's traffic passes through it and it through the
    /// traffic. The ground it fetched up against is all that is left holding it.
    /// </summary>
    Downed = 1UL << 3,
}

/// <summary>
/// The layers and masks, under one rule: <b>two bodies interact when <em>either</em> scans the other.</b>
/// </summary>
/// <remarks>
/// <para>
/// What is declared here is who <em>scans</em> whom, which is asymmetric and is meant to be: static
/// geometry scans nothing, because a building does not go looking for the cars that hit it. The solver
/// tests a pair with <c>(catA &amp; maskB) &amp;&amp; (catB &amp; maskA)</c>, so a mask built straight
/// from the scan table would make a car pass clean through every building in the town.
/// </para>
/// <para>
/// The symmetric closure is therefore taken once at startup, which turns that <c>&amp;&amp;</c> into
/// the rule's <c>||</c>: either direction of the declaration puts the bit in both masks, so both halves
/// of the test agree. Asking a predicate per candidate pair instead would decide by running code what a
/// matrix decides by being read, and would put a branch in the broad phase.
/// </para>
/// </remarks>
internal static class CollisionLayers
{
    /// <summary>How many layers there are, which is how wide the matrix is.</summary>
    public const int Count = 4;

    /// <summary>
    /// What each layer goes looking for, in layer order. This is the declaration and the masks are
    /// derived from it — a layer added here is a row, and the closure below makes it a column too.
    /// </summary>
    static readonly CollisionLayer[] Scans =
    [
        CollisionLayer.None,
        CollisionLayer.Static | CollisionLayer.Person | CollisionLayer.Car,
        CollisionLayer.Static | CollisionLayer.Person | CollisionLayer.Car,

        // The one row that scans less than the whole town, and it is the closure that makes it mean
        // anything: neither of the two rows above names Downed, so a casualty is in nobody's mask and
        // nothing but the ground can reach it (PHY-5b).
        CollisionLayer.Static,
    ];

    static readonly CollisionLayer[] Masks = Symmetrise(Scans);

    /// <summary>The mask a body on this layer carries: everything that scans it, and everything it scans.</summary>
    public static CollisionLayer MaskOf(CollisionLayer layer) => Masks[IndexOf(layer)];

    /// <summary>The rule itself, for whatever wants to ask it without a solver in the room.</summary>
    public static bool Interact(CollisionLayer first, CollisionLayer second) => (MaskOf(first) & second) != 0;

    /// <summary>
    /// The scan table's symmetric closure: a layer's mask is what it scans, plus every layer that scans
    /// it. Taken once — this is a startup cost of nine bit tests, not a per-pair decision.
    /// </summary>
    public static CollisionLayer[] Symmetrise(ReadOnlySpan<CollisionLayer> scans)
    {
        var masks = new CollisionLayer[scans.Length];
        for (var layer = 0; layer < scans.Length; layer++)
        {
            masks[layer] = scans[layer];
            for (var other = 0; other < scans.Length; other++)
            {
                if ((scans[other] & Bit(layer)) != 0) masks[layer] |= Bit(other);
            }
        }

        return masks;
    }

    public static CollisionLayer Bit(int layer) => (CollisionLayer)(1UL << layer);

    public static int IndexOf(CollisionLayer layer) => BitOperations.TrailingZeroCount((ulong)layer);
}
