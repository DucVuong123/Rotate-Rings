using System;

namespace GameCore.Pooling
{
    public interface IReturnable
    {
        void ReturnToPool();
    }

    public interface IReturnable<T> : IReturnable
    {
        Action<T> ReturnAction { get; set; }
    }
}