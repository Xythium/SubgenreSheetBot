using System;
using System.Collections.Generic;
using System.Linq;
using Common.AppleMusic;
using Serilog;
using SpotifyAPI.Web;

namespace Common
{
    public class GenericAlbum
    {
        public List<GenericTrack> Tracks { get; set; }

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
                Tracks = songs
            };

            return genericAlbum;
        }

        public static GenericAlbum FromAlbum(FullAlbum album)
        {
            var images = album.Images.OrderByDescending(i => i.Width)
                .ToArray();

            if (album.Tracks.Items == null)
                throw new Exception("Album has no tracks");

            var genericAlbum = new GenericAlbum
            {
                Artists = album.Artists.Select(a => a.Name)
                    .ToList(),
                FirstBillingArtist = album.Artists.First()
                    .Name,
                Barcode = album.ExternalIds["upc"],
                CatalogNumber = null,
                Description = null,
                Image = images[0]
                    .Url,
                Name = album.Name,
                ReleaseDate = album.ReleaseDate,
                Url = album.ExternalUrls["spotify"],
                Label = album.Label,
                Tracks = album.Tracks.Items.Select(GenericTrack.FromTrack)
                    .ToList()
            };
            
            Log.Information("ccc");

            return genericAlbum;
        }
    }
}