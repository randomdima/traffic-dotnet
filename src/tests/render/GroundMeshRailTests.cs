using System.Numerics;
using TrafficSimulation.App.Render;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Rail;
using Xunit;

namespace TrafficSimulation.Tests.Render;

/// <summary>
/// <b>The railway as the ground draws it</b> (RAIL-3): the suite's own city with a double track across it, its strips under
/// the roads that cross them and the rails over those roads.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P5)]
[Collection(TownGeometryCollection.Name)]
public class GroundMeshRailTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>How far inside a crossing's walk the place asked about stands.</summary>
    const float InsideTheWalkM = 0.3f;

    static readonly Lazy<(CityPlan Plan, GroundMesh Mesh)> Laid = new(() =>
    {
        var plan = Towns.LayFresh(Towns.Brief(Towns.CitySeed, buildings: 120, railTracks: 2));
        return (plan, GroundMesh.Build(plan, Config));
    });

    /// <summary>
    /// Across a road and its walk the track's strip is laid whole under them, and each rail's own band of it over them: the
    /// track, then the road, then the rails (the owner, 2026-10-19).
    /// </summary>
    [Fact]
    public void ACrossingRoadIsLaidOverTheTrackAndTheRailsOverTheRoad()
    {
        var (plan, mesh) = Laid.Value;
        var crossings = LevelCrossings.Of(plan, Config);
        var tracks = TrackLine.Of(plan.Rails);
        var (under, over) = (TrackTriangles(mesh, GroundPart.Track), TrackTriangles(mesh, GroundPart.Rails));
        var railOffM = (Config.Rail.GaugeM + Config.Rail.RailHeadWidthM) * 0.5f;
        Assert.NotEqual(0, crossings.Count);

        for (var meeting = 0; meeting < crossings.Count; meeting++)
        {
            var track = tracks[crossings.Track[meeting]];
            var along = track.AlongAt(crossings.AlongTrackM[meeting]);
            var across = new Vector2(-along.Y, along.X);
            // On the walk, short of its outer edge where the rails stand off the line: the road's edge runs at a slant to the track.
            var walkM = crossings.HalfOverM(plan, meeting) - (railOffM * MathF.Abs(SkewOf(plan, crossings, track, meeting))) - InsideTheWalkM;
            foreach (var overM in (ReadOnlySpan<float>)[0f, walkM, -walkM])
            {
                var lineM = track.PlaceAt(crossings.AlongTrackM[meeting] + overM);
                Assert.True(Covered(under, lineM), $"meeting {meeting}: the strip is not laid under the road {overM:F1} m along the track from its middle");
                Assert.True(Covered(over, lineM + (across * railOffM)) && Covered(over, lineM - (across * railOffM)), $"meeting {meeting}: a rail is not laid over the road {overM:F1} m along");
            }
        }
    }

    /// <summary>How far along the track the road's edge moves for a metre off the track's line, either way.</summary>
    static float SkewOf(CityPlan plan, LevelCrossings crossings, TrackLine track, int meeting)
    {
        var road = Core.Geometry.Spline.SampleAt(plan.Roads.SegmentsOf(crossings.Road[meeting]), crossings.AlongRoadM[meeting]).Direction;
        var along = track.AlongAt(crossings.AlongTrackM[meeting]);
        return Vector2.Dot(road, along) / Vector2.Dot(road, new Vector2(-along.Y, along.X));
    }

    static List<Vector2[]> TrackTriangles(GroundMesh mesh, GroundPart part)
    {
        var vertices = mesh.Vertices;
        var indices = mesh.Indices;
        var laid = mesh.Parts[(int)part];
        var triangles = new List<Vector2[]>();
        for (var at = laid.FirstIndex; at + 2 < laid.FirstIndex + laid.IndexCount; at += 3)
        {
            if (vertices[(int)indices[at]].Surface != Surface.Track) continue;

            triangles.Add([vertices[(int)indices[at]].PositionM, vertices[(int)indices[at + 1]].PositionM, vertices[(int)indices[at + 2]].PositionM]);
        }

        return triangles;
    }

    static bool Covered(List<Vector2[]> triangles, Vector2 pointM)
    {
        foreach (var triangle in triangles)
        {
            var (a, b, c) = (Cross(triangle[0], triangle[1], pointM), Cross(triangle[1], triangle[2], pointM), Cross(triangle[2], triangle[0], pointM));
            if ((a >= 0f && b >= 0f && c >= 0f) || (a <= 0f && b <= 0f && c <= 0f)) return true;
        }

        return false;
    }

    static float Cross(Vector2 fromM, Vector2 toM, Vector2 pointM) => ((toM.X - fromM.X) * (pointM.Y - fromM.Y)) - ((toM.Y - fromM.Y) * (pointM.X - fromM.X));
}
