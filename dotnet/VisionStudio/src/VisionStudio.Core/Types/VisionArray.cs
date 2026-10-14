using System.Collections;

namespace VisionStudio.Core.Types;

/// <summary>
/// First-class typed array type for industrial pipelines to process batches of numbers, points, or measurements.
/// </summary>
public sealed class VisionArray<T> : IReadOnlyList<T>
{
    private readonly List<T> _items;

    public int Count => _items.Count;
    public T this[int index] => _items[index];

    public VisionArray()
    {
        _items = new List<T>();
    }

    public VisionArray(IEnumerable<T> items)
    {
        _items = items.ToList();
    }

    public void Add(T item) => _items.Add(item);
    public void AddRange(IEnumerable<T> items) => _items.AddRange(items);

    public VisionArray<T> Filter(Func<T, bool> predicate)
    {
        return new VisionArray<T>(_items.Where(predicate));
    }

    public VisionArray<TResult> Map<TResult>(Func<T, TResult> selector)
    {
        return new VisionArray<TResult>(_items.Select(selector));
    }

    public VisionArray<T> Sort(Comparison<T>? comparison = null)
    {
        var copy = new List<T>(_items);
        if (comparison != null)
        {
            copy.Sort(comparison);
        }
        else
        {
            copy.Sort();
        }
        return new VisionArray<T>(copy);
    }

    public VisionArray<T> Slice(int start, int count)
    {
        int actualStart = Math.Clamp(start, 0, _items.Count);
        int actualCount = Math.Clamp(count, 0, _items.Count - actualStart);
        return new VisionArray<T>(_items.GetRange(actualStart, actualCount));
    }

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
}

/// <summary>
/// Heterogeneous or object-based VisionArray convenience utilities.
/// </summary>
public static class VisionArrayExtensions
{
    public static double Sum(this VisionArray<double> array) => array.Count > 0 ? Enumerable.Sum(array) : 0;
    public static double Average(this VisionArray<double> array) => array.Count > 0 ? Enumerable.Average(array) : 0;
    public static double Min(this VisionArray<double> array) => array.Count > 0 ? Enumerable.Min(array) : 0;
    public static double Max(this VisionArray<double> array) => array.Count > 0 ? Enumerable.Max(array) : 0;
}
