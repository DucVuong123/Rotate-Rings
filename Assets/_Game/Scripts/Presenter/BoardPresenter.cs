using GameCore.Pooling;
using RotateRings.Gameplay.Commands;
using RotateRings.Gameplay.Flow;
using RotateRings.Gameplay.Services;
using RotateRings.Gameplay.View;
using UnityEngine;
using VitalRouter;

namespace RotateRings.Gameplay.Presenter
{
    /// <summary>Visual and score reactions to board events.</summary>
    [Routes]
    public partial class BoardPresenter
    {
        static readonly string[] Praises = { "Great!", "Excellent!", "Amazing!", "Incredible!" };

        readonly BoardSession _session;
        readonly ScoreService _score;
        readonly BaseObjectViewPool<ShatterFxView> _shatterPool;
        readonly BaseObjectViewPool<FloatingTextView> _textPool;

        public BoardPresenter(BoardSession session, ScoreService score,
            BaseObjectViewPool<ShatterFxView> shatterPool, BaseObjectViewPool<FloatingTextView> textPool)
        {
            _session = session;
            _score = score;
            _shatterPool = shatterPool;
            _textPool = textPool;
        }

        [Route]
        void On(PiecesRemovedCommand command)
        {
            _score.AddRemoval(command.Pieces.Count);

            var centroid = Vector2.zero;
            foreach (var piece in command.Pieces)
            {
                _shatterPool.Rent().Play(piece);
                _session.ReleasePiece(piece);
                centroid += piece.LocalToWorld(piece.Path.Bounds().center);
            }

            centroid /= command.Pieces.Count;
            var praise = Praises[Mathf.Clamp(_score.Combo - 1, 0, Praises.Length - 1)];
            _textPool.Rent().Play(praise, centroid);
        }

        [Route]
        void On(PieceLockedCommand command)
        {
            var view = _session.GetView(command.Piece);
            if (view)
                view.PlayShake();
        }
    }
}
