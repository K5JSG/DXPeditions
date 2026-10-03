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
    private readonly AppSettings _settings = AppSettings.Load(AppServices.SettingsFilePath);
    private readonly ToolTip _statusToolTip = new();
    private readonly Dictionary<string, CheckBox> _bandCheckboxes = new(StringComparer.OrdinalIgnoreCase);

    // The last Fetch's results, kept so changing a band checkbox afterwards can
    // re-render every output without re-fetching. Null until a Fetch completes.
    private List<NeededResult>? _lastResults;
    private string _lastSourceSummary = "";

    public MainForm()
    {
        InitializeComponent();

        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        picLogo.Image = LoadEmbeddedLogo();

        // Restored from last run; all checked on first run, so every output is
        // fully inclusive until the user tells it a band their station can't work.
        foreach (var band in StandardBands.All)
        {
            var checkbox = new CheckBox
            {
                Text = band,
                Checked = _settings.WorkableBands?.Contains(band, StringComparer.OrdinalIgnoreCase) ?? true,
                AutoSize = true,
                Margin = new Padding(0, 6, 10, 0),
            };
            checkbox.CheckedChanged += (_, _) => RenderResults();
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
        _lastResults = null;
        dgvResults.DataSource = null;
        txtGridTrackerRegex.Text = string.Empty;
        txtHrdRegex.Text = string.Empty;

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

            var iotaNote = iotaTask.Result.Count == 0 ? ", IOTA lookup unavailable this run" : "";
            var failureNote = failedSources.Count > 0 ? $", unavailable this run: {string.Join(", ", failedSources)}" : "";
            _lastSourceSummary = $"{announcements.Count} total: " +
                                 $"{ng3kTask.Result.Count} ng3k, {dxWorldResult.Count} dx-world.net, {va3rjTask.Result.Count} va3rj, " +
                                 $"{ham365Task.Result.Count} ham365, {dxn425Task.Result.Count} 425dxn{iotaNote}{failureNote}";
            _lastResults = results;
            RenderResults();
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

    /// <summary>
    /// Fills the grid, both regex outputs and the needed count from the last
    /// Fetch's results using the currently checked bands. Runs after a Fetch and
    /// again whenever a band checkbox changes.
    /// </summary>
    private void RenderResults()
    {
        if (_lastResults is null)
        {
            return;
        }

        var workableBands = GetWorkableBands();
        PopulateGrid(_lastResults, workableBands);
        txtGridTrackerRegex.Text = GridTrackerRegexBuilder.Build(_lastResults, workableBands);
        txtHrdRegex.Text = HrdRegexChunker.Build(txtGridTrackerRegex.Text);

        var neededCount = _lastResults.Count(r => r.IsNeededOn(workableBands));
        SetStatus($"Done - {neededCount} needed of {_lastResults.Count} announced ({_lastSourceSummary})");
    }

    private static readonly Color NewEntityHighlight = Color.FromArgb(255, 249, 196); // soft gold - "never worked before"

    private void PopulateGrid(List<NeededResult> results, IReadOnlySet<string> workableBands)
    {
        var rows = results
            .OrderByDescending(r => r.IsNeededOn(workableBands))
            .ThenBy(r => r.DxccEntityName ?? r.Announcement.RawEntityName, StringComparer.OrdinalIgnoreCase)
            .Select(r => new ResultRow
            {
                Needed = r.IsNeededOn(workableBands),
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
        if (txtHrdRegex.Text.Length > 0)
        {
            Clipboard.SetText(txtHrdRegex.Text);
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

    // The label is cut short with "..." before the logo, so the full text is
    // also available by hovering over it.
    private void SetStatus(string text)
    {
        lblStatus.Text = text;
        _statusToolTip.SetToolTip(lblStatus, text);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        // After base.OnLayout, so the anchored logo is already in its new spot.
        base.OnLayout(levent);

        // picLogo is anchored to the right edge; keep the status line ending just
        // before it at any window size.
        if (lblStatus is not null && picLogo is not null)
        {
            lblStatus.Width = Math.Max(0, picLogo.Left - lblStatus.Left - 8);
        }
    }

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

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Applied here rather than in the constructor: the containers only have
        // their real docked size once the form is laid out.
        ApplySplitRatio(splitMain, _settings.MainSplitRatio);
        ApplySplitRatio(splitOutputs, _settings.OutputsSplitRatio);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _settings.WorkableBands = GetWorkableBands().ToList();
        _settings.MainSplitRatio = GetSplitRatio(splitMain) ?? _settings.MainSplitRatio;
        _settings.OutputsSplitRatio = GetSplitRatio(splitOutputs) ?? _settings.OutputsSplitRatio;
        _settings.Save(AppServices.SettingsFilePath);
        base.OnFormClosing(e);
    }

    private static int SplitLength(SplitContainer split) =>
        split.Orientation == Orientation.Vertical ? split.Width : split.Height;

    private static double? GetSplitRatio(SplitContainer split)
    {
        var length = SplitLength(split);
        return length > 0 ? (double)split.SplitterDistance / length : null;
    }

    private static void ApplySplitRatio(SplitContainer split, double? ratio)
    {
        if (ratio is not (> 0 and < 1))
        {
            return;
        }

        var distance = (int)(ratio.Value * SplitLength(split));
        var max = SplitLength(split) - split.Panel2MinSize - split.SplitterWidth;
        if (distance >= split.Panel1MinSize && distance <= max)
        {
            split.SplitterDistance = distance;
        }
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
