using System.Numerics;
using Silk.NET.Vulkan;

namespace TrafficSimulation.App.Render;

/// <summary>
/// <b>The ground's triangles filed by the band of rows they stand in, part by part</b>, so a frame draws of each part only
/// the triangles whose rows the view reaches — a run of the index buffer a class, written as an indirect draw's first and
/// count — and a traced city's millions of triangles cost a street framing the street's.
/// </summary>
/// <remarks>
/// <para>
/// <b>A part stays a run of the index buffer, in the order the parts are laid</b> (TER-7b): what is filed is the order
/// <em>inside</em> a part, and a part left out is still a part whose draws count nought (<see cref="TownRenderer.ShowGround"/>).
/// </para>
/// <para>
/// <b>A class a doubling of height.</b> A triangle reaching across <c>s</c> bands is in class ⌊log₂ s⌋ (nought for one or
/// two), and inside a class the triangles are in the order of the lowest band each stands in, the mesh's own order among
/// those of one band. A class's triangles reach at most <c>2^(c+1) − 1</c> bands past their lowest, so the view's lowest
/// band less that is where its run starts — exact to within a class's reach, and a fill struck as one long sliver across
/// the town costs a run of its own rather than every frame's whole part.
/// </para>
/// <para>
/// The bands are the town's own grid's rows (SIM-8, <see cref="GroundMesh.BandM"/>), as what stands on the ground is filed.
/// </para>
/// </remarks>
internal sealed class GroundBands
{
    /// <summary>Per part and class, where each band's triangles start in the filed index buffer, and one past the last band.</summary>
    readonly int[][] _starts;

    readonly float _fromYM;
    readonly float _bandM;

    GroundBands(int[][] starts, float fromYM, float bandM, int bands, int classes)
    {
        _starts = starts;
        _fromYM = fromYM;
        _bandM = bandM;
        Bands = bands;
        Classes = classes;
    }

    public int Bands { get; }

    /// <summary>How many classes each part is filed in, which is how many draws a part is recorded as.</summary>
    public int Classes { get; }

    /// <summary>The draws a frame writes: a class of a part apiece, the parts in the order they are laid.</summary>
    public int Draws => GroundParts.Count * Classes;

    /// <summary>
    /// The ground filed: the bands and classes, and <paramref name="indices"/> — the mesh's index buffer with each part's
    /// triangles in filed order, every part where it was.
    /// </summary>
    public static GroundBands Lay(GroundMesh mesh, out uint[] indices)
    {
        var vertices = mesh.Vertices;
        var source = mesh.Indices;
        var leastYM = float.MaxValue;
        var mostYM = float.MinValue;
        foreach (var vertex in vertices)
        {
            leastYM = MathF.Min(leastYM, vertex.PositionM.Y);
            mostYM = MathF.Max(mostYM, vertex.PositionM.Y);
        }

        var bandM = mesh.BandM;
        var bands = vertices.Length == 0 ? 1 : Math.Max(1, (int)((mostYM - leastYM) / bandM) + 1);
        var classes = BitOperations.Log2((uint)Math.Max(1, bands - 1)) + 1;
        var filed = new GroundBands(new int[GroundParts.Count * classes][], vertices.Length == 0 ? 0f : leastYM, bandM, bands, classes);

        indices = new uint[source.Length];
        int[] keys = [];
        var counts = new int[(classes * bands) + 1];
        for (var part = 0; part < GroundParts.Count; part++)
        {
            var tally = mesh.Parts[part];
            var triangles = tally.IndexCount / 3;
            if (keys.Length < triangles) keys = new int[triangles];

            Array.Clear(counts);
            for (var triangle = 0; triangle < triangles; triangle++)
            {
                var at = tally.FirstIndex + (triangle * 3);
                var lowYM = MathF.Min(vertices[(int)source[at]].PositionM.Y, MathF.Min(vertices[(int)source[at + 1]].PositionM.Y, vertices[(int)source[at + 2]].PositionM.Y));
                var highYM = MathF.Max(vertices[(int)source[at]].PositionM.Y, MathF.Max(vertices[(int)source[at + 1]].PositionM.Y, vertices[(int)source[at + 2]].PositionM.Y));
                var low = filed.BandOf(lowYM);
                var reach = filed.BandOf(highYM) - low;
                keys[triangle] = (ClassOf(reach) * bands) + low;
                counts[keys[triangle] + 1]++;
            }

            // A counting sort, stable, so the mesh's own order stands among the triangles of one band.
            for (var key = 0; key < classes * bands; key++) counts[key + 1] += counts[key];

            for (var @class = 0; @class < classes; @class++)
            {
                var starts = new int[bands + 1];
                for (var band = 0; band <= bands; band++) starts[band] = tally.FirstIndex + (3 * counts[Math.Min((@class * bands) + band, classes * bands)]);
                filed._starts[(part * classes) + @class] = starts;
            }

            for (var triangle = 0; triangle < triangles; triangle++)
            {
                var to = tally.FirstIndex + (3 * counts[keys[triangle]]++);
                source.Slice(tally.FirstIndex + (triangle * 3), 3).CopyTo(indices.AsSpan(to, 3));
            }
        }

        return filed;
    }

    /// <summary>
    /// A frame's draws of the parts in <paramref name="shown"/>, each class's run the rows from <paramref name="leastYM"/>
    /// to <paramref name="mostYM"/> reach; a part not shown draws nothing.
    /// </summary>
    public void Write(uint shown, float leastYM, float mostYM, Span<DrawIndexedIndirectCommand> into)
    {
        var lowest = BandOf(leastYM);
        var highest = BandOf(mostYM);
        for (var part = 0; part < GroundParts.Count; part++)
        {
            var showing = (shown & (1u << part)) != 0;
            for (var @class = 0; @class < Classes; @class++)
            {
                var starts = _starts[(part * Classes) + @class];
                var from = starts[Math.Max(0, lowest - ((2 << @class) - 1))];
                var count = showing ? starts[highest + 1] - from : 0;
                into[(part * Classes) + @class] = new DrawIndexedIndirectCommand
                {
                    IndexCount = (uint)count, InstanceCount = 1, FirstIndex = (uint)from,
                };
            }
        }
    }

    /// <summary>Every row of every shown part — a frame with no view to cull by, the menu's.</summary>
    public void WriteWhole(uint shown, Span<DrawIndexedIndirectCommand> into) =>
        Write(shown, _fromYM, _fromYM + (Bands * _bandM), into);

    int BandOf(float yM) => Math.Clamp((int)MathF.Floor((yM - _fromYM) / _bandM), 0, Bands - 1);

    static int ClassOf(int reach) => reach <= 1 ? 0 : BitOperations.Log2((uint)reach);
}
