namespace DXPeditions.App;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private Panel pnlToolbar;
    private Label lblMonth;
    private ComboBox cmbMonth;
    private Label lblYear;
    private NumericUpDown numYear;
    private Button btnFetch;
    private Label lblStatus;
    private FlowLayoutPanel pnlBands;
    private Label lblBands;
    private SplitContainer splitMain;
    private DataGridView dgvResults;
    private SplitContainer splitOutputs;
    private GroupBox grpGridTracker;
    private TextBox txtGridTrackerRegex;
    private Button btnCopyGridTracker;
    private GroupBox grpHrd;
    private TextBox txtHrdChecklist;
    private Button btnCopyHrd;

    private void InitializeComponent()
    {
        pnlToolbar = new Panel { Name = "pnlToolbar" };
        lblMonth = new Label { Name = "lblMonth" };
        cmbMonth = new ComboBox { Name = "cmbMonth" };
        lblYear = new Label { Name = "lblYear" };
        numYear = new NumericUpDown { Name = "numYear" };
        btnFetch = new Button { Name = "btnFetch" };
        lblStatus = new Label { Name = "lblStatus" };
        pnlBands = new FlowLayoutPanel { Name = "pnlBands" };
        lblBands = new Label { Name = "lblBands" };
        splitMain = new SplitContainer { Name = "splitMain" };
        dgvResults = new DataGridView { Name = "dgvResults" };
        splitOutputs = new SplitContainer { Name = "splitOutputs" };
        grpGridTracker = new GroupBox { Name = "grpGridTracker" };
        txtGridTrackerRegex = new TextBox { Name = "txtGridTrackerRegex" };
        btnCopyGridTracker = new Button { Name = "btnCopyGridTracker" };
        grpHrd = new GroupBox { Name = "grpHrd" };
        txtHrdChecklist = new TextBox { Name = "txtHrdChecklist" };
        btnCopyHrd = new Button { Name = "btnCopyHrd" };

        ((System.ComponentModel.ISupportInitialize)numYear).BeginInit();
        ((System.ComponentModel.ISupportInitialize)splitMain).BeginInit();
        splitMain.Panel1.SuspendLayout();
        splitMain.Panel2.SuspendLayout();
        splitMain.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)dgvResults).BeginInit();
        ((System.ComponentModel.ISupportInitialize)splitOutputs).BeginInit();
        splitOutputs.Panel1.SuspendLayout();
        splitOutputs.Panel2.SuspendLayout();
        splitOutputs.SuspendLayout();
        grpGridTracker.SuspendLayout();
        grpHrd.SuspendLayout();
        SuspendLayout();

        // pnlToolbar
        pnlToolbar.Dock = DockStyle.Top;
        pnlToolbar.Height = 44;
        pnlToolbar.Padding = new Padding(8);
        pnlToolbar.Controls.Add(lblStatus);
        pnlToolbar.Controls.Add(btnFetch);
        pnlToolbar.Controls.Add(numYear);
        pnlToolbar.Controls.Add(lblYear);
        pnlToolbar.Controls.Add(cmbMonth);
        pnlToolbar.Controls.Add(lblMonth);

        // lblMonth
        lblMonth.AutoSize = true;
        lblMonth.Location = new Point(8, 14);
        lblMonth.Text = "Month:";

        // cmbMonth
        cmbMonth.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbMonth.Location = new Point(58, 10);
        cmbMonth.Width = 110;

        // lblYear
        lblYear.AutoSize = true;
        lblYear.Location = new Point(180, 14);
        lblYear.Text = "Year:";

        // numYear
        numYear.Location = new Point(222, 10);
        numYear.Width = 70;
        numYear.Minimum = 2000;
        numYear.Maximum = 2100;

        // btnFetch
        btnFetch.Location = new Point(304, 8);
        btnFetch.Width = 90;
        btnFetch.Text = "Fetch";
        btnFetch.Click += BtnFetch_Click;

        // lblStatus
        lblStatus.AutoSize = true;
        lblStatus.Location = new Point(404, 14);
        lblStatus.Text = string.Empty;

        // pnlBands
        pnlBands.Dock = DockStyle.Top;
        pnlBands.Height = 30;
        pnlBands.Padding = new Padding(8, 4, 8, 4);
        pnlBands.FlowDirection = FlowDirection.LeftToRight;
        pnlBands.WrapContents = false;
        pnlBands.Controls.Add(lblBands);

        // lblBands
        lblBands.AutoSize = true;
        lblBands.Margin = new Padding(0, 6, 12, 0);
        lblBands.Text = "Workable bands (limits GridTracker regex):";

        // splitMain
        splitMain.Dock = DockStyle.Fill;
        splitMain.Orientation = Orientation.Horizontal;
        splitMain.Panel1.Controls.Add(dgvResults);
        splitMain.Panel2.Controls.Add(splitOutputs);
        splitMain.SplitterDistance = 320;

        // dgvResults
        dgvResults.Dock = DockStyle.Fill;
        dgvResults.AllowUserToAddRows = false;
        dgvResults.AllowUserToDeleteRows = false;
        dgvResults.ReadOnly = true;
        dgvResults.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgvResults.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

        // splitOutputs
        splitOutputs.Dock = DockStyle.Fill;
        splitOutputs.Orientation = Orientation.Vertical;
        splitOutputs.Panel1.Controls.Add(grpGridTracker);
        splitOutputs.Panel2.Controls.Add(grpHrd);

        // grpGridTracker
        grpGridTracker.Dock = DockStyle.Fill;
        grpGridTracker.Text = "GridTracker Regex Limiter";
        grpGridTracker.Controls.Add(txtGridTrackerRegex);
        grpGridTracker.Controls.Add(btnCopyGridTracker);

        // txtGridTrackerRegex
        txtGridTrackerRegex.Multiline = true;
        txtGridTrackerRegex.ReadOnly = true;
        txtGridTrackerRegex.ScrollBars = ScrollBars.Vertical;
        txtGridTrackerRegex.Dock = DockStyle.Fill;
        txtGridTrackerRegex.Font = new Font(FontFamily.GenericMonospace, 9F);

        // btnCopyGridTracker
        btnCopyGridTracker.Dock = DockStyle.Bottom;
        btnCopyGridTracker.Text = "Copy to Clipboard";
        btnCopyGridTracker.Click += BtnCopyGridTracker_Click;

        // grpHrd
        grpHrd.Dock = DockStyle.Fill;
        grpHrd.Text = "HRD Alarm Checklist";
        grpHrd.Controls.Add(txtHrdChecklist);
        grpHrd.Controls.Add(btnCopyHrd);

        // txtHrdChecklist
        txtHrdChecklist.Multiline = true;
        txtHrdChecklist.ReadOnly = true;
        txtHrdChecklist.ScrollBars = ScrollBars.Vertical;
        txtHrdChecklist.Dock = DockStyle.Fill;
        txtHrdChecklist.Font = new Font(FontFamily.GenericMonospace, 9F);

        // btnCopyHrd
        btnCopyHrd.Dock = DockStyle.Bottom;
        btnCopyHrd.Text = "Copy to Clipboard";
        btnCopyHrd.Click += BtnCopyHrd_Click;

        // MainForm
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1100, 700);
        Controls.Add(splitMain);
        Controls.Add(pnlBands);
        Controls.Add(pnlToolbar);
        Text = "DXPeditions Tracker";
        MinimumSize = new Size(800, 500);

        ((System.ComponentModel.ISupportInitialize)numYear).EndInit();
        splitMain.Panel1.ResumeLayout(false);
        splitMain.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)splitMain).EndInit();
        splitMain.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)dgvResults).EndInit();
        splitOutputs.Panel1.ResumeLayout(false);
        splitOutputs.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)splitOutputs).EndInit();
        splitOutputs.ResumeLayout(false);
        grpGridTracker.ResumeLayout(false);
        grpGridTracker.PerformLayout();
        grpHrd.ResumeLayout(false);
        grpHrd.PerformLayout();
        ResumeLayout(false);
    }
}
