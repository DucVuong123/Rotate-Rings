using System;
using RotateRings.Gameplay.Model;
using UnityEngine;

namespace RotateRings.Gameplay.Flow
{
    /// <summary>
    /// Zooms the gameplay camera out so a whole level fits on screen, leaving room for the HUD at the top.
    /// Small levels keep the scene's framing; the original framing is restored when the session ends.
    /// </summary>
    public class BoardCameraFitter : IDisposable
    {
        const float SideMargin = 0.35f;
        const float BottomMargin = 0.35f;

        /// <summary>World height kept free under the top edge for the level label and score.</summary>
        const float HudHeight = 1.4f;

        readonly Camera _camera;
        readonly float _defaultSize;
        readonly Vector3 _defaultPosition;

        public BoardCameraFitter(Camera camera)
        {
            _camera = camera;
            _defaultSize = camera.orthographicSize;
            _defaultPosition = camera.transform.position;
        }

        public void Fit(BoardModel board)
        {
            if (!TryGetBounds(board, out var min, out var max))
                return;

            var width = max.x - min.x + SideMargin * 2f;
            var height = max.y - min.y + BottomMargin + HudHeight;
            var size = Mathf.Max(_defaultSize, height * 0.5f, width * 0.5f / _camera.aspect);

            // Keep the default view when the level fits in it; otherwise center on the level under the HUD.
            if (size <= _defaultSize + 1e-3f && FitsDefaultView(min, max))
            {
                _camera.orthographicSize = _defaultSize;
                _camera.transform.position = _defaultPosition;
                return;
            }

            _camera.orthographicSize = size;
            var centerY = (min.y - BottomMargin + max.y + HudHeight) * 0.5f;
            _camera.transform.position = new Vector3((min.x + max.x) * 0.5f, centerY, _defaultPosition.z);
        }

        bool FitsDefaultView(Vector2 min, Vector2 max)
        {
            var halfHeight = _defaultSize;
            var halfWidth = _defaultSize * _camera.aspect;
            var center = (Vector2)_defaultPosition;
            return min.x >= center.x - halfWidth + SideMargin && max.x <= center.x + halfWidth - SideMargin
                && min.y >= center.y - halfHeight + BottomMargin && max.y <= center.y + halfHeight - HudHeight;
        }

        static bool TryGetBounds(BoardModel board, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            var any = false;

            foreach (var piece in board.Pieces)
            {
                foreach (var sample in piece.Path.Sample(0.2f))
                {
                    var world = piece.LocalToWorld(sample.Point);
                    min = Vector2.Min(min, world - Vector2.one * piece.TubeRadius);
                    max = Vector2.Max(max, world + Vector2.one * piece.TubeRadius);
                    any = true;
                }
            }

            return any;
        }

        public void Dispose()
        {
            if (_camera == null)
                return;

            _camera.orthographicSize = _defaultSize;
            _camera.transform.position = _defaultPosition;
        }
    }
}
