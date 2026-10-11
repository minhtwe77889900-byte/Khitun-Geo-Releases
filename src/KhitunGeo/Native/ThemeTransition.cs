using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
        if (!root.Visible || !root.IsHandleCreated || bounds.Width <= 0 || bounds.Height <= 0 || owner is null || owner.WindowState == FormWindowState.Minimized ||
            !UiMotion.Enabled || !Screen.AllScreens.Any(screen => screen.Bounds.Contains(bounds)))
        {
            Stop(root); change(); return;
        }
        Bitmap? before = null;
        try
        {
            before = new Bitmap(bounds.Width, bounds.Height);
            using var graphics = Graphics.FromImage(before);
            var screenDc = GetDC(IntPtr.Zero);
            if (screenDc == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
            var targetDc = IntPtr.Zero;
            try
            {
                targetDc = graphics.GetHdc();
                // Include the still-visible layered snapshot when reversing a fade.
                if (!BitBlt(targetDc, 0, 0, bounds.Width, bounds.Height, screenDc, bounds.X, bounds.Y, 0x40CC0020))
                    throw new System.ComponentModel.Win32Exception();
            }
            finally
            {
                if (targetDc != IntPtr.Zero) graphics.ReleaseHdc(targetDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }
        catch (System.ComponentModel.Win32Exception) { before?.Dispose(); Stop(root); change(); return; }
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
            UiMotion.Animate(layer, 240, t => layer.Opacity = .999 * (1 - t), () => Stop(root));
        }
        catch { Stop(root); throw; }
    }
    public static void Stop(Control root)
    {
        if (!running.TryGetValue(root, out var layer)) return;
        running.Remove(root); layer.Dispose(); root.Invalidate(true);
    }
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BitBlt(IntPtr target, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, uint operation);
    private sealed class FadeWindow : Form
    {
        private readonly Bitmap image;
        public FadeWindow(Bitmap image)
        {
            this.image = image;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual; AutoScaleMode = AutoScaleMode.None;
            BackgroundImage = image; BackgroundImageLayout = ImageLayout.None;
            DoubleBuffered = true; Opacity = .999;
        }
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= 0x00000020 | 0x08000000 | 0x00000080; // Input-transparent, no activation, tool window; Opacity owns layered style.
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
