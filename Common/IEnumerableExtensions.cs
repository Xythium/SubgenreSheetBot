using System;
using System.Collections.Generic;
using System.Linq;

namespace Common
{
    /*public static class EnumerableExtensions
    {
        public static IEnumerable<TSource> ExceptBy<TSource, TSelector>(this IEnumerable<TSource> first, IEnumerable<TSelector> second, Func<TSource, TSelector> selector)
        {
            if (first == null)
            {
                throw new ArgumentNullException(nameof(first));
            }

            if (second == null)
            {
                throw new ArgumentNullException(nameof(second));
            }

            return ExceptIterator(first, second, selector, null);
        }

        public static IEnumerable<TSource> ExceptBy<TSource, TSelector>(this IEnumerable<TSource> first, IEnumerable<TSelector> second, Func<TSource, TSelector> selector, IEqualityComparer<TSelector> comparer)
        {
            if (first == null)
            {
                throw new ArgumentNullException(nameof(first));
            }

            if (second == null)
            {
                throw new ArgumentNullException(nameof(second));
            }

            return ExceptIterator(first, second, selector, comparer);
        }

        private static IEnumerable<TSource> ExceptIterator<TSource, TSelector>(IEnumerable<TSource> first, IEnumerable<TSelector> second, Func<TSource, TSelector> selector, IEqualityComparer<TSelector> comparer)
        {
            var set = new HashSet<TSelector>(comparer);
            set.UnionWith(second);

            foreach (var element in first)
            {
                if (set.Add(selector(element)))
                {
                    yield return element;
                }
            }
        }
    }*/
}