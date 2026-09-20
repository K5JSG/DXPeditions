namespace DXPeditions.Core.Scraping;

/// <summary>
/// Thin HttpClient wrapper: a descriptive User-Agent, a sane timeout, and a small
/// politeness delay helper for scrapers that make many sequential requests
/// (dx-world.net's per-article fetches in particular).
/// </summary>
public sealed class HttpFetcher : IDisposable
{
    private readonly HttpClient _client;

    public HttpFetcher()
    {
        _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "DXPeditions-Tracker/1.0 (personal use; +https://github.com/)");
    }

    public async Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default)
    {
        using var response = await _client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Like <see cref="GetStringAsync"/> but returns null on a 404 instead of
    /// throwing, since scrapers use this to probe for pages that may not exist
    /// (e.g. an archive page past the last real page of results).
    /// </summary>
    public async Task<string?> TryGetStringAsync(string url, CancellationToken cancellationToken = default)
    {
        using var response = await _client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>POSTs form-encoded data, for the handful of sources whose real data lives behind an AJAX endpoint (e.g. ham365.net's calendar grid) rather than a plain GET-able page.</summary>
    public async Task<string> PostFormAsync(string url, IEnumerable<KeyValuePair<string, string>> formFields, CancellationToken cancellationToken = default)
    {
        using var content = new FormUrlEncodedContent(formFields);
        using var response = await _client.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public static Task PoliteDelay(CancellationToken cancellationToken = default) =>
        Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken);

    public void Dispose() => _client.Dispose();
}
