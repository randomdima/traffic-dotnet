using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.CityGen.Traced;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>The marks the device works out are the host's within a millimetre</b> (<see cref="DeviceMarks"/>, TER-5c) — but for
/// where a section begins or ends at a way's trimmed end: two ways leaving one place touch along their trimmed ends' lines
/// alone, and whether a touch along a line is ground in common falls to the rounding, the host's doubles and the device's
/// floats on either side of it. Over the fixture town, a city laid from its brief, and a deck over a street, whose ways
/// carry channels.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class MarksKernelTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>How far a device's section may end from the host's.</summary>
    const float WithinM = 0.001f;

    [Theory]
    [InlineData("fixture")]
    [InlineData("city")]
    [InlineData("over a street")]
    public void TheMarksTheDeviceWorksOutAreTheHostsWithinAMillimetreButAtATouch(string town)
    {
        using var world = new TownWorld(
            town switch
            {
                "fixture" => Towns.Of(Towns.Fixture),
                "city" => Towns.Of(Towns.City),
                _ => TracedPlanTests.OverAStreet(),
            },
            Config);
        var host = world.Atlas.Marks;
        var lines = world.Lines;
        var lengthM = RibbonAtlas.Filing(lines, Config.RibbonLevel, anyoneWalks: world.People.Capacity > 0).LengthM;

        using var marking = new DeviceMarks(device.Vk).Mark(
            RibbonMarks.Filing(lines, Config.RibbonLevel.CellM, Config.RibbonTouchM, Config.RibbonLevel.Grid.Main));
        while (!marking.Answered())
        {
        }

        var marks = RibbonMarks.OfRows(lines, marking.Rows, lengthM);

        // Whether a metre of a way is one of its trimmed ends, where its ground begins or stops.
        bool AtAnEnd(int way, float atM) =>
            Math.Abs(atM - Config.RibbonTouchM) <= WithinM || Math.Abs(atM - (lengthM[way] - Config.RibbonTouchM)) <= WithinM;

        // Whether a section begins or ends at a trimmed end of either of its ways.
        bool AtATouch(int way, in CrossedSection section) =>
            AtAnEnd(way, section.MineFromM) || AtAnEnd(way, section.MineToM) || AtAnEnd(section.OnWay, section.FromM)
            || AtAnEnd(section.OnWay, section.ToM);

        static bool Within(in CrossedSection one, in CrossedSection other) =>
            Math.Abs(one.FromM - other.FromM) <= WithinM && Math.Abs(one.ToM - other.ToM) <= WithinM
            && Math.Abs(one.MineFromM - other.MineFromM) <= WithinM && Math.Abs(one.MineToM - other.MineToM) <= WithinM;

        var (sections, apart) = (0, 0);
        for (var way = 0; way < lines.WayCount; way++)
        {
            var got = marks.Of(way).ToArray().ToDictionary(static section => section.OnWay);
            foreach (var wanted in host.Of(way))
            {
                sections++;
                if (!got.Remove(wanted.OnWay, out var section))
                {
                    Assert.True(AtATouch(way, wanted), $"way {way} against {wanted.OnWay}: the host marks {wanted} and the device nothing");
                    apart++;
                    continue;
                }

                if (Within(wanted, section)) continue;

                Assert.True(
                    AtATouch(way, wanted) || AtATouch(way, section),
                    $"way {way} against {wanted.OnWay}: the host marks {wanted}, the device {section}");
                apart++;
            }

            foreach (var (_, section) in got)
            {
                Assert.True(AtATouch(way, section), $"way {way} against {section.OnWay}: the device marks {section} and the host nothing");
                apart++;
            }
        }

        // The touches are the exception and not the town: most of what is marked is marked alike.
        Assert.True(apart * 20 <= sections, $"{apart} of {sections} sections stand apart at a touch");
    }
}
