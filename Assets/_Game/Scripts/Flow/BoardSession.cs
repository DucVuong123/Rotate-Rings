using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameCore.Domain.Factory;
using RotateRings.Gameplay.Data;
using RotateRings.Gameplay.Model;
using RotateRings.Gameplay.View;

namespace RotateRings.Gameplay.Flow
{
    /// <summary>The board currently being played and the views that show it.</summary>
    public class BoardSession : IDisposable
    {
        readonly IFactory<LevelDefinition, BoardModel> _boardFactory;
        readonly IFactory<PieceModel, PieceView> _pieceViewFactory;
        readonly IFactory<LinkModel, PieceView, LinkView> _linkViewFactory;

        readonly Dictionary<PieceModel, PieceView> _pieceViews = new();
        readonly Dictionary<LinkModel, LinkView> _linkViews = new();

        public BoardModel Board { get; private set; }

        public BoardSession(
            IFactory<LevelDefinition, BoardModel> boardFactory,
            IFactory<PieceModel, PieceView> pieceViewFactory,
            IFactory<LinkModel, PieceView, LinkView> linkViewFactory)
        {
            _boardFactory = boardFactory;
            _pieceViewFactory = pieceViewFactory;
            _linkViewFactory = linkViewFactory;
        }

        public UniTask LoadAsync(LevelDefinition level)
        {
            Clear();
            Board = _boardFactory.Create(level);

            foreach (var piece in Board.Pieces)
                _pieceViews.Add(piece, _pieceViewFactory.Create(piece));

            foreach (var link in Board.Links)
                _linkViews.Add(link, _linkViewFactory.Create(link, link.Owner != null ? _pieceViews[link.Owner] : null));

            var popIns = new List<UniTask>(_pieceViews.Count);
            var index = 0;
            foreach (var view in _pieceViews.Values)
                popIns.Add(view.PlayPopInAsync(0.04f * index++));

            return UniTask.WhenAll(popIns);
        }

        public PieceView GetView(PieceModel piece) => piece != null && _pieceViews.TryGetValue(piece, out var view) ? view : null;

        /// <summary>
        /// Returns a removed piece's view, the clips it owns and the pins that held it.
        /// Clips of other pieces that gripped it stay on their owners.
        /// </summary>
        public void ReleasePiece(PieceModel piece)
        {
            foreach (var link in Board.Links)
            {
                var goesWithPiece = link.Owner == piece || link.IsPin && link.Target == piece;
                if (goesWithPiece && _linkViews.Remove(link, out var linkView) && linkView)
                    linkView.ReturnToPool();
            }

            if (_pieceViews.Remove(piece, out var pieceView) && pieceView)
                pieceView.ReturnToPool();
        }

        public void Clear()
        {
            foreach (var view in _linkViews.Values)
                if (view) view.ReturnToPool();
            _linkViews.Clear();

            foreach (var view in _pieceViews.Values)
                if (view) view.ReturnToPool();
            _pieceViews.Clear();

            Board?.Dispose();
            Board = null;
        }

        public void Dispose() => Clear();
    }
}
