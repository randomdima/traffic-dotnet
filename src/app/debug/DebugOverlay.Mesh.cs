using System.Numerics;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;

namespace TrafficSimulation.App.Debug;

/// <summary>
/// The ground as the renderer holds it (OBS-2o): every triangle of the town's one mesh, drawn as the
/// three edges it has.
/// </summary>
internal sealed partial class DebugOverlay
{
    /// <summary>
    /// <b>A hairline, and drawn at the same weight the collision layer is</b> — both are edges read
    /// against the picture underneath them rather than lines to be followed, and a stroke heavy enough
    /// to see from across the town buries the cut it is drawn to show.
    /// </summary>
    const float WireLineM = CollisionLineM;

    /// <summary>
    /// <b>Held thinner on screen than the collision layer is</b>, though. That one is read as one edge
    /// against the bodywork under it and is held above a pixel so it cannot dot; this is read as a net,
    /// where a hundred edges meet inside a few pixels at the middle of every fan — and half a pixel of
    /// extra width there is the difference between a triangulation and a white blot.
    /// </summary>
    const float WireLineFloorPx = 1f;

    /// <summary>
    /// How large a triangle has to come out on the glass before it is drawn at all. <b>Under it a mesh
    /// is a wash and not a wireframe</b>: nothing about where the cuts fell can be read out of it, and
    /// at a town-wide framing it costs the whole buffer to say so. Pulling the camera back thins the
    /// picture out instead of filling it, which is the honest answer to whether this can be read from
    /// here.
    /// </summary>
    const float LeastTrianglePx = 4f;

    /// <summary>
    /// <b>Every triangle the mesh holds and not a class of them</b> — ground, kerb rim, dash and zebra
    /// stripe alike. The mesh knows where the paint starts
    /// (<see cref="GroundMesh.FirstMarkVertex"/>) and this layer does not ask: a wireframe drawn over a
    /// subset is a picture of the filter rather than of the triangulation.
    /// </summary>
    /// <remarks>
    /// An edge two triangles share is drawn twice, which is cheaper than remembering which edges have
    /// been drawn — and the diagonal across a quad is the whole reading here, so nothing that would be
    /// deduplicated could be dropped anyway.
    /// </remarks>
    static void Wireframe(
        ref ScreenDraw draw, GroundMesh mesh, Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre)
    {
        var lineM = MathF.Max(WireLineM, WireLineFloorPx / pixelsPerMetre);
        var leastM = LeastTrianglePx / pixelsPerMetre;
        var vertices = mesh.Vertices;
        var indices = mesh.Indices;

        for (var at = 0; at + 2 < indices.Length; at += 3)
        {
            var a = vertices[(int)indices[at]].PositionM;
            var b = vertices[(int)indices[at + 1]].PositionM;
            var c = vertices[(int)indices[at + 2]].PositionM;

            var minM = Vector2.Min(a, Vector2.Min(b, c));
            var maxM = Vector2.Max(a, Vector2.Max(b, c));
            var sizeM = maxM - minM;
            if (MathF.Max(sizeM.X, sizeM.Y) < leastM) continue;
            if (!OnScreen((minM + maxM) * 0.5f, viewCentreM, viewSpanM, sizeM.Length() * 0.5f)) continue;

            draw.LineM(a, b, lineM, Theme.Wireframe);
            draw.LineM(b, c, lineM, Theme.Wireframe);
            draw.LineM(c, a, lineM, Theme.Wireframe);

            // A city's mesh is more triangles than the buffer holds at any framing that admits them all,
            // and every one past the last that fits costs a cull and three dropped writes. The picture is
            // already truncated by the time this is true.
            if (draw.Full) return;
        }
    }
}
