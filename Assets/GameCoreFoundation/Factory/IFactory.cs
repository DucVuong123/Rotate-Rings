namespace GameCore.Domain.Factory
{
    public interface IFactory<out TOut>
    {
        TOut Create();
    }

    public interface IFactory<in TIn, out TOut>
    {
        TOut Create(TIn param1);
    }

    public interface IFactory<in TIn1, in TIn2, out TOut>
    {
        TOut Create(TIn1 param1, TIn2 param2);
    }

    public interface IFactory<in TIn1, in TIn2, in TIn3, out TOut>
    {
        TOut Create(TIn1 param1, TIn2 param2, TIn3 param3);
    }

    public interface IFactory<in TIn1, in TIn2, in TIn3, in TIn4, out TOut>
    {
        TOut Create(TIn1 param1, TIn2 param2, TIn3 param3, TIn4 param4);
    }
}