using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Telhai.DotNet.PlayerProject.Models;

namespace Telhai.DotNet.PlayerProject.Services
{
    public class ItunesSearchService
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        public async Task<ItunesTrackResult?> SearchBestMatchAsync(string term, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(term))
                return null;

            // music + entity=song keeps results relevant.
            // Apple docs: media/entity parameters exist for searches. citeturn0search2turn0search1
            var url =
                "https://itunes.apple.com/search?" +
                $"term={Uri.EscapeDataString(term)}&media=music&entity=song&limit=1";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

            resp.EnsureSuccessStatusCode();

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            var payload = await JsonSerializer.DeserializeAsync<ItunesSearchResponse>(stream, cancellationToken: ct);

            return payload?.Results?.FirstOrDefault();
        }

        public async Task<byte[]?> DownloadBytesAsync(string url, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            using var resp = await _http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsByteArrayAsync(ct);
        }
    }
}
