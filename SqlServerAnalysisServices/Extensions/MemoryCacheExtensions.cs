using System.Runtime.Caching;

namespace SqlServerAnalysisServices.Extensions;

internal static class MemoryCacheExtensions
{
    public static T GetOrAdd<T>(
        this MemoryCache cache,
        string key,
        Func<T> valueFactory,
        TimeSpan? absoluteExpirationFromNow = null
    )
    {
        if (cache.Get(key) is T cached)
        {
            return cached;
        }

        var value = valueFactory();

        if (value is not null)
        {
            cache.Set(key, value, CreatePolicy(absoluteExpirationFromNow));
        }

        return value;
    }

    public static async ValueTask<T> GetOrAddAsync<T>(
        this MemoryCache cache,
        string key,
        Func<ValueTask<T>> valueFactory,
        TimeSpan? absoluteExpirationFromNow = null
    )
    {
        if (cache.Get(key) is T cached)
        {
            return cached;
        }

        var value = await valueFactory();

        if (value is not null)
        {
            cache.Set(key, value, CreatePolicy(absoluteExpirationFromNow));
        }

        return value;
    }

    private static CacheItemPolicy CreatePolicy(TimeSpan? absoluteExpirationFromNow)
    {
        return absoluteExpirationFromNow.HasValue
            ? new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.Now.Add(absoluteExpirationFromNow.Value) }
            : new CacheItemPolicy();
    }
}