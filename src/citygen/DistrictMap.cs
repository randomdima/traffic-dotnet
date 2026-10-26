using System.Numerics;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>Which district of a town a point stands in</b> (GEN-56, GEN-64): the cells the town's land was cut into districts
/// on, carried with the plan so the town it was laid into can still be asked (SRV-5).
/// </summary>
/// <remarks>
/// <b>A district is any shape</b> (GEN-66), so it is answered off the cells it was cut on and never off a rule about its
/// shape. A place no district holds — the water, a barrier's corridor, a road between two districts, open land — is in
/// none (<c>−1</c>). A map not laid in districts is one district everywhere (<see cref="Whole"/>).
/// </remarks>
internal sealed class DistrictMap
{
    readonly short[] _label;
    readonly float _cellM;
    readonly int _columns;
    readonly int _rows;
    readonly bool[] _built;

    /// <param name="built">Whether each district carries a street of its own (<see cref="Built"/>).</param>
    public DistrictMap(short[] label, float cellM, int columns, int rows, Vector2[] centreM, bool[] built)
    {
        (_label, _cellM, _columns, _rows, _built) = (label, cellM, columns, rows, built);
        CentreM = centreM;
        BuiltCount = Array.FindAll(built, each => each).Length;
    }

    /// <summary>A town that is one district, which is every map not laid in districts.</summary>
    public static DistrictMap Whole { get; } = new([], 0f, 0, 0, [Vector2.Zero], [true]);

    public int Count => CentreM.Length;

    /// <summary>How many districts carry a street of their own (<see cref="Built"/>).</summary>
    public int BuiltCount { get; }

    /// <summary>The middle of the ground each district holds.</summary>
    public Vector2[] CentreM { get; }

    /// <summary>
    /// <b>Whether a district carries a street of its own once the town is laid</b> — what it is owed services for
    /// (GEN-56). One that carries none is open land (GEN-65): reached by no road between districts, or too narrow for a
    /// street of it to be anything but a dead end (GEN-5a).
    /// </summary>
    public bool Built(int district) => _built[district];

    /// <summary>The district a point stands in, or −1 where it stands in none.</summary>
    public int At(Vector2 pointM)
    {
        if (_label.Length == 0) return 0;

        var (column, row) = ((int)MathF.Floor(pointM.X / _cellM), (int)MathF.Floor(pointM.Y / _cellM));
        return column < 0 || row < 0 || column >= _columns || row >= _rows ? -1 : _label[(row * _columns) + column];
    }
}
