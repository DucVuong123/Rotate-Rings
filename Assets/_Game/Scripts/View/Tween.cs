using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RotateRings.Gameplay.View
{
    /// <summary>
    /// Minimal UniTask-driven tweening; the project ships no tween library.
    /// Cancellation ends the tween silently (pooled views cancel when returned).
    /// </summary>
    public static class Tween
    {
        public static async UniTask RunAsync(float duration, Action<float> onUpdate, CancellationToken ct, float delay = 0f)
        {
            if (delay > 0f && await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct).SuppressCancellationThrow())
                return;

            var elapsed = 0f;
            while (elapsed < duration)
            {
                onUpdate(elapsed / duration);
                if (await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow())
                    return;
                elapsed += Time.deltaTime;
            }

            onUpdate(1f);
        }

        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            var p = t - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        public static float EaseOutCubic(float t)
        {
            var p = 1f - t;
            return 1f - p * p * p;
        }
    }
}
