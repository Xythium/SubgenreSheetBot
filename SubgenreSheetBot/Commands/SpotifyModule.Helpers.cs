using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Serilog;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Http;

namespace SubgenreSheetBot.Commands
{
    public partial class SpotifyModule
    {
        private static SpotifyClient api;

        public SpotifyModule()
        {
            if (api == null)
            {
                var config = SpotifyClientConfig.CreateDefault()
                    .WithAuthenticator(new ClientCredentialsAuthenticator(File.ReadAllText("spotify_id"), File.ReadAllText("spotify_secret")))
                    //.WithDefaultPaginator(new CachingPaginator())
                    .WithRetryHandler(new SimpleRetryHandler());

                api = new SpotifyClient(config);
            }
        }

        private (string trackId, string albumId) GetIdFromUrl(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                var locals = uri.LocalPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (locals.Length == 2)
                {
                    if (locals[0] == "album")
                        return (null, locals[1]);

                    if (locals[0] == "track")
                        return (locals[1], null);

                    throw new InvalidDataException($"Unsupported link type: {locals[0]}");
                }

                throw new InvalidDataException($"Invalid link: {string.Join(" / ", locals)}");
            }

            throw new InvalidDataException($"Not a url: {url}");
        }

        private static readonly Dictionary<string, FullAlbum> fullAlbumCache = new Dictionary<string, FullAlbum>();

        private static async Task<AlbumCacheResult> GetAlbumOrCache(string albumId)
        {
            if (!fullAlbumCache.TryGetValue(albumId, out var album))
            {
                album = await api.Albums.Get(albumId);
                fullAlbumCache.Add(albumId, album);
                return new AlbumCacheResult
                {
                    Album = album,
                    Cached = false
                };
            }

            return new AlbumCacheResult
            {
                Album = album,
                Cached = true
            };
        }

        private async Task<IUserMessage> UpdateOrSend(IUserMessage message, string str)
        {
            if (message == null)
            {
                return message = await ReplyAsync(str);
            }

            await message.ModifyAsync(m => m.Content = str);
            return message;
        }

        private async Task SendOrAttachment(string str)
        {
            if (str.Length > 2000)
            {
                var writer = new MemoryStream(Encoding.UTF8.GetBytes(str));
                await Context.Channel.SendFileAsync(writer, "content.txt", $"Message too long");
            }
            else
            {
                await ReplyAsync(str);
            }
        }

        private async Task<FullPlaylist> CreateOrUpdatePlaylist(string name, FullAlbum[] albums)
        {
            var playlist = await FindUserPlaylist(name);

            await api.Playlists.ReplaceItems(playlist.Id, new PlaylistReplaceItemsRequest(albums.SelectMany(a => a.Tracks.Items.Select(t => t.Id))
                .ToList()));

            return playlist;
        }

        private static async Task<FullPlaylist> FindUserPlaylist(string name)
        {
            //bug: getting the current user will not work
            var currentPlaylists = await api.Playlists.CurrentUsers();

            await foreach (var playlist in api.Paginate(currentPlaylists))
            {
                if (string.Equals(playlist.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return await api.Playlists.Get(playlist.Id);
                }
            }

            return await api.Playlists.Create((await api.UserProfile.Current()).Id, new PlaylistCreateRequest(name));
        }
    }

    internal class AlbumCacheResult
    {
        public FullAlbum Album { get; set; }

        public bool Cached { get; set; }
    }
}