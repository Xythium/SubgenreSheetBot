using System;
using System.Collections.Generic;
using System.Linq;
using BeatportApi.Beatport;
using BeatportApi.Beatsource;
using Common.AppleMusic;
using Common.AppleMusic.Api;
using Common.Monstercat;
using Serilog;
using SpotifyAPI.Web;

namespace Common
{
    public class GenericAlbum
    {
        public List<GenericTrack> Tracks { get; set; }

        public List<GenericFreeDownload> FreeDownloads { get; set; }

        public string FirstBillingArtist { get; set; }

        public List<string> Artists { get; set; }

        public string Name { get; set; }

        public string Url { get; set; }

        public string Image { get; set; }

        public string ReleaseDate { get; set; }

        public string Barcode { get; set; }

        public string CatalogNumber { get; set; }

        public string Description { get; set; }

        public string Label { get; set; }

        public static GenericAlbum FromAlbum(Catalog catalog)
        {
            if (catalog.D.Count > 1)
                throw new Exception("Catalog has more than one album");

            var album = catalog.D.First();
            var tracks = album.Relationships.Tracks.Data.OrderBy(s => s.Attributes.DiscNumber)
                .ThenBy(s => s.Attributes.TrackNumber)
                .ToArray();

            var songs = new List<GenericTrack>();

            for (var i = 0; i < tracks.Length; i++)
            {
                var song = tracks[i];
                var track = GenericTrack.FromTrack(song);
                track.Number = i + 1;
                songs.Add(track);
            }

            var genericAlbum = new GenericAlbum
            {
                Artists = album.Relationships.Artists.Data.Select(a => a.Attributes.Name)
                    .ToList(),
                FirstBillingArtist = album.Attributes.ArtistName,
                Barcode = album.Attributes.Upc,
                CatalogNumber = null,
                Description = null,
                Image = album.Attributes.Artwork.Url.Replace("{w}", album.Attributes.Artwork.Width.ToString())
                    .Replace("{h}", album.Attributes.Artwork.Height.ToString())
                    .Replace("{f}", "jpg"),
                Name = album.Attributes.Name,
                ReleaseDate = album.Attributes.ReleaseDate,
                Url = null, // todo
                Label = album.Attributes.RecordLabel,
                Tracks = songs,
                FreeDownloads = new List<GenericFreeDownload>()
            };

            return genericAlbum;
        }

        public static GenericAlbum FromAlbum(FullAlbum album, List<FullTrack> tracks, List<TrackAudioFeatures> features)
        {
            var images = album.Images.OrderByDescending(i => i.Width)
                .ToArray();

            if (album.Tracks.Items is null)
                throw new Exception("Album has no tracks");

            var genericAlbum = new GenericAlbum
            {
                Artists = album.Artists.Select(a => a.Name)
                    .ToList(),
                FirstBillingArtist = album.Artists.First()
                    .Name,
                Barcode = album.ExternalIds.ContainsKey("upc") ? album.ExternalIds["upc"] : null,
                CatalogNumber = null,
                Description = null,
                Image = images.FirstOrDefault()
                    ?.Url,
                Name = album.Name,
                ReleaseDate = album.ReleaseDate,
                Url = album.ExternalUrls["spotify"],
                Label = album.Label,
                Tracks = tracks.Select(t => GenericTrack.FromTrack(t, features.SingleOrDefault(f => f.Uri == t.Uri)))
                    .ToList(),
                FreeDownloads = new List<GenericFreeDownload>()
            };

            return genericAlbum;
        }

        public static GenericAlbum FromAlbum(BeatportRelease album, List<BeatportTrack> tracks)
        {
            var images = album.Image.DynamicUri.Replace("{w}", "1400")
                .Replace("{h}", "1400");

            var genericAlbum = new GenericAlbum
            {
                Artists = album.Artists.Select(a => a.Name)
                    .ToList(),
                FirstBillingArtist = album.Artists.First()
                    .Name,
                Barcode = album.Upc,
                CatalogNumber = album.CatalogNumber,
                Description = album.Description,
                Image = images,
                Name = album.Name,
                ReleaseDate = album.NewReleaseDate.ToString("yyyy-MM-dd"),
                Url = $"https://www.beatport.com/release/{album.Slug}/{album.Id}",
                Label = album.Label.Name,
                Tracks = tracks.Select(GenericTrack.FromTrack)
                    .ToList(),
                FreeDownloads = tracks.SelectMany(t => t.FreeDownloads)
                    .Select(GenericFreeDownload.FromTrack)
                    .ToList()
            };

            return genericAlbum;
        }

        public static GenericAlbum FromAlbum(BeatsourceRelease album, List<BeatsourceTrack> tracks)
        {
            var images = album.Image.DynamicUri.Replace("{w}", "1400")
                .Replace("{h}", "1400");

            var genericAlbum = new GenericAlbum
            {
                Artists = album.Artists.Select(a => a.Name)
                    .ToList(),
                FirstBillingArtist = album.Artists.First()
                    .Name,
                Barcode = album.Upc,
                CatalogNumber = album.CatalogNumber,
                Description = album.Description,
                Image = images,
                Name = album.Name,
                ReleaseDate = album.NewReleaseDate.ToString("yyyy-MM-dd"),
                Url = $"https://www.beatsource.com/release/{album.Slug}/{album.Id}",
                Label = album.Label.Name,
                Tracks = tracks.Select(GenericTrack.FromTrack)
                    .ToList()
            };

            return genericAlbum;
        }

        public static GenericAlbum FromAlbum(MonstercatReleaseSummary album, List<MonstercatTrack> tracks)
        {
            var genericAlbum = new GenericAlbum
            {
                Artists = new List<string>
                {
                    album.ArtistsTitle
                },
                FirstBillingArtist = album.ArtistsTitle,
                Barcode = album.Upc,
                CatalogNumber = album.CatalogId,
                Description = album.Description,
                Image = $"https://cdx.monstercat.com/?width=256&encoding=webp&url=https://www.monstercat.com/release/{album.CatalogId}/cover",
                Name = album.Title,
                ReleaseDate = album.ReleaseDate.ToString("yyyy-MM-dd"),
                Url = $"https://player.monstercat.app/release/{album.CatalogId}",
                Label = $"Monstercat {string.Join(", ", tracks.Select(t => t.Brand).Distinct())}",
                Tracks = tracks.Select(GenericTrack.FromTrack)
                    .ToList()
            };

            return genericAlbum;
        }

        public static GenericAlbum FromAlbum(MonstercatFullRelease album)
        {
            var genericAlbum = new GenericAlbum
            {
                Artists = new List<string>
                {
                    album.ArtistsTitle
                },
                FirstBillingArtist = album.ArtistsTitle,
                Barcode = album.Upc,
                CatalogNumber = album.CatalogId,
                Description = album.Description,
                Image = $"https://cdx.monstercat.com/?width=256&encoding=webp&url=https://www.monstercat.com/release/{album.CatalogId}/cover",
                Name = album.Title,
                ReleaseDate = album.ReleaseDate.ToString("yyyy-MM-dd"),
                Url = $"https://player.monstercat.app/release/{album.CatalogId}",
                Label = $"Monstercat {string.Join(", ", album.Tracks.Select(t => t.Brand).Distinct())}",
                Tracks = album.Tracks.Select(GenericTrack.FromTrack)
                    .ToList()
            };

            return genericAlbum;
        }
    }
}