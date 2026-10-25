using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// A told fleet (<see cref="FleetList"/>, <c>--fleet</c>) is named by id. Only the naming is asked here: telling
/// one is once a process, and the suite's catalogue is the shipped fleet's.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P9)]
public class FleetListTests
{
    [Fact]
    public void AnIdIsTheVariantFileOfTheSameName()
    {
        Assert.Equal(
            [Path.Combine(ProjectPaths.Assets, "agents", "car", "variants", "hatch_blue", "hatch_blue.json")],
            FleetList.FilesOf(["hatch_blue"]));
    }

    [Fact]
    public void AnIdWithNoVariantFileIsRefused()
    {
        Assert.Throws<FileNotFoundException>(() => FleetList.FilesOf(["hatch_bluee"]));
    }
}
