using System;
using R3;

namespace RotateRings.Gameplay.Flow
{
    public class GameplayState : IDisposable
    {
        public ReactiveProperty<int> LevelIndex { get; } = new(0);
        public ReactiveProperty<bool> InputEnabled { get; } = new(false);

        public void Dispose()
        {
            LevelIndex.Dispose();
            InputEnabled.Dispose();
        }
    }
}
