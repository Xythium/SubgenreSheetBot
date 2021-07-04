using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using RestSharp;

namespace Common
{
    public static class ColorUtils
    {
        public static uint MostCommonColor(string url)
        {
            var client = new RestClient();
            var request = new RestRequest(url, Method.GET);
            var data = client.DownloadData(request);

            using (var ms = new MemoryStream(data))
            using (var bitmap = new Bitmap(ms))
            {
                return GetPixels(bitmap)
                    .GroupBy(c => c)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .First();
            }
        }

        public static IEnumerable<uint> GetPixels(Bitmap bitmap)
        {
            for (var x = 0; x < bitmap.Width; x++)
                for (var y = 0; y < bitmap.Height; y++)
                    yield return bitmap.GetPixel(x, y)
                        .ToUint();
        }

        private static uint ToUint(this Color c) { return (uint) (((c.A << 24) | (c.R << 16) | (c.G << 8) | c.B) & 0xffffffffL); }
    }
}