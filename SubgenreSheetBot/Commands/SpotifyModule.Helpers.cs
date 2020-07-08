using System;
using System.Threading.Tasks;

namespace SubgenreSheetBot.Commands
{
    public partial class SpotifyModule
    {
        private async Task<string> GetIdFromUrl(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                var locals = uri.LocalPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (locals.Length == 2)
                {
                    if (locals[0] == "album")
                    {
                        return locals[1];
                    }

                    await ReplyAsync($"Url has to link to an album");
                    return "";
                }

                await ReplyAsync($"{uri} is not a valid url");
                return "";
            }

            return url;
        }

        private static string IntToKey(int key)
        {
            return key switch
            {
                0 => "C",
                1 => "C#",
                2 => "D",
                3 => "D#",
                4 => "E",
                5 => "F",
                6 => "F#",
                7 => "G",
                8 => "G#",
                9 => "A",
                10 => "A#",
                11 => "B",
                _ => "?"
            };
        }

        private static string IntToMode(int mode)
        {
            return mode switch
            {
                0 => "min",
                1 => "maj",
                _ => "?"
            };
        }

        private static Dictionary<string, FullAlbum> fullAlbumCache = new Dictionary<string, FullAlbum>();

        private async Task<FullAlbum> GetAlbumOrCache(string albumId)
        {
            if (!fullAlbumCache.TryGetValue(albumId, out var album))
            {
                album = await api.Albums.Get(albumId);
                fullAlbumCache.Add(albumId, album);
            }

            return album;
        }

        private static Dictionary<string, TrackAudioFeatures> audioFeaturesCache = new Dictionary<string, TrackAudioFeatures>();

        private async Task<TrackAudioFeatures> GetAudioFeaturesOrCache(string trackId)
        {
            if (!audioFeaturesCache.TryGetValue(trackId, out var album))
            {
                album = await api.Tracks.GetAudioFeatures(trackId);
                audioFeaturesCache.Add(trackId, album);
            }

            return album;
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
    }

    public class FullArtistComparer : IEqualityComparer<FullArtist>
    {
        public bool Equals(FullArtist x, FullArtist y)
        {
            if (x == null || y == null)
                return false;

            return string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(FullArtist obj) { return obj.Id.GetHashCode(); }
    }

    public class SimpleArtistComparer : IEqualityComparer<SimpleArtist>
    {
        public bool Equals(SimpleArtist x, SimpleArtist y)
        {
            if (x == null || y == null)
                return false;

            return string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(SimpleArtist obj)
        {
            return obj.Name.ToLower()
                .GetHashCode();
        }
    }

    public class FullAlbumComparer : IEqualityComparer<FullAlbum>
    {
        public bool Equals(FullAlbum x, FullAlbum y)
        {
            if (x == null || y == null)
                return false;

            return x.Id == y.Id;
        }

        public int GetHashCode(FullAlbum obj) { return obj.Id.GetHashCode(); }
    }
}