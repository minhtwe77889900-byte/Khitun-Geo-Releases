using System.Drawing.Drawing2D;

namespace KhitunGeo.Native;

internal sealed class ThemeSwitcher : Button
{
    public bool Dark { get; private set; }
    public event EventHandler? ThemeChanged;

    public ThemeSwitcher()
    {
        Size = new Size(48, 48);
        NativeTheme.StyleNavigation(this);
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        CommitVisualState();
    }

    public void SetDark(bool value)
    {
        if (Dark == value) return;
        Dark = value;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
        CommitVisualState();
    }

    public void CommitVisualState()
    {
        AccessibleName = Dark ? "Включить светлую тему" : "Включить тёмную тему";
        AccessibleDescription = Dark ? "Текущая тема — тёмная" : "Текущая тема — светлая";
        Invalidate();
    }

    protected override void OnClick(EventArgs e)
    {
        SetDark(!Dark);
        base.OnClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        BrandArtwork.PaintBackdrop(this, e.Graphics);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float scale = Math.Min(Width, Height) / 48f;
        if (scale <= 0) return;
        e.Graphics.TranslateTransform(Width / 2f, Height / 2f);
        e.Graphics.ScaleTransform(scale, scale);
        using var pen = new Pen(ForeColor, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        if (Dark)
        {
            using var crescent = new GraphicsPath();
            crescent.AddBezier(3, -10, -13, -11, -13, 12, 3, 10);
            crescent.AddBezier(3, 10, 7, 9, 9, 7, 10, 4);
            crescent.AddBezier(10, 4, 0, 7, -4, -4, 3, -10);
            crescent.CloseFigure();
            e.Graphics.DrawPath(pen, crescent);
        }
        else
        {
            e.Graphics.DrawEllipse(pen, -5, -5, 10, 10);
            for (var ray = 0; ray < 8; ray++)
            {
                e.Graphics.DrawLine(pen, 8, 0, 11, 0);
                e.Graphics.RotateTransform(45);
            }
        }
        e.Graphics.ResetTransform();
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4));
    }
}
