using DXPeditions.Core.Dxcc;
using DXPeditions.Core.Scraping;

namespace DXPeditions.App;

/// <summary>Composition root: builds the shared services MainForm needs.</summary>
public sealed class AppServices : IDisposable
{
    public const string AdifLogPath = @"C:\Users\jsgay\AppData\Local\WSJT-X\wsjtx_log.adi";
    public const string IotaCacheFilePath = @"C:\Users\jsgay\AppData\Local\DXPeditions\cache\iota_fulllist.json";
    public const string DxWorldArticleCacheFilePath = @"C:\Users\jsgay\AppData\Local\DXPeditions\cache\dxworld_articles.json";

    public HttpFetcher Fetcher { get; }
    public DxccReference DxccReference { get; }
    public IDxpeditionScraper Ng3kScraper { get; }
    public Core.Scraping.DxWorldScraper DxWorldScraper { get; }
    public IDxpeditionScraper Va3rjScraper { get; }
    public IDxpeditionScraper Ham365Scraper { get; }
    public IDxpeditionScraper Dxn425Scraper { get; }

    public AppServices()
    {
        Fetcher = new HttpFetcher();
        DxccReference = DxccReference.LoadEmbedded();
        Ng3kScraper = new Core.Scraping.Ng3kScraper(Fetcher);
        DxWorldScraper = new Core.Scraping.DxWorldScraper(Fetcher, DxWorldArticleCache.Load(DxWorldArticleCacheFilePath));
        Va3rjScraper = new Core.Scraping.Va3rjScraper(Fetcher);
        Ham365Scraper = new Core.Scraping.Ham365Scraper(Fetcher);
        Dxn425Scraper = new Core.Scraping.Dxn425Scraper(Fetcher);
    }

    public void Dispose() => Fetcher.Dispose();
}
