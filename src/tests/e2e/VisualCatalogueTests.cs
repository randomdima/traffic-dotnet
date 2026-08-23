using Xunit;

namespace TrafficSimulation.Tests.E2E;

/// <summary>
/// The scenario table itself, checked without opening a rendering device: the mistakes that would
/// otherwise surface as a frame nobody can review, half an hour into a shot run.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class VisualCatalogueTests
{
    [Fact]
    public void NoTwoScenariosShareAName()
    {
        var names = Array.ConvertAll(VisualScenarios.All, scenario => scenario.Name);
        Assert.Equal(names.Length, new HashSet<string>(names).Count);
    }

    /// <summary>A scenario whose reference frame is not in the folder would be reviewed against
    /// nothing, and the review would not say so.</summary>
    [Fact]
    public void EveryReferenceFrameNamedIsOneTheRepositoryHas()
    {
        foreach (var scenario in VisualScenarios.All)
        {
            if (scenario.Expected is null) continue;
            var path = Path.Combine(VisualScenarios.Expected, scenario.Expected);
            Assert.True(File.Exists(path), $"{scenario.Name} names {scenario.Expected}, which is missing");
        }
    }

    /// <summary>No reference frame is kept that no scenario is judged against: an unused picture in
    /// the folder is one a reader will take for part of the specification.</summary>
    [Fact]
    public void EveryReferenceFrameKeptIsOneAScenarioIsJudgedAgainst()
    {
        var used = new HashSet<string>();
        foreach (var scenario in VisualScenarios.All)
            if (scenario.Expected is not null)
                used.Add(scenario.Expected);

        foreach (var file in Directory.GetFiles(VisualScenarios.Expected, "*.png"))
            Assert.Contains(Path.GetFileName(file), used);
    }

    /// <summary>
    /// Every claim is one falsifiable sentence. The length bound is the crude half of that and the
    /// only half a machine can check: a claim that runs past it is two claims joined by "and", which
    /// a reviewer cannot answer yes or no to.
    /// </summary>
    [Fact]
    public void EveryScenarioCarriesClaimsThatCanBeAnsweredOneAtATime()
    {
        foreach (var scenario in VisualScenarios.All)
        {
            Assert.NotEmpty(scenario.Expect);
            foreach (var claim in scenario.Expect)
            {
                Assert.InRange(claim.Length, 20, 260);
                Assert.EndsWith(".", claim);
            }
        }
    }

    /// <summary>
    /// The resolution is derived from what the claims are about, and both ends of that arithmetic
    /// have to stay sane: a frame under the floor is a thumbnail nobody can judge, and one over the
    /// ceiling is paying for pixels no reviewer reads.
    /// </summary>
    [Fact]
    public void EveryScenarioAsksForAFrameAReviewerCanRead()
    {
        foreach (var scenario in VisualScenarios.All)
        {
            var (widthPx, heightPx) = scenario.SizeFor();
            Assert.InRange(widthPx, 192, 1536);
            Assert.InRange(heightPx, 144, 1152);

            if (scenario.FrameWidthM <= 0f) continue;

            // The finest thing the claims name is drawn at enough pixels to say anything honest
            // about it: below about three it is an artefact of sampling.
            Assert.True(scenario.FinestFeatureM * scenario.PxPerM >= 3f,
                $"{scenario.Name} draws its {scenario.FinestFeatureM} m feature at "
                + $"{scenario.FinestFeatureM * scenario.PxPerM:0.#} px");
        }
    }
}
