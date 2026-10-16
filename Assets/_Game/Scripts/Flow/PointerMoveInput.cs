using System.Collections.Generic;
using RotateRings.Gameplay.Commands;
using RotateRings.Gameplay.Model;
using RotateRings.Gameplay.Shapes;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;
using VitalRouter;

namespace RotateRings.Gameplay.Flow
{
    /// <summary>
    /// Press on a piece's tube and drag: rotating pieces follow the pointer's angle around their pivot,
    /// sliding pieces follow the pointer projected on their axis. Pieces break only on release.
    /// Works with mouse and touch.
    /// </summary>
    public class PointerMoveInput : ITickable
    {
        const float HitToleranceInTubes = 3f;
        const float LockedFeedbackDegrees = 4f;
        const float LockedFeedbackDistance = 0.06f;

        readonly Camera _camera;
        readonly BoardSession _session;
        readonly GameplayState _state;
        readonly ICommandPublisher _publisher;
        readonly List<PieceModel> _removed = new();

        PieceModel _active;
        Vector2 _lastPoint;
        float _lockedDrag;
        bool _lockedReported;
        bool _moved;

        public PointerMoveInput(Camera camera, BoardSession session, GameplayState state, ICommandPublisher publisher)
        {
            _camera = camera;
            _session = session;
            _state = state;
            _publisher = publisher;
        }

        public void Tick()
        {
            var pointer = Pointer.current;
            var board = _session.Board;
            if (pointer == null || board == null || !_state.InputEnabled.Value)
            {
                _active = null;
                return;
            }

            if (pointer.press.wasPressedThisFrame)
            {
                _lastPoint = BoardRaycast.ScreenToBoard(_camera, pointer.position.ReadValue());
                _active = FindPiece(board, _lastPoint);
                _lockedDrag = 0f;
                _lockedReported = false;
                _moved = false;
                return;
            }

            if (!pointer.press.isPressed || _active == null || _active.IsRemoved)
            {
                if (_active != null && _moved)
                    ResolveOnRelease(board);
                _active = null;
                return;
            }

            var current = BoardRaycast.ScreenToBoard(_camera, pointer.position.ReadValue());
            var delta = DeltaFor(_active, _lastPoint, current);
            _lastPoint = current;

            if (Mathf.Approximately(delta, 0f))
                return;

            if (!board.CanMove(_active))
            {
                _lockedDrag += Mathf.Abs(delta);
                var threshold = _active.Motion == MotionType.Rotate ? LockedFeedbackDegrees : LockedFeedbackDistance;
                if (!_lockedReported && _lockedDrag >= threshold)
                {
                    _lockedReported = true;
                    _publisher.Enqueue(new PieceLockedCommand(_active));
                }
                return;
            }

            if (board.TryMove(_active, delta))
                _moved = true;
        }

        /// <summary>Pieces break only when the player lets go, based on where the dragged piece ended up.</summary>
        void ResolveOnRelease(BoardModel board)
        {
            _removed.Clear();
            board.ResolveRemovals(_removed);
            if (_removed.Count > 0)
                _publisher.Enqueue(new PiecesRemovedCommand(_removed.ToArray()));
        }

        /// <summary>Degrees around the pivot for rotating pieces, distance along the axis for sliding ones.</summary>
        static float DeltaFor(PieceModel piece, Vector2 from, Vector2 to)
        {
            switch (piece.Motion)
            {
                case MotionType.Rotate:
                    var pivot = piece.CurrentTranslation;
                    return AngleMath.Delta(AngleMath.AngleOf(from - pivot), AngleMath.AngleOf(to - pivot));
                case MotionType.Slide:
                    return Vector2.Dot(to - from, piece.SlideAxisWorld);
                default:
                    return (to - from).magnitude;
            }
        }

        static PieceModel FindPiece(BoardModel board, Vector2 point)
        {
            var tolerance = board.Pieces.Count > 0 ? board.Pieces[0].TubeRadius * HitToleranceInTubes : 0.25f;
            return board.FindPieceAt(point, tolerance);
        }
    }

    public static class BoardRaycast
    {
        /// <summary>Screen point to the board plane z = 0.</summary>
        public static Vector2 ScreenToBoard(Camera camera, Vector2 screen)
        {
            var ray = camera.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.z) < 1e-5f)
                return ray.origin;

            var t = -ray.origin.z / ray.direction.z;
            return ray.origin + ray.direction * t;
        }
    }
}
