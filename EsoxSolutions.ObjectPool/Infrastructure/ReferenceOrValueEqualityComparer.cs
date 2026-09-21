using System.Runtime.CompilerServices;

namespace EsoxSolutions.ObjectPool.Infrastructure;

internal sealed class ReferenceOrValueEqualityComparer<T> : IEqualityComparer<T>
{
    public static ReferenceOrValueEqualityComparer<T> Instance { get; } = new();

    public bool Equals(T? x, T? y)
    {
        if (typeof(T).IsValueType)
        {
            return EqualityComparer<T>.Default.Equals(x, y);
        }

        return ReferenceEquals(x, y);
    }

    public int GetHashCode(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return typeof(T).IsValueType
            ? EqualityComparer<T>.Default.GetHashCode(value)
            : RuntimeHelpers.GetHashCode(value!);
    }
}
