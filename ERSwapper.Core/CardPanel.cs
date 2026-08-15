using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace ERSwapper.Core;

public class CardPanel : Panel
{
    private string _title = "";

    public CardPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

        BackColor = Theme.Surface;
        ForeColor = Theme.Text;
        Padding = new Padding(10, 32, 10, 10);
    }

    [Category("Appearance")]
    [DefaultValue("")]
    public string Title
    {
        get => _title;
        set
        {
            _title = value ?? "";
            Invalidate();
        }
    }

    [Category("Appearance")]
    [DefaultValue(6)]
    public int CornerRadius { get; set; } = 6;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);

        using (var background = new SolidBrush(BackColor))
        using (GraphicsPath path = RoundedRect(bounds, CornerRadius))
        {
            g.FillPath(background, path);

            using var border = new Pen(Theme.Border);
            g.DrawPath(border, path);
        }

        if (_title.Length == 0) return;

        using var titleBrush = new SolidBrush(Theme.TextMuted);
        g.DrawString(_title, Font, titleBrush, new PointF(10, 9));
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();

        if (radius <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        int diameter = radius * 2;
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();

        return path;
    }
}
