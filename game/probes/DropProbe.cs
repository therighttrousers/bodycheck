using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck.Probes;

/// <summary>
/// In the "Corner" room: drops the body onto the top corner of the wall, with its right edge overhanging
/// the wall by various amounts and from various heights, and reports whether it lands on the wall or is
/// pushed off to the left.
/// </summary>
public sealed class DropProbe : IProbe
{
    private const int TicksPerDrop = 90;

    // The wall's left face and top.
    private const float WallFace = 9;
    private const float WallTop = 3;

    private static readonly float[] Overhangs = [0.01f, 0.02f, 0.04f, 0.08f, 0.16f, 0.32f];
    private static readonly float[] Heights = [2.0f, 2.1f, 2.2f, 2.3f, 4.5f, 8.0f];

    private readonly Entity _body;
    private int _tick;
    private float _maxLeftSpeed;
    private string _row = "";

    public DropProbe(Entity body)
    {
        _body = body;
        GD.Print("  each column is a drop height; L = landed on the wall, number = fastest leftward speed when pushed off");
    }

    public bool Done => _tick >= TicksPerDrop * Overhangs.Length * Heights.Length;

    /// <summary>Call once per physics tick.</summary>
    public void Tick(float dt)
    {
        int drop = _tick / TicksPerDrop;
        int phase = _tick % TicksPerDrop;
        float overhang = Overhangs[drop / Heights.Length];
        _tick++;

        if (phase == 0)
        {
            float height = Heights[drop % Heights.Length];
            _body.Position = new Vector3(
                WallFace + overhang - _body.HalfSize.X, WallTop + height + _body.HalfSize.Y, 0);
            _body.LinearVelocity = Vector3.Zero;
            _maxLeftSpeed = 0;
            return;
        }

        _maxLeftSpeed = Mathf.Max(_maxLeftSpeed, -_body.LinearVelocity.X);
        if (phase == TicksPerDrop - 1)
        {
            bool landed = _body.Position.Y - _body.HalfSize.Y > WallTop - 0.1f;
            _row += landed ? "      L" : $" {_maxLeftSpeed,6:F2}";
            if (drop % Heights.Length == Heights.Length - 1)
            {
                GD.Print($"  overhang {overhang:F2}:{_row}");
                _row = "";
            }
        }
    }
}
