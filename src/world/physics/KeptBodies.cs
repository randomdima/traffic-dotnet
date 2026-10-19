using System.Numerics;

namespace TrafficSimulation.World.Physics;

/// <summary>
/// <b>The body table as it stood when a view was written</b>, in the rows the kernels read (<see cref="BodyRows"/>): every
/// body of every kind in the one numbering, kept for an instrument that reads the bodies where the view shows them rather
/// than where a step in flight has them.
/// </summary>
/// <remarks>
/// Written by the step as it writes its view, and read by the instrument only while that view is the one shown, so never
/// both at once. What each body is never changes, and is laid once.
/// </remarks>
internal sealed class KeptBodies
{
    /// <param name="furnitureCount">How many rows the furniture stands in, first: never written again once laid.</param>
    public KeptBodies(PhysicsWorld physics, int furnitureCount)
    {
        FurnitureCount = furnitureCount;
        var count = physics.Table.Count;
        Pose = new Vector4[count];
        Shape = new Vector4[count];
        Motion = new Vector4[count];
        Filter = new uint[count];
        Tag = new int[count];
        Links = new LinkRow[physics.LinkSlotCount];
        for (var body = 0; body < count; body++) Tag[body] = physics.TagOf(new BodyId(body + 1)).Packed;
    }

    public int FurnitureCount { get; }

    public Vector4[] Pose { get; }
    public Vector4[] Shape { get; }
    public Vector4[] Motion { get; }
    public uint[] Filter { get; }

    /// <summary>What each body is, <see cref="BodyTag.Packed"/>.</summary>
    public int[] Tag { get; }

    /// <summary>The live links first, <see cref="LinkCount"/> of them.</summary>
    public LinkRow[] Links { get; }

    public int LinkCount { get; private set; }

    /// <summary>Rows <paramref name="from"/> onwards of the table as it stands, and every link.</summary>
    public void Keep(in BodyTable table, int from)
    {
        for (var body = from; body < table.Count; body++)
        {
            Pose[body] = BodyRows.Pose(table, body);
            Shape[body] = BodyRows.Shape(table, body);
            Motion[body] = BodyRows.Motion(table, body);
            Filter[body] = BodyRows.Filter(table, body);
        }

        table.Links.CopyTo(Links);
        LinkCount = table.Links.Length;
    }
}
