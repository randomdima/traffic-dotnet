using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// The two lists in one array (SRV-3): the fleet a town's traffic wraps over, and the service variants
/// past the end of it that can only be reached by name.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class CarCatalogTests
{
    static readonly CarCatalog Catalogue = CarCatalog.Load();

    /// <summary>
    /// <b>The wrap cannot reach a service variant.</b> Handing a picture out to the seventeenth ordinary
    /// car is what would put an ambulance on the school run, so the boundary is asserted from both sides.
    /// </summary>
    [Fact]
    public void TheWrapStaysInsideTheFleet()
    {
        Assert.True(Catalogue.Count > 0);
        Assert.True(Catalogue.SheetCount > Catalogue.Count, "there are no service variants past the fleet");

        for (var entry = 0; entry < Catalogue.Count; entry++) Assert.False(Catalogue.IsService(entry));
        for (var entry = Catalogue.Count; entry < Catalogue.SheetCount; entry++) Assert.True(Catalogue.IsService(entry));
    }

    /// <summary>
    /// Each service vehicle is named by id rather than by position, so <c>Service.json</c> can be reordered
    /// without a car changing what it is.
    /// </summary>
    [Fact]
    public void EveryServiceVehicleResolvesToItsOwnVariant()
    {
        Assert.Equal("ambulance_white", Catalogue.Variants[Catalogue.Ambulance].Id);
        Assert.Equal("police_white", Catalogue.Variants[Catalogue.Police].Id);
        Assert.Equal("evacuator_yellow", Catalogue.Variants[Catalogue.Evacuator].Id);

        Assert.True(Catalogue.IsService(Catalogue.Ambulance));
        Assert.True(Catalogue.IsService(Catalogue.Police));
        Assert.True(Catalogue.IsService(Catalogue.Evacuator));
    }

    /// <summary>
    /// <b>Every variant's wheel centres stand under its own bodywork.</b> The axles and the track are
    /// authored against the picture, and the one way to be sure they were read off the right one is that
    /// they fit inside it: a wheel <em>centre</em> outside the footprint is a car on outriggers, and a
    /// wheelbase as long as the body is a car with no overhang at either end. That the rubber round those
    /// centres shows past the panels is CAR-12, and is checked against the art itself.
    /// </summary>
    [Fact]
    public void EveryVariantKeepsItsWheelsInsideItsOwnFootprint()
    {
        for (var entry = 0; entry < Catalogue.SheetCount; entry++)
        {
            var variant = Catalogue.Variants[entry];
            var halfLengthM = variant.FootprintM.X * 0.5f;

            Assert.True(
                variant.HalfTrackM * 2f < variant.FootprintM.Y,
                $"{variant.Id} is {variant.FootprintM.Y:F2} m wide and its track is {variant.HalfTrackM * 2f:F2} m");
            Assert.True(
                variant.FrontAxleM < halfLengthM && variant.RearAxleM > -halfLengthM,
                $"{variant.Id} stands an axle outside its own {variant.FootprintM.X:F2} m body");
            Assert.True(
                variant.WheelbaseM > 0f && variant.WheelbaseM < variant.FootprintM.X,
                $"{variant.Id} has a {variant.WheelbaseM:F2} m wheelbase under a {variant.FootprintM.X:F2} m body");
        }
    }

    /// <summary>
    /// CAR-12b: <b>every variant is collided as a shape that fits inside the picture of it</b> — inside the
    /// panels, and so inside the mirrors and the tyres that stand off them too — and none of them is
    /// simply the footprint again. Asked of the art, because a fitted shape is measured off the art and a
    /// figure checked against another figure in the same file is checked against nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is <em>sampled</em> rather than rasterised: the shape's own boundary is where it can be outside
    /// the bodywork, and a few hundred points around it at pixel spacing catch a fit that has crept over
    /// an edge without asking the whole silhouette. A single stray pixel is allowed, because at this
    /// resolution one is the alpha fringe and never a panel.
    /// </para>
    /// <para>
    /// <b>What is asked is the silhouette and not the pixel</b>: a grille, a scoop and a gap between two
    /// mirrors are transparent and are inside the car, so the question is put to the picture with its
    /// holes filled — which is what a body is.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryVariantIsCollidedAsAShapeInsideItsOwnPicture()
    {
        var fitted = 0;
        foreach (var path in ShippedVariantFiles())
        {
            var file = AssetJson.Read(path, CarVariantJson.Default.CarVariantFile);
            Assert.NotNull(file.CollisionM);

            var sizeM = file.CollisionM.SizeM;
            var radiusM = file.CollisionM.CornerRadiusM;
            var half = sizeM * 0.5f;

            Assert.InRange(radiusM, 0f, MathF.Min(half.X, half.Y));
            Assert.True(
                sizeM.X <= file.FootprintM.X && sizeM.Y <= file.FootprintM.Y,
                $"{file.Id} is collided at {sizeM} inside a {file.FootprintM} picture");
            if (sizeM.X < file.FootprintM.X && sizeM.Y < file.FootprintM.Y) fitted++;

            using var art = SixLabors.ImageSharp.Image.Load<Rgba32>(
                AssetJson.Beside(path, file.Sprite));

            var body = Silhouette(art);
            var perM = new Vector2(art.Width / file.FootprintM.X, art.Height / file.FootprintM.Y);
            var core = half - new Vector2(radiusM);
            var outside = 0;
            for (var step = 0; step < BoundarySamples; step++)
            {
                var at = MathF.Tau * step / BoundarySamples;
                var (sin, cos) = MathF.SinCos(at);

                // The rounded box's own boundary: the core corner nearest this direction, plus the radius.
                var pivot = new Vector2(MathF.CopySign(core.X, cos), MathF.CopySign(core.Y, sin));
                var onM = pivot + (new Vector2(cos, sin) * radiusM);
                var px = (onM + (file.FootprintM * 0.5f)) * perM;
                var x = Math.Clamp((int)px.X, 0, art.Width - 1);
                var y = Math.Clamp((int)px.Y, 0, art.Height - 1);
                if (!Body(body, art.Width, art.Height, x, y)) outside++;
            }

            Assert.True(
                outside <= BoundarySamples * MayStandOff,
                $"{file.Id} is collided as a shape standing off its own bodywork at {outside} of " +
                $"{BoundarySamples} places round its edge");
        }

        Assert.Equal(Catalogue.SheetCount, fitted);
    }

    /// <summary>Places round a shape's edge to ask the picture about. A car's edge is a few metres, so this is a centimetre or two apart.</summary>
    const int BoundarySamples = 360;

    /// <summary>
    /// How much of that edge may be a texel outside the body after all. Fitting a curve to a picture is
    /// done to the centimetre and a corner sampled at the wrong side of its own arc is worth nothing;
    /// what this has to catch is a shape that has stopped following the art, and the one that got past an
    /// earlier version of this test was <b>forty per cent</b> outside it.
    /// </summary>
    const float MayStandOff = 0.02f;

    /// <summary>
    /// Whether this place is the car, <b>to within a pixel</b>. The shape is measured off the picture and
    /// authored to the centimetre, so a sample can land on the outer side of the very texel its edge runs
    /// down; a pixel is a centimetre of car and is the fringe of the art rather than a gap in it.
    /// </summary>
    static bool Body(bool[] body, int width, int height, int x, int y)
    {
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                if (body[(ny * width) + nx]) return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The car the picture draws: every pixel the transparent ground outside it cannot be walked to, so a
    /// grille or a scoop counts as bodywork rather than as a hole a shape may not cover.
    /// </summary>
    static bool[] Silhouette(SixLabors.ImageSharp.Image<Rgba32> art)
    {
        var ground = new bool[art.Width * art.Height];
        var stack = new Stack<int>();

        void Reach(int x, int y)
        {
            var at = (y * art.Width) + x;
            if (ground[at] || art[x, y].A > 24) return;

            ground[at] = true;
            stack.Push(at);
        }

        for (var x = 0; x < art.Width; x++)
        {
            Reach(x, 0);
            Reach(x, art.Height - 1);
        }

        for (var y = 0; y < art.Height; y++)
        {
            Reach(0, y);
            Reach(art.Width - 1, y);
        }

        while (stack.Count > 0)
        {
            var at = stack.Pop();
            var x = at % art.Width;
            var y = at / art.Width;
            if (x > 0) Reach(x - 1, y);
            if (x + 1 < art.Width) Reach(x + 1, y);
            if (y > 0) Reach(x, y - 1);
            if (y + 1 < art.Height) Reach(x, y + 1);
        }

        var body = new bool[ground.Length];
        for (var at = 0; at < body.Length; at++) body[at] = !ground[at];
        return body;
    }

    /// <summary>
    /// The two lists the catalogue itself reads, named here because what is being asked about is the file
    /// rather than the model: the footprint the fit is measured against is not on <see cref="CarVariant"/>.
    /// </summary>
    static IEnumerable<string> ShippedVariantFiles()
    {
        foreach (var list in (string[])["Fleet.json", "Service.json"])
        {
            var path = Path.Combine(ProjectPaths.Assets, "agents", "car", "variants", "common", list);
            foreach (var variant in AssetJson.Catalog(path)) yield return variant;
        }
    }

    /// <summary>
    /// SRV-4: <b>every shipped vehicle breaks, the evacuator included</b>. PHY-4b is a state the file format
    /// still offers and nothing in this town wears — the recovery truck used to, and a truck that could not
    /// be wrecked was the one vehicle in the town nothing could ever happen to.
    /// </summary>
    [Fact]
    public void NoShippedVariantIsUnbreakable()
    {
        for (var entry = 0; entry < Catalogue.SheetCount; entry++)
        {
            Assert.False(Catalogue.UnbreakableOf(entry), $"{Catalogue.Variants[entry].Id} cannot be wrecked");
        }
    }

    /// <summary>
    /// EVA-5: <b>the evacuator is the one variant that tows on an arm</b>, and the arm is its picture's — the
    /// reach the coupling is held at is a distance somebody drew, so it is read off the same file as the
    /// sprite and not chosen in the config.
    /// </summary>
    [Fact]
    public void TheEvacuatorIsTheOnlyVariantWithATowArm()
    {
        for (var entry = 0; entry < Catalogue.SheetCount; entry++)
        {
            Assert.Equal(entry == Catalogue.Evacuator, Catalogue.BeamOf(entry) is not null);
            Assert.Equal(entry == Catalogue.Evacuator, Catalogue.BeamSlotOf(entry, towing: false) != CarCatalog.NoBeam);
        }

        var beam = Catalogue.BeamOf(Catalogue.Evacuator)!.Value;
        CarTowArm[] pictures = [beam.Collapsed, beam.Extended];
        foreach (var arm in pictures)
        {
            Assert.True(File.Exists(arm.SpritePath), $"the arm names art that is not there: {arm.SpritePath}");
        }

        // Two pictures of one machine, and two slots: an arm drawn in reaches less far than one that is out.
        Assert.NotEqual(
            Catalogue.BeamSlotOf(Catalogue.Evacuator, towing: false),
            Catalogue.BeamSlotOf(Catalogue.Evacuator, towing: true));
        Assert.True(beam.Collapsed.SizeM.X < beam.Extended.SizeM.X, "the arm drawn in is no shorter than the arm out");
        Assert.True(beam.ReachM > 0f, "an arm with no reach holds nothing at any distance");

        // The hinge is bolted behind the middle of the truck and the fork reaches past its tail, or the arm
        // is drawn somewhere inside the bodywork it is meant to stick out of.
        var half = Catalogue.Variants[Catalogue.Evacuator].FootprintM.X * 0.5f;
        Assert.True(beam.PivotM.X < 0f && beam.PivotM.X > -half, $"the hinge sits at {beam.PivotM.X:F2} m");
        Assert.True(-beam.PivotM.X + beam.ReachM > half, "the fork does not reach past the truck's own tail");
    }
}
