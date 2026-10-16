using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using RotateRings.Gameplay.Commands;
using RotateRings.Gameplay.Data;
using RotateRings.Gameplay.Services;
using RotateRings.Gameplay.Shapes;
using UnityEngine;
using VContainer.Unity;
using VitalRouter;

namespace RotateRings.Gameplay.Flow
{
    /// <summary>Loads levels and detects the win.</summary>
    [Routes]
    public partial class GameplayFlow : IAsyncStartable, IDisposable
    {
        const float WinDelaySeconds = 0.9f;

        readonly ILevelSource _levels;
        readonly ShapeRegistry _shapes;
        readonly BoardSession _session;
        readonly GameplayState _state;
        readonly ScoreService _score;
        readonly ICommandPublisher _publisher;
        readonly BoardCameraFitter _cameraFitter;

        CancellationTokenSource _levelCts;

        public GameplayFlow(ILevelSource levels, ShapeRegistry shapes, BoardSession session, GameplayState state,
            ScoreService score, ICommandPublisher publisher, BoardCameraFitter cameraFitter)
        {
            _levels = levels;
            _shapes = shapes;
            _session = session;
            _state = state;
            _score = score;
            _publisher = publisher;
            _cameraFitter = cameraFitter;
        }

        public UniTask StartAsync(CancellationToken cancellation) => LoadLevelAsync(0);

        [Route]
        void On(LoadLevelCommand command) => LoadLevelAsync(command.LevelIndex).Forget();

        [Route]
        void On(PiecesRemovedCommand _)
        {
            if (_session.Board is { IsCleared: true })
                WinAsync(_levelCts.Token).Forget();
        }

        async UniTask LoadLevelAsync(int index)
        {
            _levelCts?.Cancel();
            _levelCts?.Dispose();
            var levelCts = _levelCts = new CancellationTokenSource();

            var wrapped = (index % _levels.Count + _levels.Count) % _levels.Count;
            _state.InputEnabled.Value = false;
            _state.LevelIndex.Value = wrapped;
            _score.Reset();

            var level = _levels.Get(wrapped);
            foreach (var issue in LevelValidator.Validate(level, _shapes))
                Debug.LogWarning($"[{level.name}] {issue.Message}");

            var loading = _session.LoadAsync(level);
            _cameraFitter.Fit(_session.Board);
            await loading;

            // A newer load may have started while this board was popping in.
            if (!levelCts.IsCancellationRequested)
                _state.InputEnabled.Value = true;
        }

        async UniTaskVoid WinAsync(CancellationToken ct)
        {
            _state.InputEnabled.Value = false;

            if (await UniTask.Delay(TimeSpan.FromSeconds(WinDelaySeconds), cancellationToken: ct).SuppressCancellationThrow())
                return;

            _publisher.Enqueue(new LevelClearedCommand(_state.LevelIndex.Value, _score.Score.Value));
        }

        public void Dispose()
        {
            _levelCts?.Cancel();
            _levelCts?.Dispose();
        }
    }
}
