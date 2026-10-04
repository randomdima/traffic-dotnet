using TrafficSimulation.CityGen.Traced;
using Xunit;

namespace TrafficSimulation.Tests.CityGen.Traced;

/// <summary>
/// <b>The menu describes a traced map off the head of its file alone</b> (<see cref="SurveyHead"/>): a page
/// holds nothing else of one until it is opened, so the map has to have written the description inside it.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class SurveyHeadTests
{
    [Fact]
    public void TheDescriptionIsReadOffTheHeadAlone()
    {
        // Two thousand points put the map well past its head, so the cut below is a map cut short.
        const int Points = 2000;
        var map = new TracedMap
        {
            Name = "Surveyed",
            Description = "a few ways laid by hand",
            Licence = "",
            OsmBase = "",
            Relation = 1,
            Frame = new OsmFrame { Lat0Deg = 0, Lon0Deg = 0, WestM = 0, SouthM = 0, WidthM = 10, HeightM = 10, MarginM = 0 },
            PointM = [.. Enumerable.Range(0, Points).Select(point => new Vector2D(point * 3.7, point * 1.3))],
            Roads = [],
            Coast = [],
            Turns = OsmTurns.None,
            Controls = TracedMap.ControlArrays.None,
            Crossings = TracedMap.CrossingArrays.None,
            Footprints = TracedMap.FootprintArrays.None,
            TreeM = [],
        };

        using var written = new MemoryStream();
        map.Write(written);
        var head = Scratch.Write("survey-head.map", written.GetBuffer().AsSpan(0, SurveyHead.Bytes));

        Assert.Equal("a few ways laid by hand", SurveyHead.Description(head));
    }
}
