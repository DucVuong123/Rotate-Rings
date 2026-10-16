using System;
using R3;
using RotateRings.Gameplay.Flow;
using RotateRings.Gameplay.Services;
using RotateRings.Gameplay.View;
using VContainer.Unity;

namespace RotateRings.Gameplay.Presenter
{
    public class HudPresenter : IStartable, IDisposable
    {
        readonly GameplayHudView _view;
        readonly GameplayState _state;
        readonly ScoreService _score;
        readonly CompositeDisposable _disposables = new();

        public HudPresenter(GameplayHudView view, GameplayState state, ScoreService score)
        {
            _view = view;
            _state = state;
            _score = score;
        }

        public void Start()
        {
            _state.LevelIndex.Subscribe(index => _view.SetLevel(index + 1)).AddTo(_disposables);
            _score.Score.Subscribe(_view.SetScore).AddTo(_disposables);
        }

        public void Dispose() => _disposables.Dispose();
    }
}
