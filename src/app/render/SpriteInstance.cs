using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.App.Render;

/// <summary>
/// One upright quad on the ground: everything the second pipeline is told about a body, and the only
/// thing that changes between two frames of a standing town. <b>Twenty-eight bytes</b>, packed as the vertex stage
/// reads it: the centre and the heading as floats, the half-size as two halves, the cell of the sheet as four bytes, the
/// tint as four normalised bytes, and the sheet and the ID as a half-word each.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rotation belongs to whatever the art draws one frame of</b>, and is zero for everything else.
/// A walker does not turn with the body, on its feet or down (PER-18): the sheet draws all eight facings and
/// every one of them is drawn upright, so heading is shown by <em>which cell</em> is sampled. A car's art is
/// one frame with its nose along <c>+x</c>, so for it the heading is the quad's own rotation — one float,
/// whose sine and cosine are taken in the shader where the quad is built anyway.
/// </para>
/// <para>
/// <b>The tint is a colour and an opacity</b>, and everything the town draws but a mark, a glow and a casualty
/// leaves both alone: a mark is the same quad laid at whatever strength the tyre that made it earned, and a
/// picture cannot carry a per-instance strength; a casualty is its stance reddened (PER-18). <b>"Selected" is no tint</b>: it is a ring round the picture
/// (<see cref="Ring"/>, <see cref="SpriteRings"/>), carried in the sheet's half-word above every slot the atlas has.
/// </para>
/// <para>
/// <b>The half-size is two halves</b> (<see cref="Halves"/>), so an instance the device emits is the host's to the bit.
/// A half is a millimetre on a car and a centimetre and a half on the widest roof.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal readonly record struct SpriteInstance
{
    public readonly Vector2 CentreM;

    /// <summary>The half-size's two halves, x in the low sixteen bits (<see cref="HalfSizeM"/>).</summary>
    readonly uint _halfSize;

    public readonly float HeadingRad;

    /// <summary>Which cell of its sheet is drawn.</summary>
    public readonly SpriteCell Cell;

    /// <summary>The tint as red, green, blue and opacity a byte each from the low (<see cref="Tint"/>).</summary>
    public readonly uint PackedTint;

    /// <summary>The atlas slot, and the ring above it (<see cref="RingShift"/>).</summary>
    readonly ushort _sheet;

    /// <summary>
    /// Which body of its kind this is, the same every frame — a building's or a wagon's index, its low sixteen bits; nought
    /// for everything nothing turns on. All a fan's start is taken from (<c>fans.glsl</c>, OBJ-6).
    /// </summary>
    public readonly ushort Id;

    /// <summary>Where the ring starts in the sheet's half-word, past every slot (<c>quad.glsl</c> and <c>town.wgsl</c> say it again).</summary>
    public const int RingShift = 14;

    public SpriteInstance(
        Vector2 centreM, Vector2 halfSizeM, SpriteCell cell, uint packedTint, uint sheet, float headingRad, int id = 0,
        SpriteRing ring = SpriteRing.None)
    {
        CentreM = centreM;
        _halfSize = Halves.Pack(halfSizeM);
        HeadingRad = headingRad;
        Cell = cell;
        PackedTint = packedTint;
        _sheet = (ushort)(sheet | ((uint)ring << RingShift));
        Id = (ushort)id;
    }

    /// <summary>The atlas slot drawn from: fewer than <see cref="SheetAtlas.Slots"/>.</summary>
    public ushort Sheet => (ushort)(_sheet & ((1 << RingShift) - 1));

    /// <summary>The ring round its picture (CTL-1), which the stages grow the quad by.</summary>
    public SpriteRing Ring => (SpriteRing)(_sheet >> RingShift);

    /// <summary>The quad's half-size as drawn: the one handed in, at a half's precision (<see cref="AsDrawn"/>).</summary>
    public Vector2 HalfSizeM => Halves.Unpack(_halfSize);

    public Vector4 Tint => SpriteTint.Of(PackedTint);

    /// <summary>A half-size as an instance carries it, which is what a quad is drawn at.</summary>
    public static Vector2 AsDrawn(Vector2 halfSizeM) => new((float)(Half)halfSizeM.X, (float)(Half)halfSizeM.Y);
}

/// <summary>
/// <b>Which cell of a sheet cut on a grid an instance is drawn from</b>: its column and row, and the grid's columns and
/// rows — a byte each, so a sheet is cut at most 255 to a side. The vertex stage maps a corner to
/// <c>(cell + corner) / grid</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly record struct SpriteCell(byte Column, byte Row, byte Columns, byte Rows)
{
    /// <summary>The whole sheet: the one cell of a grid of one.</summary>
    public static readonly SpriteCell Whole = new(0, 0, 1, 1);

    /// <summary>A cell of a grid, checked to fit the bytes it is carried in.</summary>
    public static SpriteCell Of(int column, int row, int columns, int rows) =>
        columns is < 1 or > byte.MaxValue || rows is < 1 or > byte.MaxValue
            ? throw new ArgumentOutOfRangeException(nameof(columns), $"a {columns} × {rows} grid is past what a cell carries")
            : new SpriteCell((byte)column, (byte)row, (byte)columns, (byte)rows);

    /// <summary>The four bytes as one word, the column lowest — as the device's emit writes a cell (<c>spritesCell</c>).</summary>
    public uint Packed => Column | ((uint)Row << 8) | ((uint)Columns << 16) | ((uint)Rows << 24);

    /// <summary>Where the cell starts in its sheet, in texture coordinates.</summary>
    public Vector2 UvMin => new(Column / (float)Columns, Row / (float)Rows);

    /// <summary>And how much of the sheet it is.</summary>
    public Vector2 UvSize => new(1f / Columns, 1f / Rows);
}

/// <summary>A tint as an instance carries it: four bytes, each a channel's share of full rounded to the nearest of 255 steps.</summary>
internal static class SpriteTint
{
    /// <summary>Untinted and opaque: everything the town draws but a mark, a glow and a casualty.</summary>
    public const uint Plain = uint.MaxValue;

    public static uint Pack(Vector4 tint) => Byte(tint.X) | (Byte(tint.Y) << 8) | (Byte(tint.Z) << 16) | (Byte(tint.W) << 24);

    public static Vector4 Of(uint packed) =>
        new Vector4(packed & 0xFF, (packed >> 8) & 0xFF, (packed >> 16) & 0xFF, packed >> 24) / byte.MaxValue;

    static uint Byte(float share) => (uint)((Math.Clamp(share, 0f, 1f) * byte.MaxValue) + 0.5f);
}
