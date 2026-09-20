using DXPeditions.Core.Announcements;

namespace DXPeditions.Core.Scraping;

public interface IDxpeditionScraper
{
    Task<IReadOnlyList<DxpeditionAnnouncement>> GetAnnouncementsAsync(
        int year, int month, CancellationToken cancellationToken = default);
}
