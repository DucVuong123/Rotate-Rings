using System;
using System.Collections.Generic;
using RotateRings.Gameplay.Shapes;
using UnityEngine;

namespace RotateRings.Gameplay.Model
{
    /// <summary>
    /// Pure rules of the puzzle:
    /// - a piece that owns at least one engaged clip is locked; Fixed pieces never move;
    /// - a link is engaged while its target's tube covers the grip point, so it releases when a gap or an
    ///   open end passes under it and grips again when the tube comes back;
    /// - a moving piece slides through the links holding it only along parts of its path that match its motion
    ///   (arcs around the pivot when rotating, straight runs along the axis when sliding); anything else blocks it;
    /// - the parts that travel with a moving piece (its clip stems and heads, and the tube itself when sliding)
    ///   collide with everything else on the board, and stop touching what they hit; a clip head that has just
    ///   let go of a tube can only slide off along that tube's line, not sideways through its end;
    /// - when the player lets go (ResolveRemovals), a piece with no engaged link left (as owner or as target) is
    ///   removed, which can cascade. Moving alone never removes anything.
    /// </summary>
    public class BoardModel : IDisposable
    {
        const float DefaultMaxAngleStep = 30f;
        const float DefaultMaxSlideStep = 0.25f;
        const float MinSlideStep = 0.01f;
        const float CompatibilityTolerance = 1e-3f;

        /// <summary>Largest distance any moving point may travel in one sub-step, so thin parts cannot tunnel.</summary>
        const float MaxStepDisplacement = 0.03f;

        /// <summary>Halvings used to find the exact contact point within a blocked sub-step (0.03 / 2^8 ≈ 0.0001).</summary>
        const int ContactSearchIterations = 8;

        /// <summary>How far off its tube's line a just-released clip head may drift while still around the tube's end.</summary>
        const float TrackTolerance = 0.02f;

        readonly List<PieceModel> _pieces;
        readonly List<LinkModel> _links;
        readonly List<LinkModel> _holding = new();
        readonly BoardCollision _collision;

        public IReadOnlyList<PieceModel> Pieces => _pieces;
        public IReadOnlyList<LinkModel> Links => _links;

        public int AliveCount { get; private set; }
        public bool IsCleared => AliveCount == 0;

        public BoardModel(IEnumerable<PieceModel> pieces, IEnumerable<LinkModel> links)
        {
            _pieces = new List<PieceModel>(pieces);
            _links = new List<LinkModel>(links);
            AliveCount = _pieces.Count;
            _collision = new BoardCollision(_pieces, _links);
        }

        public PieceModel FindPiece(string id) => _pieces.Find(p => p.Id == id);

        public bool CanMove(PieceModel piece)
        {
            if (piece == null || piece.IsRemoved || piece.Motion == MotionType.Fixed)
                return false;

            foreach (var link in _links)
            {
                if (link.Owner == piece && link.IsEngaged)
                    return false;
            }

            return true;
        }

        public bool HasEngagement(PieceModel piece)
        {
            foreach (var link in _links)
            {
                if ((link.Owner == piece || link.Target == piece) && link.IsEngaged)
                    return true;
            }

            return false;
        }

        /// <summary>Alive piece whose tube is closest to point, within tolerance of its centerline.</summary>
        public PieceModel FindPieceAt(Vector2 point, float tolerance)
        {
            PieceModel best = null;
            var bestDistance = float.MaxValue;

            foreach (var piece in _pieces)
            {
                if (piece.IsRemoved)
                    continue;

                var distance = piece.DistanceToBody(point);
                if (distance <= tolerance && distance < bestDistance)
                {
                    best = piece;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>
        /// Moves piece by delta (degrees for Rotate, units for Slide) in sub-steps, stopping early where a link
        /// would hit a part of the tube the motion cannot slide through, or where something travelling with the
        /// piece would run into another part of the board. Nothing is removed here: links may release and grip
        /// again freely while dragging; call ResolveRemovals when the player lets go.
        /// Returns false if the piece is locked.
        /// </summary>
        public bool TryMove(PieceModel piece, float delta)
        {
            if (!CanMove(piece))
                return false;

            if (Mathf.Approximately(delta, 0f))
                return true;

            // The owner moves, so every clip it owns leaves its target's path for good.
            foreach (var link in _links)
            {
                if (link.Owner == piece && !link.Target.IsRemoved)
                    link.Detach();
            }

            _collision.BeginMove(piece);

            var maxStep = Mathf.Min(MaxStepFor(piece), CollisionStepFor(piece));
            var steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(delta) / maxStep));
            var step = delta / steps;

            for (var i = 0; i < steps; i++)
            {
                CollectHolding(piece);
                var previous = piece.Offset.Value;
                piece.Offset.Value = previous + step;

                if (StepBlocked(piece))
                {
                    piece.Offset.Value = ContactOffset(piece, previous, previous + step);
                    break;
                }
            }

            return true;
        }

        bool StepBlocked(PieceModel piece) => IsBlocked(piece) || IsTrackBlocked(piece) || _collision.HasNewContact();

        /// <summary>
        /// The last free offset between free (not blocked) and blocked, found by halving the interval, so a blocked
        /// piece stops touching what stopped it instead of a whole sub-step short.
        /// </summary>
        float ContactOffset(PieceModel piece, float free, float blocked)
        {
            for (var i = 0; i < ContactSearchIterations; i++)
            {
                var middle = (free + blocked) * 0.5f;
                piece.Offset.Value = middle;
                if (StepBlocked(piece))
                    blocked = middle;
                else
                    free = middle;
            }

            return free;
        }

        /// <summary>
        /// A clip that just let go of a tube still has its head around the tube's end (or right at it): the head
        /// may slide along the tube's line, but not sideways through the tube, and the owner cannot thread it
        /// back over the tube. Once the head no longer touches that tube, ordinary collisions apply.
        /// </summary>
        bool IsTrackBlocked(PieceModel piece)
        {
            foreach (var link in _links)
            {
                if (link.Owner != piece || link.Target.IsRemoved || !_collision.HeadOverlapsOwnTarget(link))
                    continue;

                var target = link.Target;
                var local = target.WorldToLocal(link.HeadPosition);
                if (BoardCollision.TrackDistance(target.Path, local) > TrackTolerance)
                    return true;

                if (target.Path.Covers(local, link.HalfSize, target.TubeRadius))
                    return true;
            }

            return false;
        }

        /// <summary>Sub-step size so nothing travelling with the piece moves more than MaxStepDisplacement at once.</summary>
        float CollisionStepFor(PieceModel piece)
        {
            if (piece.Motion != MotionType.Rotate)
                return MaxStepDisplacement;

            var reach = Mathf.Max(0.2f, _collision.Reach(piece));
            return MaxStepDisplacement / reach * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Removes every alive piece that has no engaged link, repeating until stable. Called when the player
        /// releases a piece, so only the final position of a drag counts.
        /// </summary>
        public void ResolveRemovals(List<PieceModel> removed)
        {
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var piece in _pieces)
                {
                    if (piece.IsRemoved || HasEngagement(piece))
                        continue;

                    piece.MarkRemoved();
                    AliveCount--;
                    removed?.Add(piece);
                    changed = true;
                }
            }
        }

        void CollectHolding(PieceModel piece)
        {
            _holding.Clear();
            foreach (var link in _links)
            {
                if (link.Target == piece && link.IsEngaged)
                    _holding.Add(link);
            }
        }

        /// <summary>
        /// After a step, each link that was holding the piece must either still sit on a part of the path that
        /// the motion slides through, or have left through an open end or a gap.
        /// </summary>
        bool IsBlocked(PieceModel piece)
        {
            foreach (var link in _holding)
            {
                var local = piece.WorldToLocal(link.GripPoint);
                var projection = piece.Path.Project(local);

                if (piece.Path.Covers(local, link.HalfSize, piece.TubeRadius))
                {
                    if (!IsCompatible(piece, piece.Path.Segments[projection.SegmentIndex]))
                        return true;
                    continue;
                }

                // Released: fine only if it left through an end of the tube (or a ring's gap).
                var nearEnd = projection.S <= link.HalfSize || projection.S >= piece.Path.Length - link.HalfSize;
                if (!nearEnd)
                    return true;
            }

            return false;
        }

        static bool IsCompatible(PieceModel piece, PathSegment segment) => piece.Motion switch
        {
            MotionType.Rotate => segment is ArcSegment arc && arc.Center.sqrMagnitude < CompatibilityTolerance,
            MotionType.Slide => segment is LineSegment line
                                && Mathf.Abs(line.Direction.x * piece.SlideAxisLocal.y - line.Direction.y * piece.SlideAxisLocal.x) < CompatibilityTolerance,
            _ => false,
        };

        /// <summary>Steps small enough that every part of the tube a link passes over is checked for blocking.</summary>
        float MaxStepFor(PieceModel piece)
        {
            if (piece.Motion == MotionType.Rotate)
                return DefaultMaxAngleStep;

            var maxStep = DefaultMaxSlideStep;
            foreach (var link in _links)
            {
                if (link.Target == piece)
                    maxStep = Mathf.Min(maxStep, link.HalfSize);
            }

            return Mathf.Max(MinSlideStep, maxStep);
        }

        public void Dispose()
        {
            foreach (var piece in _pieces)
                piece.Dispose();
        }
    }
}
