using TrafficSimulation.World.Routing;
using Xunit;

namespace TrafficSimulation.Tests.Routing;

/// <summary>
/// What a link is priced at while the table's slots are spent, recycled and read against a clock — the
/// answer the search sees, whichever slots it walked to find it.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P4)]
public class LinkSurchargesTests
{
    /// <summary>
    /// <b>A slot whose mark ran out and was then marked again is priced as its new mark</b>, beside the marks
    /// still live in the full table.
    /// </summary>
    [Fact]
    public void ASlotMarkedAgainAfterItsMarkRanOutIsPricedAsTheNewMark()
    {
        var marks = new LinkSurcharges(2);
        marks.Advance(0f);
        marks.Mark(1, priceM: 100f, forS: 1f);
        marks.Mark(2, priceM: 200f, forS: 10f);

        marks.Advance(2f);
        marks.Mark(3, priceM: 300f, forS: 10f);

        Assert.Equal(200f, marks.PriceM(2));
        Assert.Equal(300f, marks.PriceM(3));
    }

    /// <summary><b>A clock run backwards brings a spent mark back</b>: a mark's life is read against the clock as it now stands.</summary>
    [Fact]
    public void AClockRunBackwardsPricesAMarkAgainThatHadRunOut()
    {
        var marks = new LinkSurcharges(2);
        marks.Advance(0f);
        marks.Mark(1, priceM: 100f, forS: 1f);

        marks.Advance(2f);
        marks.Advance(0.5f);

        Assert.Equal(100f, marks.PriceM(1));
    }
}
