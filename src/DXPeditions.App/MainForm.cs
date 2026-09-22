using DXPeditions.Core.Adif;
using DXPeditions.Core.Announcements;
using DXPeditions.Core.Iota;
using DXPeditions.Core.Needed;
using DXPeditions.Core.Output;
using DXPeditions.Core.Scraping;

namespace DXPeditions.App;

public partial class MainForm : Form
{
    private readonly AppServices _services = new();
    private readonly Dictionary<string, CheckBox> _bandCheckboxes = new(StringComparer.OrdinalIgnoreCase);

    public MainForm()
    {
        InitializeComponent();

        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        picLogo.Image = LoadEmbeddedLogo();

        // All checked by default, so the regex is fully inclusive until the user
        // actually tells it a band their station can't work.
        foreach (var band in StandardBands.All)
        {
            var checkbox = new CheckBox
            {
                Text = band,
                Checked = true,
                AutoSize = true,
                Margin = new Padding(0, 6, 10, 0),
            };
            _bandCheckboxes[band] = checkbox;
            pnlBands.Controls.Add(checkbox);
        }

        cmbMonth.Items.AddRange([
            "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December"
        ]);

        var today = DateTime.Today;
        cmbMonth.SelectedIndex = today.Month - 1;
        numYear.Value = today.Year;
    }

    private HashSet<string> GetWorkableBands() =>
        _bandCheckboxes.Where(kv => kv.Value.Checked).Select(kv => kv.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private async void BtnFetch_Click(object? sender, EventArgs e)
    {
        btnFetch.Enabled = false;
        dgvResults.DataSource = null;
        txtGridTrackerRegex.Text = string.Empty;
        txtHrdChecklist.Text = string.Empty;

        var year = (int)numYear.Value;
        var month = cmbMonth.SelectedIndex + 1;

        try
        {
            SetStatus("Reading ADIF log...");
            var records = await Task.Run(() => AdifLogReader.ReadRecords(AppServices.AdifLogPath).ToList());

            // Cheap, single-fetch sources first - dx-world's per-article fetch is
            // the expensive one, so it's kicked off last, once these can tell it
            // which callsigns it can skip fetching for. Each source is fetched
            // defensively: one unreachable/slow site (a real timeout was seen from
            // 425dxn) must not take down the other four - a flaky source degrades
            // to "0 results, noted on the status line" rather than aborting the
            // whole run, same "skip rather than crash" philosophy as everywhere
            // else in this app.
            SetStatus("Fetching ng3k.com, va3rj, ham365, 425dxn, and IOTA reference data...");
            var failedSources = new List<string>();
            var ng3kTask = SafeFetchAsync("ng3k", _services.Ng3kScraper.GetAnnouncementsAsync(year, month), failedSources);
            var va3rjTask = SafeFetchAsync("va3rj", _services.Va3rjScraper.GetAnnouncementsAsync(year, month), failedSources);
            var ham365Task = SafeFetchAsync("ham365", _services.Ham365Scraper.GetAnnouncementsAsync(year, month), failedSources);
            var dxn425Task = SafeFetchAsync("425dxn", _services.Dxn425Scraper.GetAnnouncementsAsync(year, month), failedSources);
            var iotaTask = IotaReference.LoadAsync(_services.Fetcher, AppServices.IotaCacheFilePath);

            await Task.WhenAll(ng3kTask, va3rjTask, ham365Task, dxn425Task, iotaTask);

            var crossReference = CrossReferenceIndex.Build(va3rjTask.Result, ng3kTask.Result, dxn425Task.Result, ham365Task.Result);

            SetStatus("Fetching dx-world.net (only for callsigns the other sources didn't already cover)...");
            var dxWorldResult = await SafeFetchAsync(
                "dx-world.net", _services.DxWorldScraper.GetAnnouncementsAsync(year, month, crossReference), failedSources);

            SetStatus("Computing needed list...");
            var calculator = new NeededCalculator(records, _services.DxccReference, iotaTask.Result);
            var announcements = ng3kTask.Result
                .Concat(dxWorldResult)
                .Concat(va3rjTask.Result)
                .Concat(ham365Task.Result)
                .Concat(dxn425Task.Result)
                .ToList();
            var results = announcements.Select(calculator.Evaluate).ToList();

            PopulateGrid(results);
            txtGridTrackerRegex.Text = GridTrackerRegexBuilder.Build(results, GetWorkableBands());
            txtHrdChecklist.Text = HrdChecklistBuilder.Build(results);

            var neededCount = results.Count(r => r.IsNeeded);
            var iotaNote = iotaTask.Result.Count == 0 ? ", IOTA lookup unavailable this run" : "";
            var failureNote = failedSources.Count > 0 ? $", unavailable this run: {string.Join(", ", failedSources)}" : "";
            SetStatus($"Done - {neededCount} needed of {results.Count} announced ({announcements.Count} total: " +
                      $"{ng3kTask.Result.Count} ng3k, {dxWorldResult.Count} dx-world.net, {va3rjTask.Result.Count} va3rj, " +
                      $"{ham365Task.Result.Count} ham365, {dxn425Task.Result.Count} 425dxn{iotaNote}{failureNote})");
        }
        catch (Exception ex)
        {
            SetStatus("Error: " + ex.Message);
            MessageBox.Show(this, ex.ToString(), "Fetch failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnFetch.Enabled = true;
        }
    }

    private static readonly Color NewEntityHighlight = Color.FromArgb(255, 249, 196); // soft gold - "never worked before"

    private void PopulateGrid(List<NeededResult> results)
    {
        var rows = results
            .OrderByDescending(r => r.IsNeeded)
            .ThenBy(r => r.DxccEntityName ?? r.Announcement.RawEntityName, StringComparer.OrdinalIgnoreCase)
            .Select(r => new ResultRow
            {
                Needed = r.IsNeeded,
                Entity = r.DxccEntityName ?? r.Announcement.RawEntityName,
                Start = r.Announcement.StartDate?.ToString() ?? "?",
                End = r.Announcement.EndDate?.ToString() ?? "?",
                IsNewEntity = !r.HasAnyQso,
            })
            .ToList();

        dgvResults.DataSource = rows;
        dgvResults.Columns[nameof(ResultRow.IsNewEntity)].Visible = false;

        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].IsNewEntity)
            {
                dgvResults.Rows[i].DefaultCellStyle.BackColor = NewEntityHighlight;
            }
        }
    }

    private void BtnCopyGridTracker_Click(object? sender, EventArgs e)
    {
        if (txtGridTrackerRegex.Text.Length > 0)
        {
            Clipboard.SetText(txtGridTrackerRegex.Text);
        }
    }

    private void BtnCopyHrd_Click(object? sender, EventArgs e)
    {
        if (txtHrdChecklist.Text.Length > 0)
        {
            Clipboard.SetText(txtHrdChecklist.Text);
        }
    }

    /// <summary>
    /// Awaits a source's fetch, but never lets it fault the caller - a network
    /// failure (timeout, DNS, connection refused, ...) from one source degrades
    /// to an empty result and a note in <paramref name="failedSources"/> instead
    /// of aborting every other source's already-in-flight fetch.
    /// </summary>
    private static async Task<IReadOnlyList<DxpeditionAnnouncement>> SafeFetchAsync(
        string sourceName, Task<IReadOnlyList<DxpeditionAnnouncement>> fetchTask, List<string> failedSources)
    {
        try
        {
            return await fetchTask;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Net.Sockets.SocketException)
        {
            failedSources.Add(sourceName);
            return [];
        }
    }

    private void SetStatus(string text) => lblStatus.Text = text;

    private static Image? LoadEmbeddedLogo()
    {
        using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("DXPeditions.App.Resources.Logo.png");
        if (stream is null)
        {
            return null;
        }

        using var embedded = Image.FromStream(stream);
        return new Bitmap(embedded);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _services.Dispose();
        base.OnFormClosed(e);
    }

    private sealed class ResultRow
    {
        public bool Needed { get; set; }
        public string Entity { get; set; } = "";
        public string Start { get; set; } = "";
        public string End { get; set; } = "";
        public bool IsNewEntity { get; set; }
    }
}
