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

        private static async Task<FullAlbum> GetAlbum(string albumId)
        {
            using var session = SubgenreSheetBot.SpotifyStore.OpenSession();
            return await SpotifyDbUtils.GetAlbumOrCache(api, session, albumId);
        }

        private static async Task<List<FullTrack>> GetTracks(FullAlbum album)
        {
            using var session = SubgenreSheetBot.SpotifyStore.OpenSession();
            return await SpotifyDbUtils.GetTracksOrCache(api, session, album.Tracks.Items);
        }

        private static async Task<List<TrackAudioFeatures>> GetFeatures(FullAlbum album)
        {
            using var session = SubgenreSheetBot.SpotifyStore.OpenSession();
            return await SpotifyDbUtils.GetFeaturesOrCache(api, session, album.Tracks.Items);
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

        private async Task SendOrAttachment(string str, bool removeQuotes = false)
        {
            if (str.Length > 2000)
            {
                if (removeQuotes)
                    str = str.Replace("`", "");
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
}