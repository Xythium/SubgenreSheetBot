﻿using System;
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

        private static readonly Dictionary<string, TrackAudioFeatures> audioFeaturesCache = new Dictionary<string, TrackAudioFeatures>();

        private static async Task<TrackAudioFeatures> GetAudioFeaturesOrCache(string trackId)
        {
            if (!audioFeaturesCache.TryGetValue(trackId, out var album))
            {
                try
                {
                    album = await api.Tracks.GetAudioFeatures(trackId);
                    audioFeaturesCache.Add(trackId, album);
                }
                catch (APIException ex) when (ex.Message == "analysis not found")
                {
                    return null;
                }
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

        private static string FormatTrack(SimpleTrack track, TrackAudioFeatures features)
        {
            var featureList = new List<string>
            {
                TimeSpan.FromMilliseconds(track.DurationMs)
                    .ToString("g")
            };

            if (features != null)
            {
                featureList.Add(BpmToString(features));

                if (features.TimeSignature != 4)
                {
                    featureList.Add($"{features.TimeSignature}/4");
                }

                featureList.Add($"{IntToKey(features.Key)} {IntToMode(features.Mode)}");
            }
            else
            {
                //featureList.Add("**track analysis not available**");
            }

            var sb = new StringBuilder($"{track.TrackNumber}. {track.Name} [{string.Join(", ", featureList)}]");

            return sb.ToString();
        }

        private static string BpmToString(TrackAudioFeatures features)
        {
            var bpm = features.Tempo;
            var floating = bpm - (int) bpm;

            if (floating < 0.3 || floating > 0.7)
            {
                features.Tempo = bpm = (float) Math.Round(bpm);
            }
            else
            {
                features.Tempo = bpm = (float) Math.Round(bpm, 1);
            }

            if (Math.Abs(floating - 0.5) < 0.03 && bpm < 95)
            {
                bpm *= 2;
            }

            var str = bpm.ToString(CultureInfo.GetCultureInfo("en-US"));

            if (bpm != features.Tempo)
            {
                str = $"{features.Tempo} (Mark says {bpm})";
            }

            return str;
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