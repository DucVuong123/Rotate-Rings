using System.Collections.Generic;
using RotateRings.Gameplay.Shapes;
using UnityEngine;

namespace RotateRings.Gameplay.Model
{
    /// <summary>
    /// 2D collision between the solid parts of the board, matching what is drawn: tube sections and clip stems
    /// are capsules, clip heads and pins are boxes of the drawn size. Used to stop a moving piece right where one
    /// of its parts would run into something. Overlaps already present when a move starts (designed interlocks)
    /// are tolerated but may not get any deeper.
    /// </summary>
    public sealed class BoardCollision
    {
        public const float StemRadius = 0.035f;
        public const float HeadDepthScale = 1.6f;
        const float PinDepthScale = 1.9f;
        const float PinLengthScale = 1.5f;
        const float TubeSampleStep = 0.1f;

        /// <summary>How much deeper a tolerated overlap may get before it counts as running into something.</summary>
        const float DeepeningTolerance = 0.002f;

        enum Kind
        {
            Capsule,
            Box,
        }

        sealed class Part
        {
            /// <summary>Piece the part moves with; null for pins (fixed to the board).</summary>
            public PieceModel Owner;

            /// <summary>The clip or pin this part belongs to; null for tube sections.</summary>
            public LinkModel Link;

            public Kind Kind;
            public bool IsHead => Kind == Kind.Box;

            // Capsule: axis A-B and radius. Box: center A, unit axis U along the gripped tube, half sizes.
            // In the owner's local space (world space for pins).
            public Vector2 A, B, U;
            public float Radius, HalfLength, HalfDepth;

            // World-space cache.
            public Vector2 WorldA, WorldB, WorldU;
            public Rect Bounds;

            public void UpdateWorld()
            {
                if (Kind == Kind.Capsule)
                {
                    WorldA = Owner != null ? Owner.LocalToWorld(A) : A;
                    WorldB = Owner != null ? Owner.LocalToWorld(B) : B;
                    Bounds = Rect.MinMaxRect(
                        Mathf.Min(WorldA.x, WorldB.x) - Radius, Mathf.Min(WorldA.y, WorldB.y) - Radius,
                        Mathf.Max(WorldA.x, WorldB.x) + Radius, Mathf.Max(WorldA.y, WorldB.y) + Radius);
                    return;
                }

                WorldA = Owner != null ? Owner.LocalToWorld(A) : A;
                WorldU = Owner != null ? Owner.LocalDirectionToWorld(U) : U;
                var extent = Mathf.Sqrt(HalfLength * HalfLength + HalfDepth * HalfDepth);
                Bounds = new Rect(WorldA.x - extent, WorldA.y - extent, extent * 2f, extent * 2f);
            }

            /// <summary>The box seen as the capsule that fills it along its longer side (for box-box tests).</summary>
            public (Vector2 a, Vector2 b, float radius) AsCapsule()
            {
                var v = new Vector2(-WorldU.y, WorldU.x);
                return HalfDepth >= HalfLength
                    ? (WorldA - v * (HalfDepth - HalfLength), WorldA + v * (HalfDepth - HalfLength), HalfLength)
                    : (WorldA - WorldU * (HalfLength - HalfDepth), WorldA + WorldU * (HalfLength - HalfDepth), HalfDepth);
            }
        }

        readonly List<Part> _parts = new();

        /// <summary>Pieces joined by a clip are layered over one another, so their tubes may cross.</summary>
        readonly HashSet<(PieceModel, PieceModel)> _clipped = new();
        readonly List<Part> _moving = new();
        readonly List<Part> _obstacles = new();
        readonly Dictionary<long, float> _initialDepths = new();
        PieceModel _movingPiece;

        public BoardCollision(IReadOnlyList<PieceModel> pieces, IReadOnlyList<LinkModel> links)
        {
            foreach (var piece in pieces)
            {
                var samples = piece.Path.Sample(TubeSampleStep);
                for (var i = 0; i + 1 < samples.Count; i++)
                    _parts.Add(new Part { Owner = piece, Kind = Kind.Capsule, A = samples[i].Point, B = samples[i + 1].Point, Radius = piece.TubeRadius });
            }

            foreach (var link in links)
            {
                AddLink(link);
                if (link.Owner != null)
                {
                    _clipped.Add((link.Owner, link.Target));
                    _clipped.Add((link.Target, link.Owner));
                }
            }
        }

        /// <summary>Half the clip head's thickness across the tube it grips.</summary>
        public static float HeadDepthHalf(LinkModel link) =>
            link.Target.TubeRadius * (link.IsPin ? PinDepthScale : HeadDepthScale);

        /// <summary>Half the clip head's length along the tube it grips.</summary>
        public static float HeadLengthHalf(LinkModel link) => link.HalfSize * (link.IsPin ? PinLengthScale : 1f);

        void AddLink(LinkModel link)
        {
            var target = link.Target;
            var projection = target.Path.Project(target.WorldToLocal(link.GripPoint));
            var tangent = target.LocalDirectionToWorld(target.Path.TangentAt(projection.S)).normalized;
            var depthHalf = HeadDepthHalf(link);

            var head = new Part
            {
                Owner = link.Owner, Link = link, Kind = Kind.Box,
                HalfLength = HeadLengthHalf(link), HalfDepth = depthHalf,
            };

            if (link.IsPin)
            {
                head.A = link.GripPoint;
                head.U = tangent;
                _parts.Add(head);
                return;
            }

            var owner = link.Owner;
            head.A = link.HeadOnOwner;
            head.U = PieceModel.Rotate(tangent, -owner.CurrentRotation).normalized;
            _parts.Add(head);

            // Stem: from the owner's tube to the head (the part inside the head is covered by the head's box).
            var grip = link.HeadOnOwner;
            var anchor = owner.Path.PointAt(owner.Path.Project(grip).S);
            var toGrip = grip - anchor;
            var length = toGrip.magnitude - depthHalf;
            if (length > 0.01f)
                _parts.Add(new Part { Owner = owner, Link = link, Kind = Kind.Capsule, A = anchor, B = anchor + toGrip.normalized * length, Radius = StemRadius });
        }

        /// <summary>Remembers what already overlaps (and how deep) before piece starts moving.</summary>
        public void BeginMove(PieceModel piece)
        {
            _movingPiece = piece;
            _moving.Clear();
            _obstacles.Clear();
            _initialDepths.Clear();

            foreach (var part in _parts)
            {
                if (part.Owner == piece)
                {
                    _moving.Add(part);
                    continue;
                }

                var gone = part.Owner != null ? part.Owner.IsRemoved : part.Link.Target.IsRemoved;
                if (gone)
                    continue;

                part.UpdateWorld();
                _obstacles.Add(part);
            }

            Scan(record: true);
        }

        /// <summary>True when, at the piece's current pose, a moving part runs into something.</summary>
        public bool HasNewContact() => Scan(record: false);

        /// <summary>Largest distance from the pivot to any moving part, used to size rotation steps.</summary>
        public float Reach(PieceModel piece)
        {
            var reach = 0f;
            foreach (var part in _parts)
            {
                if (part.Owner != piece)
                    continue;

                reach = part.Kind == Kind.Capsule
                    ? Mathf.Max(reach, part.A.magnitude + part.Radius, part.B.magnitude + part.Radius)
                    : Mathf.Max(reach, part.A.magnitude + Mathf.Sqrt(part.HalfLength * part.HalfLength + part.HalfDepth * part.HalfDepth));
            }

            return reach;
        }

        /// <summary>
        /// Whether the clip head of link (owned by the moving piece) overlaps the tube it gripped.
        /// Used by the board's track rule.
        /// </summary>
        public bool HeadOverlapsOwnTarget(LinkModel link)
        {
            Part head = null;
            foreach (var part in _moving)
            {
                if (part.Link == link && part.IsHead)
                {
                    head = part;
                    break;
                }
            }

            if (head == null)
                return false;

            head.UpdateWorld();
            foreach (var obstacle in _obstacles)
            {
                if (obstacle.Owner == link.Target && obstacle.Link == null
                    && head.Bounds.Overlaps(obstacle.Bounds) && Depth(head, obstacle) > 0f)
                    return true;
            }

            return false;
        }

        bool Scan(bool record)
        {
            for (var m = 0; m < _moving.Count; m++)
            {
                var moving = _moving[m];
                moving.UpdateWorld();

                for (var o = 0; o < _obstacles.Count; o++)
                {
                    var obstacle = _obstacles[o];
                    if (!moving.Bounds.Overlaps(obstacle.Bounds) || Ignored(moving, obstacle))
                        continue;

                    var depth = Depth(moving, obstacle);
                    if (depth <= 0f)
                        continue;

                    var key = ((long)m << 32) | (uint)o;
                    if (record)
                        _initialDepths[key] = depth;
                    else if (!_initialDepths.TryGetValue(key, out var initial) || depth > initial + DeepeningTolerance)
                        return true;
                }
            }

            return false;
        }

        /// <summary>Pairs that are meant to pass through each other, or are handled by another rule.</summary>
        bool Ignored(Part moving, Part obstacle)
        {
            // Clips and pins gripping the moving piece wrap its tube: the tube slides through them.
            if (moving.Link == null && obstacle.Link != null && obstacle.Link.Target == _movingPiece && !obstacle.Link.IsDetached)
                return true;

            // A clip head against the tube it gripped is handled by the board's track rule.
            if (moving.IsHead && obstacle.Link == null && obstacle.Owner == moving.Link.Target)
                return true;

            // Two tubes joined by a clip lie one over the other (like a bar crossing the arc that clips it).
            if (moving.Link == null && obstacle.Link == null && _clipped.Contains((moving.Owner, obstacle.Owner)))
                return true;

            return false;
        }

        /// <summary>How far two parts overlap (positive) or how far apart they are (negative).</summary>
        static float Depth(Part p, Part q)
        {
            if (p.Kind == Kind.Capsule && q.Kind == Kind.Capsule)
                return p.Radius + q.Radius - Mathf.Sqrt(SegmentDistanceSquared(p.WorldA, p.WorldB, q.WorldA, q.WorldB));

            if (p.Kind == Kind.Capsule)
                return CapsuleBoxDepth(p.WorldA, p.WorldB, p.Radius, q);

            if (q.Kind == Kind.Capsule)
                return CapsuleBoxDepth(q.WorldA, q.WorldB, q.Radius, p);

            var (a, b, radius) = q.AsCapsule();
            return CapsuleBoxDepth(a, b, radius, p);
        }

        /// <summary>Overlap depth between a capsule and a box: radius minus the segment's distance to the box.</summary>
        static float CapsuleBoxDepth(Vector2 a, Vector2 b, float radius, Part box)
        {
            // Into the box's frame: u along the gripped tube, v across it.
            var u = box.WorldU;
            var v = new Vector2(-u.y, u.x);
            var la = new Vector2(Vector2.Dot(a - box.WorldA, u), Vector2.Dot(a - box.WorldA, v));
            var lb = new Vector2(Vector2.Dot(b - box.WorldA, u), Vector2.Dot(b - box.WorldA, v));
            var half = new Vector2(box.HalfLength, box.HalfDepth);

            // Closest approach between the segment and the box, sampled finely enough for these small parts,
            // plus exact checks at the box corners.
            var best = float.MaxValue;
            const int samples = 16;
            for (var i = 0; i <= samples; i++)
                best = Mathf.Min(best, SignedBoxDistance(Vector2.Lerp(la, lb, (float)i / samples), half));

            foreach (var corner in new[] { new Vector2(half.x, half.y), new Vector2(-half.x, half.y), new Vector2(half.x, -half.y), new Vector2(-half.x, -half.y) })
                best = Mathf.Min(best, Mathf.Sqrt(PointSegmentDistanceSquared(corner, la, lb)) + Mathf.Min(0f, SignedBoxDistance(corner, half)));

            return radius - best;
        }

        /// <summary>Distance from a point to a centered box; negative inside (depth to the nearest side).</summary>
        static float SignedBoxDistance(Vector2 p, Vector2 half)
        {
            var d = new Vector2(Mathf.Abs(p.x) - half.x, Mathf.Abs(p.y) - half.y);
            var outside = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude;
            var inside = Mathf.Min(Mathf.Max(d.x, d.y), 0f);
            return outside + inside;
        }

        static float SegmentDistanceSquared(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1)
        {
            if (SegmentsIntersect(a0, a1, b0, b1))
                return 0f;

            return Mathf.Min(
                Mathf.Min(PointSegmentDistanceSquared(a0, b0, b1), PointSegmentDistanceSquared(a1, b0, b1)),
                Mathf.Min(PointSegmentDistanceSquared(b0, a0, a1), PointSegmentDistanceSquared(b1, a0, a1)));
        }

        static float PointSegmentDistanceSquared(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lengthSquared = ab.sqrMagnitude;
            var t = lengthSquared > 1e-12f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSquared) : 0f;
            return (p - (a + ab * t)).sqrMagnitude;
        }

        static bool SegmentsIntersect(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1)
        {
            static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

            var d1 = Cross(b0, b1, a0);
            var d2 = Cross(b0, b1, a1);
            var d3 = Cross(a0, a1, b0);
            var d4 = Cross(a0, a1, b1);
            return (d1 > 0f && d2 < 0f || d1 < 0f && d2 > 0f) && (d3 > 0f && d4 < 0f || d3 < 0f && d4 > 0f);
        }

        /// <summary>Distance from a local point to the line the nearest tube section runs along (circle or line).</summary>
        public static float TrackDistance(PiecePath path, Vector2 local)
        {
            var segment = path.Segments[path.Project(local).SegmentIndex];
            switch (segment)
            {
                case ArcSegment arc:
                    return Mathf.Abs((local - arc.Center).magnitude - arc.Radius);
                case LineSegment line:
                    var offset = local - line.From;
                    return Mathf.Abs(offset.x * line.Direction.y - offset.y * line.Direction.x);
                default:
                    return path.Project(local).Distance;
            }
        }
    }
}
