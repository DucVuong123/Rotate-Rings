using RotateRings.Gameplay.Model;
using UnityEngine;

namespace RotateRings.Gameplay.View
{
    /// <summary>
    /// Clip: a stem from the owner's tube to a head wrapped around the target's tube; lives under the owner's
    /// Mover so it moves with the owner. Pin: a silver sleeve fixed on the board around the target's tube.
    /// Local +Y of the root follows the target's tube at the grip point.
    /// </summary>
    public class LinkView : PooledView<LinkView>
    {
        const float StemDiameter = 0.07f;
        const float HeadTubeScale = 1.6f;
        const float PinTubeScale = 1.9f;
        const float PinLengthScale = 1.5f;

        static readonly Color PinColor = new(0.8f, 0.82f, 0.86f);

        [SerializeField] Transform stem;
        [SerializeField] Renderer stemRenderer;
        [SerializeField] Transform head;
        [SerializeField] Renderer headRenderer;

        public LinkModel Model { get; private set; }

        public void Setup(LinkModel link, PieceView ownerView)
        {
            BeginUse();
            Model = link;

            var target = link.Target;
            var projection = target.Path.Project(target.WorldToLocal(link.GripPoint));
            var tangentWorld = target.LocalDirectionToWorld(target.Path.TangentAt(projection.S));
            var tubeDiameter = target.TubeRadius * 2f;

            if (link.IsPin || ownerView == null)
            {
                transform.SetParent(null, false);
                transform.SetPositionAndRotation(link.GripPoint, RotationAlong(tangentWorld));
                transform.localScale = Vector3.one;

                head.localPosition = Vector3.zero;
                head.localRotation = Quaternion.identity;
                head.localScale = new Vector3(tubeDiameter * PinTubeScale, link.HalfSize * 2f * PinLengthScale, tubeDiameter * PinTubeScale);
                stem.gameObject.SetActive(false);
                ColorUtil.SetColor(headRenderer, PinColor);
                return;
            }

            var owner = link.Owner;
            transform.SetParent(ownerView.Mover, false);
            transform.localScale = Vector3.one;

            // Everything below is in the owner's local frame (the Mover's space).
            var gripLocal = owner.WorldToLocal(link.GripPoint);
            var anchorLocal = owner.Path.PointAt(owner.Path.Project(gripLocal).S);
            var tangentLocal = PieceModel.Rotate(tangentWorld, -owner.CurrentRotation);

            transform.localPosition = gripLocal;
            transform.localRotation = RotationAlong(tangentLocal);

            var headDepth = tubeDiameter * HeadTubeScale;
            head.localPosition = Vector3.zero;
            head.localRotation = Quaternion.identity;
            head.localScale = new Vector3(headDepth, link.HalfSize * 2f, headDepth);

            var toAnchor = Quaternion.Inverse(transform.localRotation) * (Vector3)(anchorLocal - gripLocal);
            var stemLength = toAnchor.magnitude;
            stem.gameObject.SetActive(stemLength > 0.02f);
            if (stemLength > 0.02f)
            {
                stem.localPosition = toAnchor * 0.5f;
                stem.localRotation = Quaternion.FromToRotation(Vector3.up, toAnchor / stemLength);
                stem.localScale = new Vector3(StemDiameter, stemLength * 0.5f, StemDiameter);
            }

            ColorUtil.SetColor(stemRenderer, owner.Color);
            ColorUtil.SetColor(headRenderer, ColorUtil.Shade(owner.Color, 0.85f));
        }

        static Quaternion RotationAlong(Vector2 tangent) =>
            Quaternion.Euler(0f, 0f, AngleMath.AngleOf(tangent) - 90f);

        protected override void OnReturning() => Model = null;
    }
}
