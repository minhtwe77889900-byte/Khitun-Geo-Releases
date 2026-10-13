using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace KhitunGeo.Native;

internal static class UiMotion
{
    private static readonly ConditionalWeakTable<Control, Motion> active = new();
    // Instant transitions avoid layout/paint stalls on large tables and drawings.
    public static bool Enabled => false;
    public static void Animate(Control owner, int duration, Action<double> frame, Action? completed = null)
    {
        Cancel(owner);
        if (owner.IsDisposed || owner.Disposing) return;
        if (!Enabled) { frame(1); completed?.Invoke(); return; }
        var motion = new Motion(owner, duration, frame, completed);
        active.Add(owner, motion); motion.Start();
    }
    public static void Cancel(Control owner)
    {
        if (active.TryGetValue(owner, out var motion)) { active.Remove(owner); motion.Dispose(); }
    }
    public static DialogResult ShowDialog(Form dialog, IWin32Window? owner)
    {
        if (Enabled)
        {
            dialog.Opacity = 0;
            dialog.Shown += (_, _) => Animate(dialog, 170, t => dialog.Opacity = t);
        }
        return dialog.ShowDialog(owner);
    }
    private sealed class Motion : IDisposable
    {
        private readonly Control owner;
        private readonly int duration;
        private readonly Action<double> frame;
        private readonly Action? completed;
        private readonly Stopwatch clock = new();
        private readonly System.Windows.Forms.Timer timer = new() { Interval = 15 };
        public Motion(Control owner, int duration, Action<double> frame, Action? completed)
        {
            this.owner = owner; this.duration = duration; this.frame = frame; this.completed = completed;
            timer.Tick += Tick; owner.Disposed += Disposed;
        }
        public void Start() { frame(0); clock.Start(); timer.Start(); }
        private void Tick(object? sender, EventArgs e)
        {
            if (owner.IsDisposed || owner.Disposing) { Cancel(owner); return; }
            var t = Math.Clamp(clock.Elapsed.TotalMilliseconds / duration, 0, 1);
            frame(t * t * (3 - 2 * t));
            if (t >= 1) { Cancel(owner); completed?.Invoke(); }
        }
        private void Disposed(object? sender, EventArgs e) => Cancel(owner);
        public void Dispose() { timer.Stop(); timer.Dispose(); owner.Disposed -= Disposed; }
    }
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, [MarshalAs(UnmanagedType.Bool)] out bool value, uint flags);
}
