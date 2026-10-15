using Cysharp.Threading.Tasks;

namespace GameCore.Domain.Factory
{
    public interface IAsyncFactory<TOut>
    {
        UniTask<TOut> CreateAsync();
    }

    public interface IAsyncFactory<in TIn, TOut>
    {
        UniTask<TOut> CreateAsync(TIn param1);
    }

    public interface IAsyncFactory<in TIn1, in TIn2, TOut>
    {
        UniTask<TOut> CreateAsync(TIn1 param1, TIn2 param2);
    }

    public interface IAsyncFactory<in TIn1, in TIn2, in TIn3, TOut>
    {
        UniTask<TOut> CreateAsync(TIn1 param1, TIn2 param2, TIn3 param3);
    }

    public interface IAsyncFactory<in TIn1, in TIn2, in TIn3, in TIn4, TOut>
    {
        UniTask<TOut> CreateAsync(TIn1 param1, TIn2 param2, TIn3 param3, TIn4 param4);
    }
}