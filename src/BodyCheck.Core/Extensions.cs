using Godot;

namespace BodyCheck.Core;

/// <summary>Extension members on Godot's types that more than one subsystem, or the game, uses.</summary>
public static class BodyCheckCoreExtensions
{
    extension(Vector2 self)
    {
        /// <summary>Lifts the vector to a 3D space with a Z=0.</summary>
        public Vector3 ToVector3()
        {
            return new Vector3(self.X, self.Y, 0);
        }

        /// <summary>The nearest of the eight directions to <paramref name="direction"/>, whatever its length, as a unit vector.</summary>
        public static Vector2 EightWay(Vector2 direction)
        {
            const float step = MathF.PI / 4;
            float angle = MathF.Round(direction.Angle() / step) * step;
            // Rounded, so that the axes are exactly zero.
            return new Vector2(MathF.Round(MathF.Cos(angle), 6), MathF.Round(MathF.Sin(angle), 6));
        }
    }

    extension(Vector3 self)
    {
        /// <summary>Projects the vector to 2D space by removing the Z coordinate.</summary>
        public Vector2 ToVector2()
        {
            return new Vector2(self.X, self.Y);
        }
    }
}
