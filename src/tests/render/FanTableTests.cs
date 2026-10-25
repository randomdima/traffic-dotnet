using System.Numerics;
using TrafficSimulation.Agents.Train.Body;
using TrafficSimulation.App.Render;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Statics;
using Xunit;

namespace TrafficSimulation.Tests.Render;

/// <summary>
/// The fans as the sprite stages read them (OBJ-6, TRN-13): a roof's or a vehicle's sheet names its own fans and no other
/// sheet names any, and what turns covers what the art drew.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P9)]
public class FanTableTests
{
    const int FirstRoofSheet = 100;

    static readonly BuildingCatalog Roofs = BuildingCatalog.Shared;

    static readonly TrainCatalog Vehicles = TrainCatalog.Shared;

    static int FirstVehicleSheet => FirstRoofSheet + Roofs.Count;

    static readonly uint[] Table = FanTable.Of(Roofs, FirstRoofSheet, Vehicles, FirstVehicleSheet);

    /// <summary>Every picture that may draw fans: its sheet, its name, the footprint it is drawn at and its fans.</summary>
    static IEnumerable<(int Sheet, string Id, Vector2 FootprintM, DrawnFan[] Fans)> Pictures()
    {
        for (var variant = 0; variant < Roofs.Count; variant++)
        {
            var roof = Roofs.Variants[variant];
            yield return (FirstRoofSheet + variant, roof.Id, roof.FootprintM, roof.SpritePath is null ? [] : roof.Fans);
        }

        for (var vehicle = 0; vehicle < Vehicles.Vehicles.Length; vehicle++)
        {
            var drawn = Vehicles.Vehicles[vehicle];
            yield return (FirstVehicleSheet + vehicle, drawn.Id, drawn.FootprintM, drawn.SpritePath is null ? [] : drawn.Fans);
        }
    }

    [Fact]
    public void EveryPicturesSheetNamesItsOwnFansAndNoOtherSheetNamesAny()
    {
        var drawn = new int[SheetAtlas.Slots];
        foreach (var picture in Pictures()) drawn[picture.Sheet] = picture.Fans.Length;

        for (var sheet = 0; sheet < SheetAtlas.Slots; sheet++) Assert.Equal(drawn[sheet], (int)(Table[sheet] >> 16));
    }

    /// <summary>
    /// <b>The circle turned is round and covers all of the opening the art drew</b>, every way out of the hub: short of the
    /// rim anywhere, what lies between stands still round the turning blades.
    /// </summary>
    [Fact]
    public void AFansTurnedCircleIsRoundAndCoversItsDrawnOpening()
    {
        foreach (var (sheet, id, footprintM, fans) in Pictures())
        {
            var run = Table[sheet];
            for (var fan = 0; fan < fans.Length; fan++)
            {
                var at = SheetAtlas.Slots + ((int)((run & 0xffff) + fan) * 8);
                var radiusM = BitConverter.UInt32BitsToSingle(Table[at + 2]) * footprintM.X;
                Assert.Equal(radiusM, BitConverter.UInt32BitsToSingle(Table[at + 3]) * footprintM.Y, 1e-4f);

                for (var degree = 0; degree < 360; degree++)
                {
                    var reachM = fans[fan].RimReachM(Heading.Unit(degree * MathF.PI / 180f));
                    Assert.True(reachM <= radiusM, $"{id}'s fan {fan}'s rim reaches {reachM} m at {degree}°, past its circle's {radiusM} m");
                }
            }
        }
    }
}
