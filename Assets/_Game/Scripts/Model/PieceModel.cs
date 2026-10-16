using System;
using R3;
using RotateRings.Gameplay.Shapes;
using UnityEngine;

namespace RotateRings.Gameplay.Model
{
    /// <summary>
    /// A rigid tube on the board. Its pose is Position + BaseRotation plus one degree of freedom (Offset):
    /// degrees around the pivot for Rotate, distance along the slide axis for Slide.
    /// </summary>
    public class PieceModel : IDisposable
    {
        public string Id { get; }
        public Color Color { get; }
        public IShapeTemplate Template { get; }
        public ShapeParams Parameters { get; }
        public PiecePath Path { get; }
        public MotionType Motion { get; }
        public float TubeRadius { get; }

        /// <summary>Pivot in world space.</summary>
        public Vector2 Position { get; }

        public float BaseRotation { get; }

        /// <summary>Unit slide direction in local space.</summary>
        public Vector2 SlideAxisLocal { get; }

        public bool IsRemoved { get; private set; }

        public ReactiveProperty<float> Offset { get; } = new(0f);

        public PieceModel(string id, IShapeTemplate template, ShapeParams parameters, MotionType motion,
            Vector2 position, float baseRotation, Color color, float tubeRadius)
        {
            Id = id;
            Template = template;
            Parameters = parameters;
            Path = template.Build(parameters);
            Motion = motion;
            Position = position;
            BaseRotation = baseRotation;
            Color = color;
            TubeRadius = tubeRadius;
            SlideAxisLocal = template.SlideAxis(parameters).normalized;
        }

        public float CurrentRotation => BaseRotation + (Motion == MotionType.Rotate ? Offset.Value : 0f);

        public Vector2 CurrentTranslation => Motion == MotionType.Slide
            ? Position + Rotate(SlideAxisLocal, BaseRotation) * Offset.Value
            : Position;

        public Vector2 SlideAxisWorld => Rotate(SlideAxisLocal, BaseRotation);

        public Vector2 WorldToLocal(Vector2 world) => Rotate(world - CurrentTranslation, -CurrentRotation);

        public Vector2 LocalToWorld(Vector2 local) => CurrentTranslation + Rotate(local, CurrentRotation);

        public Vector2 LocalDirectionToWorld(Vector2 local) => Rotate(local, CurrentRotation);

        /// <summary>Distance from a world point to the tube centerline.</summary>
        public float DistanceToBody(Vector2 world) => Path.Project(WorldToLocal(world)).Distance;

        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            var rad = degrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(rad);
            var sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        internal void MarkRemoved() => IsRemoved = true;

        public void Dispose() => Offset.Dispose();
    }
}
