using System;
using R3;
using UnityEngine;

namespace RotateRings.Gameplay.Services
{
    /// <summary>200 points per freed ring, multiplied by a combo that grows while removals keep coming quickly.</summary>
    public class ScoreService : IDisposable
    {
        public const int PointsPerRing = 200;
        public const float ComboWindow = 1.5f;

        float _lastRemovalTime = float.NegativeInfinity;

        public ReactiveProperty<int> Score { get; } = new(0);
        public int Combo { get; private set; }

        public int AddRemoval(int ringCount)
        {
            var now = Time.time;
            Combo = now - _lastRemovalTime <= ComboWindow ? Combo + 1 : 1;
            _lastRemovalTime = now;

            var gained = PointsPerRing * ringCount * Combo;
            Score.Value += gained;
            return gained;
        }

        public void Reset()
        {
            Combo = 0;
            _lastRemovalTime = float.NegativeInfinity;
            Score.Value = 0;
        }

        public void Dispose() => Score.Dispose();
    }
}
