using System;
using R3;
using RotateRings.Gameplay.Commands;
using RotateRings.Gameplay.Flow;
using RotateRings.Gameplay.View;
using VContainer.Unity;
using VitalRouter;

namespace RotateRings.Gameplay.Presenter
{
    [Routes]
    public partial class WinPanelPresenter : IStartable, IDisposable
    {
        readonly WinPanelView _view;
        readonly GameplayState _state;
        readonly ICommandPublisher _publisher;
        readonly CompositeDisposable _disposables = new();

        public WinPanelPresenter(WinPanelView view, GameplayState state, ICommandPublisher publisher)
        {
            _view = view;
            _state = state;
            _publisher = publisher;
        }

        public void Start()
        {
            _view.ReplayButton.OnClickAsObservable()
                .Subscribe(_ => _publisher.Enqueue(new LoadLevelCommand(_state.LevelIndex.Value)))
                .AddTo(_disposables);

            _view.NextButton.OnClickAsObservable()
                .Subscribe(_ => _publisher.Enqueue(new LoadLevelCommand(_state.LevelIndex.Value + 1)))
                .AddTo(_disposables);
        }

        [Route]
        void On(LevelClearedCommand command) => _view.Show(command.Score);

        [Route]
        void On(LoadLevelCommand _) => _view.Hide();

        public void Dispose() => _disposables.Dispose();
    }
}
