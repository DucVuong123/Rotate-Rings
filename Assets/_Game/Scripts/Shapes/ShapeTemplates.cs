using System.Collections.Generic;
using RotateRings.Gameplay.Model;
using UnityEngine;

namespace RotateRings.Gameplay.Shapes
{
    /// <summary>
    /// Circular arc around the pivot. The body is centered on local 180°, so the gap faces local +X and the
    /// piece rotation is the gap direction. sweep = 360 gives a closed ring.
    /// </summary>
    public sealed class ArcShape : IShapeTemplate
    {
        public const string TemplateId = "arc";

        public string Id => TemplateId;
        public string DisplayName => "Arc / Ring";
        public MotionType DefaultMotion => MotionType.Rotate;
        public ShapeParams DefaultParams => new() { radius = 0.6f, sweep = 260f };

        public IReadOnlyList<ParamDef> Params { get; } = new[]
        {
            new ParamDef(nameof(ShapeParams.radius), "Radius", 0.2f, 6f),
            new ParamDef(nameof(ShapeParams.sweep), "Sweep (°)", 10f, 360f),
        };

        public IReadOnlyList<HandleDef> Handles { get; } = new[]
        {
            new HandleDef("radius",
                p => new Vector2(-p.radius, 0f),
                (p, local) => p.With(nameof(ShapeParams.radius), Mathf.Clamp(local.magnitude, 0.2f, 6f))),
            new HandleDef("end",
                p => AngleMath.Direction(180f + p.sweep * 0.5f) * p.radius,
                (p, local) => p.With(nameof(ShapeParams.sweep), SweepTo(local))),
            new HandleDef("start",
                p => AngleMath.Direction(180f - p.sweep * 0.5f) * p.radius,
                (p, local) => p.With(nameof(ShapeParams.sweep), SweepTo(local))),
        };

        static float SweepTo(Vector2 local) =>
            Mathf.Clamp(2f * Mathf.Abs(AngleMath.Delta(180f, AngleMath.AngleOf(local))), 10f, 360f);

        public PiecePath Build(ShapeParams p)
        {
            var sweep = Mathf.Clamp(p.sweep, 1f, 360f);
            return new PiecePath(new PathSegment[] { new ArcSegment(Vector2.zero, p.radius, 180f - sweep * 0.5f, sweep) });
        }

        public Vector2 SlideAxis(ShapeParams p) => Vector2.up;
    }

    /// <summary>Straight bar centered on the pivot, along local +X.</summary>
    public sealed class BarShape : IShapeTemplate
    {
        public const string TemplateId = "bar";

        public string Id => TemplateId;
        public string DisplayName => "Bar";
        public MotionType DefaultMotion => MotionType.Slide;
        public ShapeParams DefaultParams => new() { length = 2f };

        public IReadOnlyList<ParamDef> Params { get; } = new[]
        {
            new ParamDef(nameof(ShapeParams.length), "Length", 0.3f, 10f),
        };

        public IReadOnlyList<HandleDef> Handles { get; } = new[]
        {
            new HandleDef("end",
                p => new Vector2(p.length * 0.5f, 0f),
                (p, local) => p.With(nameof(ShapeParams.length), Mathf.Clamp(2f * Mathf.Abs(local.x), 0.3f, 10f))),
            new HandleDef("start",
                p => new Vector2(-p.length * 0.5f, 0f),
                (p, local) => p.With(nameof(ShapeParams.length), Mathf.Clamp(2f * Mathf.Abs(local.x), 0.3f, 10f))),
        };

        public PiecePath Build(ShapeParams p)
        {
            var half = p.length * 0.5f;
            return new PiecePath(new PathSegment[] { new LineSegment(new Vector2(-half, 0f), new Vector2(half, 0f)) });
        }

        public Vector2 SlideAxis(ShapeParams p) => Vector2.right;
    }

    /// <summary>
    /// Bent tube: arm A runs along local +X and ends at the pivot, then a bend of bendAngle (positive turns left)
    /// with bendRadius, then arm B. ±90° gives an L, ±180° a hook. It slides along arm A.
    /// </summary>
    public sealed class ElbowShape : IShapeTemplate
    {
        public const string TemplateId = "elbow";

        public string Id => TemplateId;
        public string DisplayName => "Elbow / Hook";
        public MotionType DefaultMotion => MotionType.Slide;
        public ShapeParams DefaultParams => new() { length = 1.2f, length2 = 0.6f, bendRadius = 0.25f, bendAngle = 90f };

        public IReadOnlyList<ParamDef> Params { get; } = new[]
        {
            new ParamDef(nameof(ShapeParams.length), "Arm A", 0.2f, 8f),
            new ParamDef(nameof(ShapeParams.length2), "Arm B", 0f, 8f),
            new ParamDef(nameof(ShapeParams.bendRadius), "Bend radius", 0.1f, 2f),
            new ParamDef(nameof(ShapeParams.bendAngle), "Bend (°)", -180f, 180f),
        };

        public IReadOnlyList<HandleDef> Handles { get; } = new[]
        {
            new HandleDef("armA",
                p => new Vector2(-p.length, 0f),
                (p, local) => p.With(nameof(ShapeParams.length), Mathf.Clamp(-local.x, 0.2f, 8f))),
            new HandleDef("armB",
                p => BendEnd(p) + BendDirection(p) * p.length2,
                (p, local) => p.With(nameof(ShapeParams.length2),
                    Mathf.Clamp(Vector2.Dot(local - BendEnd(p), BendDirection(p)), 0f, 8f))),
        };

        static float Turn(ShapeParams p) => Mathf.Approximately(p.bendAngle, 0f) ? 1f : Mathf.Sign(p.bendAngle);

        static Vector2 BendCenter(ShapeParams p) => new(0f, Turn(p) * p.bendRadius);

        static float BendStart(ShapeParams p) => -90f * Turn(p);

        static Vector2 BendEnd(ShapeParams p) =>
            BendCenter(p) + AngleMath.Direction(BendStart(p) + p.bendAngle) * p.bendRadius;

        static Vector2 BendDirection(ShapeParams p) => AngleMath.Direction(p.bendAngle);

        public PiecePath Build(ShapeParams p)
        {
            var segments = new List<PathSegment> { new LineSegment(new Vector2(-p.length, 0f), Vector2.zero) };

            if (!Mathf.Approximately(p.bendAngle, 0f))
                segments.Add(new ArcSegment(BendCenter(p), p.bendRadius, BendStart(p), p.bendAngle));

            if (p.length2 > 0.001f)
            {
                var from = BendEnd(p);
                segments.Add(new LineSegment(from, from + BendDirection(p) * p.length2));
            }

            return new PiecePath(segments);
        }

        public Vector2 SlideAxis(ShapeParams p) => Vector2.right;
    }
}
