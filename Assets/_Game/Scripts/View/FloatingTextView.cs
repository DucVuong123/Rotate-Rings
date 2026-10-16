using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace RotateRings.Gameplay.View
{
    /// <summary>World-space praise text ("Great!") that pops, rises and fades.</summary>
    public class FloatingTextView : PooledView<FloatingTextView>
    {
        [SerializeField] TMP_Text label;
        [SerializeField] float rise = 0.6f;
        [SerializeField] float duration = 0.9f;

        public void Play(string text, Vector2 position)
        {
            BeginUse();
            label.text = text;
            PlayAsync(new Vector3(position.x, position.y, -1f)).Forget();
        }

        async UniTaskVoid PlayAsync(Vector3 start)
        {
            // Captured once: the object may be destroyed (leaving Play mode) before the tween ends.
            var token = UseToken;
            var color = label.color;
            await Tween.RunAsync(duration, t =>
            {
                transform.position = start + Vector3.up * (rise * Tween.EaseOutCubic(t));
                transform.localScale = Vector3.one * Tween.EaseOutBack(Mathf.Min(1f, t * 3f));
                color.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                label.color = color;
            }, token);

            if (!token.IsCancellationRequested)
                ReturnToPool();
        }
    }
}
