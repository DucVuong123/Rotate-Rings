using UnityEngine;

namespace RotateRings.Gameplay.Model
{
    public static class AngleMath
    {
        /// <summary>Angle of a vector in degrees, CCW from +X, in (-180, 180].</summary>
        public static float AngleOf(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;

        /// <summary>Shortest signed difference b - a in degrees, in [-180, 180].</summary>
        public static float Delta(float a, float b) => Mathf.DeltaAngle(a, b);

        public static Vector2 Direction(float degrees)
        {
            var rad = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }
    }
}
