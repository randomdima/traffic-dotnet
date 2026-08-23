using TrafficSimulation.CityGen;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// The format's two halves held to each other. <b>Every shipped town is written back out and read
/// again</b>, which is the only check that says the writer lays the fields the reader takes — in the
/// same order, at the same widths, with the same sentinel where a record points at nothing.
/// </summary>
/// <remarks>
/// Asked as bytes and not field by field: a plan compared property by property would pass a writer that
/// swapped two floats of the same width, and the file is what the next process reads.
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
public class TownWriterTests
{
    public static TheoryData<string> Maps => Towns.EveryShippedMap();

    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryShippedTownSurvivesBeingWrittenBackOut(string map)
    {
        var plan = Towns.Of(map);

        var written = TownWriter.Write(plan);
        var again = TownReader.Read(written, $"{map} written back out");

        Assert.Equal(plan.Name, again.Name);
        Assert.Equal(TownWriter.Write(again), written);
    }

    /// <summary>
    /// <b>And it is the file's own bytes</b> and not merely a shape that survives a round trip through
    /// this pair: the town on disk, written from what was read out of it, comes back the same file.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void WhatIsWrittenIsTheFileThatWasRead(string map)
    {
        var onDisk = File.ReadAllBytes(TrafficSimulation.Core.Config.ProjectPaths.TownFile(map));

        Assert.Equal(onDisk, TownWriter.Write(Towns.Of(map)));
    }
}
