using System;
using Cysharp.Threading.Tasks;
using RotateRings.Gameplay.Model;
using UnityEngine;

namespace RotateRings.Gameplay.View
{
    /// <summary>Bursts a removed piece into small tumbling shards along its tube.</summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class ShatterFxView : PooledView<ShatterFxView>
    {
        const float Lifetime = 1.1f;

        [SerializeField] ParticleSystem particles;
        [SerializeField] float shardsPerUnit = 9f;

        public void Play(PieceModel piece)
        {
            BeginUse();
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            particles.Clear();
            particles.Play();

            var samples = piece.Path.Sample(0.05f);
            var count = Mathf.Max(6, Mathf.RoundToInt(piece.Path.Length * shardsPerUnit));

            var centroid = Vector2.zero;
            foreach (var sample in samples)
                centroid += sample.Point;
            centroid /= samples.Count;

            for (var i = 0; i < count; i++)
            {
                var sample = samples[UnityEngine.Random.Range(0, samples.Count)];
                var world = piece.LocalToWorld(sample.Point);
                var away = piece.LocalDirectionToWorld(sample.Point - centroid);
                var outward = away.sqrMagnitude > 1e-4f ? away.normalized : UnityEngine.Random.insideUnitCircle.normalized;
                var color = Color.Lerp(piece.Color, Color.white, UnityEngine.Random.Range(0f, 0.25f));

                particles.Emit(new ParticleSystem.EmitParams
                {
                    position = world,
                    velocity = (Vector3)(outward * UnityEngine.Random.Range(1.5f, 3.5f))
                               + new Vector3(0f, UnityEngine.Random.Range(0.5f, 2f), UnityEngine.Random.Range(-1f, 1f)),
                    startSize = UnityEngine.Random.Range(0.09f, 0.17f),
                    startLifetime = UnityEngine.Random.Range(Lifetime * 0.7f, Lifetime),
                    startColor = color,
                    rotation3D = UnityEngine.Random.insideUnitSphere * 180f,
                    angularVelocity3D = UnityEngine.Random.insideUnitSphere * 540f,
                }, 1);
            }

            ReturnLaterAsync().Forget();
        }

        async UniTaskVoid ReturnLaterAsync()
        {
            if (await UniTask.Delay(TimeSpan.FromSeconds(Lifetime + 0.1f), cancellationToken: UseToken).SuppressCancellationThrow())
                return;

            ReturnToPool();
        }

        protected override void OnReturning() => particles.Clear();
    }
}
