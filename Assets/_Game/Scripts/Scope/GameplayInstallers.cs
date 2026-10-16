using System;
using GameCore.Domain.Factory;
using GameCore.Pooling;
using RotateRings.Gameplay.Data;
using RotateRings.Gameplay.Flow;
using RotateRings.Gameplay.Model;
using RotateRings.Gameplay.Presenter;
using RotateRings.Gameplay.Services;
using RotateRings.Gameplay.Shapes;
using RotateRings.Gameplay.View;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using VitalRouter.VContainer;

namespace RotateRings.Gameplay.Scope
{
    [Serializable]
    public class GameplayViewRefs
    {
        public Camera camera;
        public PieceView piecePrefab;
        public LinkView linkPrefab;
        public ShatterFxView shatterFxPrefab;
        public FloatingTextView floatingTextPrefab;
    }

    [Serializable]
    public class GameplayUiRefs
    {
        public GameplayHudView hud;
        public WinPanelView winPanel;
    }

    /// <summary>
    /// Shapes, board factory, meshes, pools and view factories: the view layer of the Gameplay scene, kept apart
    /// from the session so another scope could reuse it.
    /// </summary>
    public class GameplayViewInstaller : IInstaller
    {
        readonly GameplayViewRefs _refs;

        public GameplayViewInstaller(GameplayViewRefs refs) => _refs = refs;

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_refs.camera);
            builder.RegisterInstance(ShapeRegistry.CreateDefault());
            builder.Register<PathMeshCache>(Lifetime.Singleton);

            builder.Register<BaseObjectViewPool<PieceView>>(Lifetime.Singleton).WithParameter(_refs.piecePrefab);
            builder.Register<BaseObjectViewPool<LinkView>>(Lifetime.Singleton).WithParameter(_refs.linkPrefab);
            builder.Register<BaseObjectViewPool<ShatterFxView>>(Lifetime.Singleton).WithParameter(_refs.shatterFxPrefab);
            builder.Register<BaseObjectViewPool<FloatingTextView>>(Lifetime.Singleton).WithParameter(_refs.floatingTextPrefab);

            builder.Register<IFactory<LevelDefinition, BoardModel>, BoardModelFactory>(Lifetime.Singleton);
            builder.Register<IFactory<PieceModel, PieceView>, PieceViewFactory>(Lifetime.Singleton);
            builder.Register<IFactory<LinkModel, PieceView, LinkView>, LinkViewFactory>(Lifetime.Singleton);
        }
    }

    /// <summary>One play session: level flow, board, score, input, presenters and its own command router.</summary>
    public class GameplaySessionInstaller : IInstaller
    {
        readonly GameplayUiRefs _ui;
        readonly ILevelSource _levels;

        public GameplaySessionInstaller(GameplayUiRefs ui, ILevelSource levels)
        {
            _ui = ui;
            _levels = levels;
        }

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_levels);
            builder.RegisterComponent(_ui.hud);
            builder.RegisterComponent(_ui.winPanel);

            builder.Register<GameplayState>(Lifetime.Singleton);
            builder.Register<ScoreService>(Lifetime.Singleton);
            builder.Register<BoardSession>(Lifetime.Singleton);
            builder.Register<BoardCameraFitter>(Lifetime.Singleton);

            builder.RegisterVitalRouter(routing =>
            {
                routing.Isolated = true;
                routing.MapEntryPoint<GameplayFlow>();
                routing.Map<BoardPresenter>();
                routing.MapEntryPoint<WinPanelPresenter>();
            });

            builder.RegisterEntryPoint<HudPresenter>();
            builder.RegisterEntryPoint<PointerMoveInput>();
        }
    }
}
