using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Http;

namespace Common.Spotify
{
    public class CachingPaginator : IPaginator
    {
        public Task<IList<T>> PaginateAll<T>(IPaginatable<T> firstPage, IAPIConnector connector) { throw new NotImplementedException(); }

        public Task<IList<T>> PaginateAll<T, TNext>(IPaginatable<T, TNext> firstPage, Func<TNext, IPaginatable<T, TNext>> mapper, IAPIConnector connector) { throw new NotImplementedException(); }

        public async IAsyncEnumerable<T> Paginate<T>(IPaginatable<T> firstPage, IAPIConnector connector, [EnumeratorCancellation] CancellationToken cancel = new())
        {
            if (firstPage is null)
                throw new ArgumentNullException(nameof(firstPage));
            if (connector is null)
                throw new ArgumentNullException(nameof(connector));

            var page = firstPage;

            foreach (var item in page.Items)
            {
                yield return item;
            }

            while (!string.IsNullOrWhiteSpace(page.Next))
            {
                page = await connector.Get<Paging<T>>(new Uri(page.Next, UriKind.Absolute))
                    .ConfigureAwait(false);

                foreach (var item in page.Items!)
                {
                    yield return item;
                }
            }
        }

        public async IAsyncEnumerable<T> Paginate<T, TNext>(IPaginatable<T, TNext> firstPage, Func<TNext, IPaginatable<T, TNext>> mapper, IAPIConnector connector, [EnumeratorCancellation] CancellationToken cancel = new())
        {
            if (firstPage is null)
                throw new ArgumentNullException(nameof(firstPage));
            if (mapper is null)
                throw new ArgumentNullException(nameof(mapper));
            if (connector is null)
                throw new ArgumentNullException(nameof(connector));

            var page = firstPage;

            foreach (var item in page.Items!)
            {
                yield return item;
            }

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            while (!string.IsNullOrWhiteSpace(page.Next))
            {
                try
                {
                    var next = await connector.Get<TNext>(new Uri(page.Next, UriKind.Absolute));

                    page = mapper(next);
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "Error ");
                    yield break;
                }

                foreach (var item in page.Items!)
                {
                    yield return item;
                }
            }
        }
    }
}