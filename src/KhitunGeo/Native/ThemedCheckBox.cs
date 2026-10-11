using System.Drawing.Drawing2D;

namespace KhitunGeo.Native;

internal sealed class ThemedCheckBox : CheckBox
{
    public ThemedCheckBox() => SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = DeviceDpi / 96f; int size = (int)(18 * scale);
        bool right = CheckAlign is ContentAlignment.MiddleRight or ContentAlignment.TopRight or ContentAlignment.BottomRight;
        var box = new Rectangle(right ? Width - size - 2 : 1, (Height - size) / 2, size, size);
        var dark = NativeTheme.IsDark(this);
        var accent = Color.FromArgb(0, 151, 164);
        using var fill = new SolidBrush(Checked ? (Enabled ? accent : dark ? Color.FromArgb(53, 84, 94) : Color.FromArgb(193, 211, 214)) : BackColor);
        using var border = new Pen(Enabled ? accent : Color.FromArgb(116, 137, 145));
        e.Graphics.FillRectangle(fill, box); e.Graphics.DrawRectangle(border, box);
        if (Checked)
        {
            using var tick = new Pen(Color.White, 1.7f * scale);
            e.Graphics.DrawLines(tick, new[] { new PointF(box.X + size * .2f, box.Y + size * .5f), new PointF(box.X + size * .43f, box.Y + size * .74f), new PointF(box.X + size * .8f, box.Y + size * .23f) });
        }
        var text = right ? new Rectangle(0, 0, Math.Max(0, box.X - 6), Height) : new Rectangle(box.Right + 7, 0, Math.Max(0, Width - box.Right - 7), Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, text, Enabled ? ForeColor : dark ? Color.FromArgb(148, 165, 178) : Color.FromArgb(118, 128, 143), TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, text);
    }
    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = TextRenderer.MeasureText(Text, Font);
        return new Size(text.Width + (int)(29 * DeviceDpi / 96f), Math.Max(text.Height, (int)(24 * DeviceDpi / 96f)));
    }
}
