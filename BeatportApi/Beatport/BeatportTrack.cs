using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace BeatportApi.Beatport
{
    public class BeatportTrack
    {
        [JsonProperty("artists"), JsonRequired]
        public List<BeatportArtistSummary> Artists { get; set; }

        [JsonProperty("audio_format")] // todo
        public object AudioFormat { get; set; }

        public string ArtistConcat => string.Join(" x ", Artists.Select(a => a.Name));

        [JsonProperty("available_worldwide"), JsonRequired]
        public bool IsAvailableWorldwide { get; set; }

        [JsonProperty("bpm")] // optional
        public int? Bpm { get; set; }

        [JsonProperty("catalog_number"), JsonRequired]
        public string CatalogNumber { get; set; }

        [JsonProperty("copyright")] // optional
        public string Copyright { get; set; }

        [JsonProperty("current_status"), JsonRequired]
        public BeatportStatusSummary CurrentStatus { get; set; }

        [JsonProperty("desc")] // optional
        public string Description { get; set; }

        [JsonProperty("enabled")] // todo
        public bool IsEnabled { get; set; }

        [JsonProperty("encode_status")]
        public string? EncodeStatus { get; set; }

        [JsonProperty("encoded_date"), JsonRequired]
        public DateTime EncodedDate { get; set; }

        [JsonProperty("exclusive"), JsonRequired]
        public bool IsExclusive { get; set; }

        [JsonProperty("exclusive_period")] // optional
        public BeatportExclusiveSummary ExclusivePeriod { get; set; }

        [JsonProperty("free_download_end_date")] // optional
        public DateTime? FreeDownloadEndDate { get; set; }

        [JsonProperty("free_download_start_date")] // optional
        public DateTime? FreeDownloadStartDate { get; set; }

        [JsonProperty("free_downloads"), JsonRequired]
        public BeatportFreeDownload[] FreeDownloads { get; set; }

        [JsonProperty("genre"), JsonRequired]
        public BeatportGenreSummary Genre { get; set; }

        [JsonProperty("hidden")]
        public bool? IsHidden { get; set; }

        [JsonProperty("id"), JsonRequired]
        public int Id { get; set; }

        [JsonProperty("image")]
        public BeatportImage Image { get; set; }

        [JsonProperty("is_available_for_streaming"), JsonRequired]
        public bool IsStreaming { get; set; }

        [JsonProperty("is_classic")]
        public bool? IsClassic { get; set; }

        [JsonProperty("is_hype"), JsonRequired]
        public bool IsHype { get; set; }

        [JsonProperty("isrc")] // optional
        public string Isrc { get; set; }

        [JsonProperty("key")]
        public BeatportKeySummary? Key { get; set; }

        [JsonProperty("label_track_identifier")]
        public string LabelTrackIdentifier { get; set; }

        [JsonProperty("length")]
        public TimeSpan Length => TimeSpan.FromMilliseconds(LengthMs);

        [JsonProperty("length_ms"), JsonRequired]
        public int LengthMs { get; set; }

        [JsonProperty("mix_name"), JsonRequired]
        public string MixName { get; set; }

        [JsonProperty("name"), JsonRequired]
        public string Name { get; set; }

        [JsonProperty("new_release_date"), JsonRequired]
        public DateTime NewReleaseDate { get; set; }

        [JsonProperty("number")]
        public int? Number { get; set; }

        [JsonProperty("pre_order"), JsonRequired]
        public bool IsPreorder { get; set; }

        [JsonProperty("pre_order_date")] // optional
        public DateTime? PreorderDate { get; set; }

        [JsonProperty("price"), JsonRequired]
        public BeatportPrice Price { get; set; }

        [JsonProperty("publish_date"), JsonRequired]
        public DateTime PublishDate { get; set; }

        [JsonProperty("publish_status"), JsonRequired]
        public string PublishStatus { get; set; }

        [JsonProperty("release"), JsonRequired]
        public BeatportReleaseSummary Release { get; set; }

        [JsonProperty("remixers"), JsonRequired]
        public List<BeatportArtistSummary> Remixers { get; set; }

        [JsonProperty("sale_type"), JsonRequired]
        public BeatportSaleTypeSummary SaleType { get; set; }

        [JsonProperty("sample_end")]
        public TimeSpan? SampleEnd => SampleEndMs != null ? TimeSpan.FromMilliseconds(SampleEndMs.Value) : null;

        [JsonProperty("sample_end_ms")]
        public int? SampleEndMs { get; set; }

        [JsonProperty("sample_start")]
        public TimeSpan? SampleStart => SampleStartMs != null ? TimeSpan.FromMilliseconds(SampleStartMs.Value) : null;

        [JsonProperty("sample_start_ms")]
        public int? SampleStartMs { get; set; }

        [JsonProperty("sample_url"), JsonRequired]
        public string SampleUrl { get; set; }

        [JsonProperty("slug"), JsonRequired]
        public string Slug { get; set; }

        [JsonProperty("sub_genre")] // optional
        public BeatportSubgenreSummary? Subgenre { get; set; }

        [JsonProperty("territories")] // idk why
        public int[]? Territories { get; set; }

        [JsonProperty("url")] // optional
        public string Url { get; set; }

        [JsonProperty("was_ever_exclusive")]
        public bool? WasEverExclusive { get; set; }
    }
}