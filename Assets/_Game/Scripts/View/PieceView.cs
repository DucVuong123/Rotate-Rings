using System;
using Cysharp.Threading.Tasks;
using R3;
using RotateRings.Gameplay.Model;
using RotateRings.Gameplay.Shapes;
using UnityEngine;

namespace RotateRings.Gameplay.View
{
    /// <summary>
    /// Root (pivot + base rotation, pop scale) / Mover (the piece's one degree of freedom + shake, holds owned clips)
    /// / Body (tube mesh of the path in local space).
    /// </summary>
    public class PieceView : PooledView<PieceView>
    {
        const float ShakeDegrees = 5f;
        const float ShakeDistance = 0.06f;

        [SerializeField] Transform mover;
        [SerializeField] MeshFilter bodyFilter;
        [SerializeField] MeshRenderer bodyRenderer;

        IDisposable _offsetSubscription;
        float _offset;
        float _shake;

        public PieceModel Model { get; private set; }
        public Transform Mover => mover;

        public void Setup(PieceModel model, Mesh mesh)
        {
            BeginUse();
            Model = model;

            transform.SetPositionAndRotation(new Vector3(model.Position.x, model.Position.y, 0f),
                Quaternion.Euler(0f, 0f, model.BaseRotation));
            transform.localScale = Vector3.one;
            bodyFilter.sharedMesh = mesh;
            SetColor(model.Color);

            _shake = 0f;
            _offsetSubscription?.Dispose();
            _offsetSubscription = model.Offset.Subscribe(this, static (value, view) =>
            {
                view._offset = value;
                view.ApplyMover();
            });
        }

        public void SetColor(Color color) => ColorUtil.SetColor(bodyRenderer, color);

        void ApplyMover()
        {
            var amount = _offset + _shake;
            switch (Model?.Motion)
            {
                case MotionType.Rotate:
                    mover.localPosition = Vector3.zero;
                    mover.localRotation = Quaternion.Euler(0f, 0f, amount);
                    break;
                case MotionType.Slide:
                    mover.localPosition = (Vector3)(Model.SlideAxisLocal * amount);
                    mover.localRotation = Quaternion.identity;
                    break;
                default:
                    mover.localPosition = new Vector3(_shake, 0f, 0f);
                    mover.localRotation = Quaternion.identity;
                    break;
            }
        }

        public UniTask PlayPopInAsync(float delay)
        {
            transform.localScale = Vector3.zero;
            return Tween.RunAsync(0.35f, t => transform.localScale = Vector3.one * Tween.EaseOutBack(t), UseToken, delay);
        }

        /// <summary>Feedback when the player drags a piece that cannot move.</summary>
        public void PlayShake()
        {
            var amplitude = Model?.Motion == MotionType.Rotate ? ShakeDegrees : ShakeDistance;
            Tween.RunAsync(0.3f, t =>
            {
                _shake = Mathf.Sin(t * Mathf.PI * 6f) * amplitude * (1f - t);
                ApplyMover();
            }, UseToken).Forget();
        }

        protected override void OnReturning()
        {
            _offsetSubscription?.Dispose();
            _offsetSubscription = null;
            Model = null;
        }
    }
}
