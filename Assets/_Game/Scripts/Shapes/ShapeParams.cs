using System;
using UnityEngine;

namespace RotateRings.Gameplay.Shapes
{
    /// <summary>Flat bag of shape parameters; each template reads only the fields it declares.</summary>
    [Serializable]
    public struct ShapeParams
    {
        public float radius;
        public float sweep;
        public float length;
        public float length2;
        public float bendRadius;
        public float bendAngle;

        public float Get(string field) => field switch
        {
            nameof(radius) => radius,
            nameof(sweep) => sweep,
            nameof(length) => length,
            nameof(length2) => length2,
            nameof(bendRadius) => bendRadius,
            nameof(bendAngle) => bendAngle,
            _ => throw new ArgumentException($"Unknown shape parameter '{field}'."),
        };

        public ShapeParams With(string field, float value)
        {
            var copy = this;
            switch (field)
            {
                case nameof(radius): copy.radius = value; break;
                case nameof(sweep): copy.sweep = value; break;
                case nameof(length): copy.length = value; break;
                case nameof(length2): copy.length2 = value; break;
                case nameof(bendRadius): copy.bendRadius = value; break;
                case nameof(bendAngle): copy.bendAngle = value; break;
                default: throw new ArgumentException($"Unknown shape parameter '{field}'.");
            }

            return copy;
        }

        public override int GetHashCode() => HashCode.Combine(radius, sweep, length, length2, bendRadius, bendAngle);
    }

    /// <summary>An editable parameter a template exposes to the level editor.</summary>
    public readonly struct ParamDef
    {
        public readonly string Field;
        public readonly string Label;
        public readonly float Min;
        public readonly float Max;

        public ParamDef(string field, string label, float min, float max)
        {
            Field = field;
            Label = label;
            Min = min;
            Max = max;
        }

        public float Clamp(float value) => Mathf.Clamp(value, Min, Max);
    }

    /// <summary>A draggable point in the editor that changes shape parameters (positions in local space).</summary>
    public sealed class HandleDef
    {
        public string Id { get; }
        public Func<ShapeParams, Vector2> Position { get; }

        /// <summary>New parameters when the handle is dragged to a local-space point.</summary>
        public Func<ShapeParams, Vector2, ShapeParams> Apply { get; }

        public HandleDef(string id, Func<ShapeParams, Vector2> position, Func<ShapeParams, Vector2, ShapeParams> apply)
        {
            Id = id;
            Position = position;
            Apply = apply;
        }
    }
}
