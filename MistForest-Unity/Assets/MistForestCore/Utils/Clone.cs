namespace MistForest.Core.Utils
{
    public interface IClone<T>
        where T : IClone<T>
    {
        T Clone();
    }
}
