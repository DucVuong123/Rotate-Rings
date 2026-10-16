using GameCore.Domain.Factory;
using GameCore.Pooling;
using RotateRings.Gameplay.Model;

namespace RotateRings.Gameplay.View
{
    public class PieceViewFactory : IFactory<PieceModel, PieceView>
    {
        readonly BaseObjectViewPool<PieceView> _pool;
        readonly PathMeshCache _meshes;

        public PieceViewFactory(BaseObjectViewPool<PieceView> pool, PathMeshCache meshes)
        {
            _pool = pool;
            _meshes = meshes;
        }

        public PieceView Create(PieceModel model)
        {
            var view = _pool.Rent();
            view.Setup(model, _meshes.Get(model));
            return view;
        }
    }

    /// <summary>Creates a clip under its owner's view, or a pin on the board when ownerView is null.</summary>
    public class LinkViewFactory : IFactory<LinkModel, PieceView, LinkView>
    {
        readonly BaseObjectViewPool<LinkView> _pool;

        public LinkViewFactory(BaseObjectViewPool<LinkView> pool) => _pool = pool;

        public LinkView Create(LinkModel model, PieceView ownerView)
        {
            var view = _pool.Rent();
            view.Setup(model, ownerView);
            return view;
        }
    }
}
