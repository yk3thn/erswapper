using System.Runtime.InteropServices;

namespace ERSwapper.Core;

public static class Theme
{
    public static readonly Color WindowBackground = Color.FromArgb(0x16, 0x16, 0x1A);
    public static readonly Color Surface = Color.FromArgb(0x1F, 0x1F, 0x25);
    public static readonly Color SurfaceAlt = Color.FromArgb(0x26, 0x26, 0x2E);
    public static readonly Color SurfaceHover = Color.FromArgb(0x2E, 0x2E, 0x38);
    public static readonly Color Border = Color.FromArgb(0x34, 0x34, 0x3E);

    public static readonly Color Text = Color.FromArgb(0xE8, 0xE8, 0xED);
    public static readonly Color TextMuted = Color.FromArgb(0x9C, 0x9C, 0xAA);

    public static readonly Color Accent = Color.FromArgb(0x5B, 0x8C, 0xFF);
    public static readonly Color Success = Color.FromArgb(0x57, 0xC7, 0x7F);
    public static readonly Color Danger = Color.FromArgb(0xFF, 0x7A, 0x7A);

    public static readonly Color WarningBackground = Color.FromArgb(0x3A, 0x2A, 0x14);
    public static readonly Color WarningText = Color.FromArgb(0xF2, 0xC0, 0x78);

    public static void Apply(Form form)
    {
        form.BackColor = WindowBackground;
        form.ForeColor = Text;

        ApplyToChildren(form);

        if (form.IsHandleCreated) ApplyDarkTitleBar(form);
        else form.HandleCreated += (_, _) => ApplyDarkTitleBar(form);
    }

    private static void ApplyToChildren(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            ApplyToControl(control);
            ApplyToChildren(control);
        }
    }

    private static void ApplyToControl(Control control)
    {
        switch (control)
        {
            case CardPanel card:
                card.BackColor = Surface;
                card.ForeColor = Text;
                break;

            case Button button:
                button.FlatStyle = FlatStyle.Flat;
                button.BackColor = SurfaceAlt;
                button.ForeColor = Text;
                button.FlatAppearance.BorderColor = Border;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.MouseOverBackColor = SurfaceHover;
                button.FlatAppearance.MouseDownBackColor = Border;
                button.UseVisualStyleBackColor = false;
                break;

            case TextBox textBox:
                textBox.BackColor = SurfaceAlt;
                textBox.ForeColor = Text;
                textBox.BorderStyle = BorderStyle.FixedSingle;
                break;

            case ComboBox comboBox:
                comboBox.FlatStyle = FlatStyle.Flat;
                comboBox.BackColor = SurfaceAlt;
                comboBox.ForeColor = Text;

                if (comboBox.DrawMode == DrawMode.Normal)
                {
                    comboBox.DrawMode = DrawMode.OwnerDrawFixed;
                    comboBox.DrawItem += DrawComboBoxItem;
                }

                break;

            case NumericUpDown numeric:
                numeric.BackColor = SurfaceAlt;
                numeric.ForeColor = Text;
                numeric.BorderStyle = BorderStyle.FixedSingle;
                break;

            case ListView listView:
                listView.BackColor = Surface;
                listView.ForeColor = Text;
                listView.BorderStyle = BorderStyle.None;
                ApplyDarkExplorerTheme(listView);
                break;

            case ListBox listBox:
                listBox.BackColor = SurfaceAlt;
                listBox.ForeColor = Text;
                listBox.BorderStyle = BorderStyle.FixedSingle;
                break;

            case LinkLabel link:
                link.BackColor = Color.Transparent;
                link.LinkColor = Accent;
                link.ActiveLinkColor = Accent;
                link.VisitedLinkColor = Accent;
                link.ForeColor = Text;
                break;

            case Label label:
                label.BackColor = Color.Transparent;
                label.ForeColor = Text;
                break;

            case CheckBox checkBox:
                checkBox.BackColor = Color.Transparent;
                checkBox.ForeColor = Text;
                break;

            case GroupBox groupBox:
                groupBox.BackColor = WindowBackground;
                groupBox.ForeColor = TextMuted;
                break;

            case PictureBox pictureBox:
                pictureBox.BackColor = Surface;
                break;

            case ProgressBar progressBar:

                progressBar.BackColor = SurfaceAlt;
                progressBar.ForeColor = Accent;
                RunWhenHandleReady(progressBar, () => SetWindowTheme(progressBar.Handle, "", ""));
                break;

            case Panel panel:
                panel.BackColor = WindowBackground;
                panel.ForeColor = Text;
                break;

            case SplitContainer split:
                split.BackColor = WindowBackground;
                split.Panel1.BackColor = WindowBackground;
                split.Panel2.BackColor = WindowBackground;
                break;
        }
    }

    private static void DrawComboBoxItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox combo) return;

        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

        using (var background = new SolidBrush(selected ? SurfaceHover : SurfaceAlt))
        {
            e.Graphics.FillRectangle(background, e.Bounds);
        }

        if (e.Index < 0 || e.Index >= combo.Items.Count) return;

        TextRenderer.DrawText(
            e.Graphics,
            combo.GetItemText(combo.Items[e.Index]),
            combo.Font,
            Rectangle.Inflate(e.Bounds, -3, 0),
            Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeLegacy = 19;

    private static void ApplyDarkTitleBar(Form form)
    {
        try
        {
            int enabled = 1;

            if (DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
                DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkModeLegacy, ref enabled, sizeof(int));
        }
        catch
        {
        }
    }

    private static void ApplyDarkExplorerTheme(ListView listView)
    => RunWhenHandleReady(listView, () => SetWindowTheme(listView.Handle, "DarkMode_Explorer", null));

    private static void RunWhenHandleReady(Control control, Action action)
    {
        void Guarded()
        {
            try { action(); }
            catch { }
        }

        if (control.IsHandleCreated) Guarded();
        else control.HandleCreated += (_, _) => Guarded();
    }

    [DllImport("dwmapi.dll", SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);
}
