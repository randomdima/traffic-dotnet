using System.Reflection;
using System.Xml.Linq;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Config;

/// <summary>
/// <b>The page keeps every figure group the shared figures can name</b>: <c>figures.trim.xml</c> preserves exactly the
/// groups <see cref="SharedFiguresReader"/> reaches from <see cref="SimConfig"/>, so a group added to the figures is not
/// trimmed out of the browser head, where its setters would be gone and the file naming it refused.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P8)]
public class FiguresTrimTests
{
    [Fact]
    public void TheTrimmedBuildKeepsExactlyTheFigureGroups()
    {
        var groups = new SortedSet<string>(StringComparer.Ordinal);
        Reach(typeof(SimConfig), groups);

        var kept = new SortedSet<string>(
            XDocument.Load(Path.Combine(ProjectPaths.Root, "src", "core", "config", "figures.trim.xml"))
                .Descendants("type").Select(type => (string)type.Attribute("fullname")!),
            StringComparer.Ordinal);

        Assert.True(groups.SetEquals(kept),
            $"not kept: {string.Join(", ", groups.Except(kept))}; kept and no group: {string.Join(", ", kept.Except(groups))}");
    }

    /// <summary>A group, and every group a property of it holds — what the reader can walk into by name.</summary>
    static void Reach(Type group, SortedSet<string> groups)
    {
        if (!groups.Add(group.FullName!)) return;
        foreach (var figure in group.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var type = figure.PropertyType;
            if (type.IsClass && type != typeof(string) && !type.IsArray) Reach(type, groups);
        }
    }
}
