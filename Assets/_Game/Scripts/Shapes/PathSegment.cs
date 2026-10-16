using UnityEngine;

namespace RotateRings.Gameplay.Shapes
{
    /// <summary>One piece of a tube's centerline, in the owning piece's local space.</summary>
    public abstract class PathSegment
    {
        public abstract float Length { get; }

        /// <summary>Point at distance s (0..Length) along the segment.</summary>
        public abstract Vector2 PointAt(float s);

        /// <summary>Unit tangent at distance s, pointing toward increasing s.</summary>
        public abstract Vector2 TangentAt(float s);

        /// <summary>Closest point on the segment: its distance along the segment and how far point is from it.</summary>
        public abstract (float s, float distance) Project(Vector2 point);

        public Vector2 Start => PointAt(0f);
        public Vector2 End => PointAt(Length);
    }

    public sealed class LineSegment : PathSegment
    {
        public Vector2 From { get; }
        public Vector2 To { get; }
        public Vector2 Direction { get; }

        public LineSegment(Vector2 from, Vector2 to)
        {
            From = from;
            To = to;
            Direction = (to - from).normalized;
        }

        public override float Length => (To - From).magnitude;

        public override Vector2 PointAt(float s) => From + Direction * s;

        public override Vector2 TangentAt(float s) => Direction;

        public override (float s, float distance) Project(Vector2 point)
        {
            var s = Mathf.Clamp(Vector2.Dot(point - From, Direction), 0f, Length);
            return (s, (point - PointAt(s)).magnitude);
        }
    }

    public sealed class ArcSegment : PathSegment
    {
        public Vector2 Center { get; }
        public float Radius { get; }

        /// <summary>Start angle in degrees, CCW from +X.</summary>
        public float StartAngle { get; }

        /// <summary>Signed sweep in degrees; positive runs CCW. |Sweep| >= 360 is a full circle.</summary>
        public float Sweep { get; }

        public bool IsFullCircle => Mathf.Abs(Sweep) >= 360f - 1e-3f;

        public ArcSegment(Vector2 center, float radius, float startAngle, float sweep)
        {
            Center = center;
            Radius = radius;
            StartAngle = startAngle;
            Sweep = Mathf.Clamp(sweep, -360f, 360f);
        }

        public override float Length => Radius * Mathf.Abs(Sweep) * Mathf.Deg2Rad;

        float AngleAt(float s) => StartAngle + Mathf.Sign(Sweep) * (s / Radius) * Mathf.Rad2Deg;

        public override Vector2 PointAt(float s)
        {
            var angle = AngleAt(s) * Mathf.Deg2Rad;
            return Center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Radius;
        }

        public override Vector2 TangentAt(float s)
        {
            var angle = AngleAt(s) * Mathf.Deg2Rad;
            var ccw = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
            return Sweep >= 0f ? ccw : -ccw;
        }

        public override (float s, float distance) Project(Vector2 point)
        {
            var offset = point - Center;
            var angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;

            // Angle travelled from the start in the sweep direction, in [0, 360).
            var travelled = Mathf.Repeat((angle - StartAngle) * Mathf.Sign(Sweep == 0f ? 1f : Sweep), 360f);
            var span = Mathf.Abs(Sweep);

            if (IsFullCircle || travelled <= span)
            {
                var s = travelled * Mathf.Deg2Rad * Radius;
                return (s, Mathf.Abs(offset.magnitude - Radius));
            }

            // In the missing part of the circle: snap to the nearer end.
            var toStart = (point - Start).magnitude;
            var toEnd = (point - End).magnitude;
            return toStart <= toEnd ? (0f, toStart) : (Length, toEnd);
        }
    }
}
