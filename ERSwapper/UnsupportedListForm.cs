namespace ERSwapper;

public partial class UnsupportedListForm : Form
{
    public UnsupportedListForm()
    {
        InitializeComponent();
        Theme.Apply(this);

        lblIntro.ForeColor = Theme.TextMuted;
        lblCount.ForeColor = Theme.TextMuted;

        Populate();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        FitReasonColumn();
    }

    private void lstUnsupported_Resize(object? sender, EventArgs e) => FitReasonColumn();

    private void FitReasonColumn()
    {
        int available = lstUnsupported.ClientSize.Width - colName.Width - colSize.Width - 4;
        if (available < 200) available = 200;

        if (colReason.Width != available) colReason.Width = available;
    }

    private void Populate()
    {
        List<UnsupportedEntry> entries = UnsupportedRegistry.Load(ShippedPath)
            .Concat(UnsupportedRegistry.Load())
            .GroupBy(e => e.DumpFile, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(e => e.TextureName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        lstUnsupported.BeginUpdate();
        lstUnsupported.Items.Clear();

        foreach (UnsupportedEntry entry in entries)
        {
            string name = string.IsNullOrWhiteSpace(entry.TextureName)
                ? Path.GetFileNameWithoutExtension(entry.DumpFile)
                : entry.TextureName;

            var item = new ListViewItem(ParsedTexture.FriendlyName(name));
            item.SubItems.Add(entry.Width > 0 ? $"{entry.Width} x {entry.Height}" : "");
            item.SubItems.Add(Explain(entry));

            lstUnsupported.Items.Add(item);
        }

        lstUnsupported.EndUpdate();

        lblCount.Text = entries.Count == 0
            ? "Nothing has been recorded as unsupported."
            : $"{entries.Count} texture(s) cannot be swapped.";

        if (entries.Count != 0) return;

        lblIntro.Text =
            "Nothing is on this list yet.\r\n\r\n" +
            "If an item you want is missing from the gallery it has probably just not been added " +
            "yet, rather than being impossible.";
    }

    private static string Explain(UnsupportedEntry entry)
    {
        if (entry.Reason.Contains("m_TextureFormat", StringComparison.OrdinalIgnoreCase))
            return $"Compressed format {entry.TextureFormat} — this tool cannot rewrite it";

        if (entry.Reason.Contains("stream", StringComparison.OrdinalIgnoreCase))
            return "Stored inside the asset file rather than as streamed texture data";

        return string.IsNullOrWhiteSpace(entry.Reason) ? "Not supported" : entry.Reason;
    }

    private static string ShippedPath =>
        Path.Combine(AppPaths.SeedConfigDirectory, "unsupported.json");
}
