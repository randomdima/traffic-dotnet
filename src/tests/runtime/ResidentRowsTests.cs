using TrafficSimulation.Runtime;
using Xunit;

namespace TrafficSimulation.Tests.Runtime;

/// <summary>
/// <b>Rows marked in more places than a send copies are copied by a kernel</b> (<see cref="ResidentRows{T}.Send"/>), and
/// land where they lie as a copy would land them, every row left unmarked untouched — a second send of the same table in
/// one recording among them.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P2)]
public class ResidentRowsTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    /// <summary>Enough items that every other one is many more runs than a send copies.</summary>
    const int Items = 96;

    /// <summary>What every row holds before the sends asked of: laid whole, one run.</summary>
    const uint Laid = 0xDEADu;

    [Fact]
    public void EveryRowMarkedInManyPlacesLandsWhereItLies()
    {
        using var rows = new ResidentRows<uint>(device.Vk, Items, 1, readsBack: true);
        using var batch = new ComputeBatch(device.Vk);
        var expected = LaidWhole(rows, batch);

        var commands = batch.Begin();
        Mark(rows, expected, first: 0);
        rows.Send(commands);
        device.Vk.AfterTransfer(commands);
        rows.Read(commands, Items);
        batch.SubmitAndWait();

        Assert.Equal(expected, rows.AllRead.ToArray());
    }

    [Fact]
    public void ASecondSendInOneRecordingLandsAsWellAsTheFirst()
    {
        using var rows = new ResidentRows<uint>(device.Vk, Items, 1, readsBack: true);
        using var batch = new ComputeBatch(device.Vk);
        var expected = LaidWhole(rows, batch);

        var commands = batch.Begin();
        Mark(rows, expected, first: 0);
        rows.Send(commands);
        Mark(rows, expected, first: 1);
        rows.Send(commands);
        device.Vk.AfterTransfer(commands);
        rows.Read(commands, Items);
        batch.SubmitAndWait();

        Assert.Equal(expected, rows.AllRead.ToArray());
    }

    /// <summary>Every row laid as <see cref="Laid"/> and sent, one run; what the device then holds.</summary>
    uint[] LaidWhole(ResidentRows<uint> rows, ComputeBatch batch)
    {
        rows.All.Fill(Laid);
        rows.Mark(0, Items);
        var commands = batch.Begin();
        rows.Send(commands);
        batch.SubmitAndWait();
        return Enumerable.Repeat(Laid, Items).ToArray();
    }

    /// <summary>Every other item from <paramref name="first"/> written its own number and marked, a run each.</summary>
    static void Mark(ResidentRows<uint> rows, uint[] expected, int first)
    {
        for (var item = first; item < Items; item += 2)
        {
            expected[item] = (uint)(item + 1);
            rows.Rows(item)[0] = expected[item];
            rows.Mark(item);
        }
    }
}
