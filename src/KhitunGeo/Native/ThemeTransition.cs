using System.Runtime.CompilerServices;

namespace KhitunGeo.Native;

internal static class ThemeTransition
{
    private static readonly ConditionalWeakTable<Control, FadeWindow> running = new();
    public static void Apply(Control root, bool dark) => Change(root, () => NativeTheme.Apply(root, dark));

    // DWM fades one snapshot while the real controls receive the new state once.
    // Never WM_PRINT/DrawToBitmap live native controls: doing so can replay their
    // foregrounds onto sibling HWNDs during subsequent transparency painting.
    public static void Change(Control root, Action change)
    {
        if (root.IsDisposed || root.Disposing) return;
        var owner = root.FindForm();
        var bounds = root.RectangleToScreen(root.ClientRectangle);
        if (!root.Visible || !root.IsHandleCreated || owner is null || owner.WindowState == FormWindowState.Minimized ||
            !UiMotion.Enabled || !Screen.AllScreens.Any(screen => screen.Bounds.Contains(bounds)))
        {
            Stop(root); change(); return;
        }
        Bitmap before;
        try
        {
            before = new Bitmap(bounds.Width, bounds.Height);
            using var graphics = Graphics.FromImage(before);
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        }
        catch (System.ComponentModel.Win32Exception) { Stop(root); change(); return; }
        Stop(root);
        var layer = new FadeWindow(before) { Name = "TransitionLayer", Bounds = bounds };
        running.Add(root, layer);
        EventHandler moved = (_, _) => Stop(root);
        root.Resize += moved; root.LocationChanged += moved; owner.Deactivate += moved;
        layer.Disposed += (_, _) =>
        {
            root.Resize -= moved; root.LocationChanged -= moved; owner.Deactivate -= moved;
            running.Remove(root);
        };
        try
        {
            layer.Show(owner); layer.Update();
            root.SuspendLayout();
            try { change(); }
            finally { root.ResumeLayout(true); }
            UiMotion.Animate(layer, 240, t => layer.Opacity = 1 - t, () => Stop(root));
        }
        catch { Stop(root); throw; }
    }
    public static void Stop(Control root)
    {
        if (!running.TryGetValue(root, out var layer)) return;
        running.Remove(root); layer.Dispose(); root.Invalidate(true);
    }
    private sealed class FadeWindow : Form
    {
        private readonly Bitmap image;
        public FadeWindow(Bitmap image)
        {
            this.image = image;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual; AutoScaleMode = AutoScaleMode.None;
            BackgroundImage = image; BackgroundImageLayout = ImageLayout.None;
            DoubleBuffered = true; Opacity = 1;
        }
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= 0x00080000 | 0x00000020 | 0x08000000 | 0x00000080; // Layered, input-transparent, no activation, tool window.
                return parameters;
            }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x84) { m.Result = new IntPtr(-1); return; }
            base.WndProc(ref m);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { UiMotion.Cancel(this); BackgroundImage = null; image.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
