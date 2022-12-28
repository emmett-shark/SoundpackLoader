using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoundpackLoader;

internal static class CollectionExtensions
{
    public static T GetRandom<T>(this IEnumerable<T> vals)
    {
        return vals.OrderBy(x => Guid.NewGuid()).First();
    }

    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source) where T : class =>
        from item in source
        where item is not null
        select item;

    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source) where T : struct =>
        from item in source
        where item.HasValue
        select item.Value;
}
