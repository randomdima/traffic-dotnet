using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>A world reads a car on the lanes it follows where the car's own line would put it</b> (<see cref="LineReading"/>):
/// the same progress and the same distance off, to the bit, as projecting onto the line laid over those lanes.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P3)]
public class LineReadingTests
{
    static readonly SimConfig Config = SimConfig.Shipped();
    static readonly RoadGraph Roads = RoadGraph.Build(Towns.Of(Towns.City), Config);

    /// <summary>A car's own window either side of where it was (<c>CarBuild.ProjectionWindowM</c>), for a car 4.5 m long.</summary>
    const float WindowM = 9f;

    const float StepM = 0.7f;
    const float AsideM = 0.6f;
    const float AroundOffM = 0.3f;

    [Fact]
    public void ACarIsReadWhereItsLineWouldPutIt()
    {
        var arcs = new ArcSeg[LineAssembler.ArcsFor(Roads)];
        var starts = new float[LineAssembler.MostLanes];
        var ends = new float[LineAssembler.MostLanes];
        Span<int> pair = stackalloc int[2];
        var read = 0;

        for (var lane = 0; lane < Roads.LaneCount; lane++)
        {
            var onto = Roads.LanesFrom(lane);
            pair[0] = lane;

            // One lane whole and cut short, and a lane with the join and the next lane after it.
            read += Compared(pair[..1], float.PositiveInfinity);
            read += Compared(pair[..1], Roads.LaneLengthM[lane] * 0.5f);
            if (onto.Length == 0) continue;

            pair[1] = onto[lane % onto.Length];
            read += Compared(pair, float.PositiveInfinity);
            read += Compared(pair, Roads.LaneLengthM[pair[1]] * 0.5f);
        }

        Assert.True(read > 0);

        int Compared(ReadOnlySpan<int> lanes, float lastLaneToM)
        {
            var laid = LineAssembler.Assemble(Roads, lanes, arcs, starts, ends, lastLaneToM);
            var line = arcs.AsSpan(0, laid.ArcCount);
            var join = laid.LaneCount > 1 ? Roads.ConnectorBetween(lanes[0], lanes[1]) : RoadGraph.NoConnector;
            var stopsAtM = LineAssembler.LaidToM(Roads, lanes[laid.LaneCount - 1], laid.LaneCount == lanes.Length, lastLaneToM);

            var count = 0;
            for (var alongM = 0f; alongM <= laid.LengthM; alongM += StepM)
            {
                var on = Spline.SampleAt(line, alongM);
                var aside = (count % 3 - 1) * AsideM;
                var pointM = on.PositionM + (on.Right * aside);
                var aroundM = alongM + ((count % 2 == 0 ? 1 : -1) * AroundOffM);

                var expectedM = Spline.ProjectM(line, pointM, aroundM, WindowM);
                var cursor = default(SplineCursor);
                var expectedOffM = (Spline.SampleFrom(line, expectedM, ref cursor).PositionM - pointM).Length();

                var followed = new LineFollowed(lanes[0], join, stopsAtM, aroundM, serial: 1);
                LineReading.Read(Roads, ref followed, pointM, WindowM);

                Assert.Equal(BitConverter.SingleToInt32Bits(expectedM), BitConverter.SingleToInt32Bits(followed.ProgressM));
                Assert.Equal(BitConverter.SingleToInt32Bits(expectedOffM), BitConverter.SingleToInt32Bits(followed.OffLineM));
                count++;
            }

            return count;
        }
    }
}
