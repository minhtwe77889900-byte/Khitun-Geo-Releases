using System.Drawing.Imaging;
using System.Runtime.CompilerServices;

namespace KhitunGeo.Native;

internal static class ThemeTransition
{
    private static readonly ConditionalWeakTable<Control, FadeLayer> running = new();
    public static void Apply(Control root, bool dark) => Change(root, () => NativeTheme.Apply(root, dark));

    // Native controls receive the new state once. Only the buffered layer paints each frame.
    public static void Change(Control root, Action change)
    {
        if (root.IsDisposed || root.Disposing) return;
        if (!root.Visible || !root.IsHandleCreated || !UiMotion.Enabled) { Stop(root); change(); return; }
        var before = CaptureClient(root);
        Stop(root); root.SuspendLayout();
        try { change(); }
        catch { before.Dispose(); throw; }
        finally { root.ResumeLayout(true); }
        Bitmap after;
        try { after = CaptureClient(root); }
        catch { before.Dispose(); throw; }
        var layer = new FadeLayer(before, after) { Name = "TransitionLayer", Dock = DockStyle.Fill };
        running.Add(root, layer); root.Controls.Add(layer); layer.BringToFront();
        EventHandler resized = (_, _) => Stop(root);
        root.Resize += resized;
        layer.Disposed += (_, _) => { root.Resize -= resized; running.Remove(root); };
        UiMotion.Animate(layer, 240, t => { layer.Progress = (float)t; layer.Invalidate(); }, () => Stop(root));
    }
    public static void Stop(Control root)
    {
        if (!running.TryGetValue(root, out var layer)) return;
        running.Remove(root); root.Controls.Remove(layer); layer.Dispose(); root.Invalidate(true);
    }
    private static Bitmap CaptureClient(Control root)
    {
        using var whole = new Bitmap(Math.Max(1, root.Width), Math.Max(1, root.Height));
        root.DrawToBitmap(whole, new Rectangle(Point.Empty, whole.Size));
        var offset = root is Form form ? new Point(form.PointToScreen(Point.Empty).X - form.Left, form.PointToScreen(Point.Empty).Y - form.Top) : Point.Empty;
        return whole.Clone(new Rectangle(offset, root.ClientSize), PixelFormat.Format32bppPArgb);
    }
    private sealed class FadeLayer : Control
    {
        private readonly Bitmap before, after;
        public float Progress { get; set; }
        public FadeLayer(Bitmap before, Bitmap after)
        {
            this.before = before; this.after = after;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
            TabStop = false;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.DrawImageUnscaled(after, Point.Empty);
            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(new ColorMatrix { Matrix33 = 1 - Progress });
            e.Graphics.DrawImage(before, ClientRectangle, 0, 0, before.Width, before.Height, GraphicsUnit.Pixel, attributes);
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x84) { m.Result = new IntPtr(-1); return; } // HTTRANSPARENT preserves input during a fade.
            base.WndProc(ref m);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { UiMotion.Cancel(this); before.Dispose(); after.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
