using System.Collections.Generic;
using UnityEngine;

namespace RotateRings.Gameplay.Shapes
{
    public readonly struct PathProjection
    {
        /// <summary>Distance along the whole path.</summary>
        public readonly float S;

        /// <summary>How far the projected point is from the centerline.</summary>
        public readonly float Distance;

        public readonly int SegmentIndex;

        public PathProjection(float s, float distance, int segmentIndex)
        {
            S = s;
            Distance = distance;
            SegmentIndex = segmentIndex;
        }
    }

    public readonly struct PathSample
    {
        public readonly Vector2 Point;
        public readonly Vector2 Tangent;

        public PathSample(Vector2 point, Vector2 tangent)
        {
            Point = point;
            Tangent = tangent;
        }
    }

    /// <summary>
    /// Centerline of a tube made of connected segments, in the piece's local space (pivot at the origin).
    /// </summary>
    public sealed class PiecePath
    {
        const float MaxArcStepDegrees = 4f;

        readonly List<PathSegment> _segments;
        readonly float[] _offsets;

        public IReadOnlyList<PathSegment> Segments => _segments;
        public float Length { get; }

        /// <summary>A full circle or a path whose ends meet: it has no open ends to slide out of.</summary>
        public bool IsClosed { get; }

        public PiecePath(IEnumerable<PathSegment> segments)
        {
            _segments = new List<PathSegment>(segments);
            _offsets = new float[_segments.Count];

            var total = 0f;
            for (var i = 0; i < _segments.Count; i++)
            {
                _offsets[i] = total;
                total += _segments[i].Length;
            }

            Length = total;
            IsClosed = _segments.Count == 1 && _segments[0] is ArcSegment { IsFullCircle: true }
                       || _segments.Count > 1 && (_segments[0].Start - _segments[^1].End).sqrMagnitude < 1e-6f;
        }

        public PathProjection Project(Vector2 point)
        {
            var best = new PathProjection(0f, float.MaxValue, -1);
            for (var i = 0; i < _segments.Count; i++)
            {
                var (s, distance) = _segments[i].Project(point);
                if (distance < best.Distance)
                    best = new PathProjection(_offsets[i] + s, distance, i);
            }

            return best;
        }

        /// <summary>
        /// True while a clip of half-size halfSize centered on point still wraps some of the tube: the point is on
        /// the centerline (within tolerance), or past an open end (or into a gap) by less than halfSize.
        /// The clip lets go only once it sits entirely beyond the tube.
        /// </summary>
        public bool Covers(Vector2 point, float halfSize, float tolerance)
        {
            var projection = Project(point);
            var atOpenEnd = !IsClosed && (projection.S <= 1e-4f || projection.S >= Length - 1e-4f);
            return projection.Distance <= (atOpenEnd ? Mathf.Max(halfSize, tolerance) : tolerance);
        }

        public Vector2 PointAt(float s)
        {
            var (segment, local) = Locate(s);
            return segment.PointAt(local);
        }

        public Vector2 TangentAt(float s)
        {
            var (segment, local) = Locate(s);
            return segment.TangentAt(local);
        }

        (PathSegment segment, float local) Locate(float s)
        {
            s = Mathf.Clamp(s, 0f, Length);
            for (var i = _segments.Count - 1; i >= 0; i--)
            {
                if (s >= _offsets[i])
                    return (_segments[i], s - _offsets[i]);
            }

            return (_segments[0], 0f);
        }

        /// <summary>Points along the centerline no more than maxStep apart (arcs also limited by angle).</summary>
        public List<PathSample> Sample(float maxStep)
        {
            var samples = new List<PathSample>();
            for (var i = 0; i < _segments.Count; i++)
            {
                var segment = _segments[i];
                var steps = Mathf.Max(1, Mathf.CeilToInt(segment.Length / maxStep));
                if (segment is ArcSegment arc)
                    steps = Mathf.Max(steps, Mathf.CeilToInt(Mathf.Abs(arc.Sweep) / MaxArcStepDegrees));

                for (var k = i == 0 ? 0 : 1; k <= steps; k++)
                {
                    var s = segment.Length * k / steps;
                    samples.Add(new PathSample(segment.PointAt(s), segment.TangentAt(s)));
                }
            }

            return samples;
        }

        /// <summary>Axis-aligned bounds of the centerline in local space.</summary>
        public Rect Bounds()
        {
            var samples = Sample(0.1f);
            var min = samples[0].Point;
            var max = min;
            foreach (var sample in samples)
            {
                min = Vector2.Min(min, sample.Point);
                max = Vector2.Max(max, sample.Point);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
