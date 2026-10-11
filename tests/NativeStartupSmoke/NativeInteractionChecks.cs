using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static partial class Program
{
    private static void CheckInteractionRegressions(Form form)
    {
        using var animationSettings = new AnimationSettings();
        var failures = new List<string>();
        void Check(string name, Action action)
        {
            try { action(); Console.WriteLine("PASS " + name); }
            catch (Exception e) { failures.Add(name + ": " + e.Message); Console.WriteLine("FAIL " + failures[^1]); }
        }
        Check("Drawing menu stays alive through close and reopen", () =>
        {
            var button = Descendants(form).OfType<Button>().Single(b => b.AccessibleName == "Чертёж DWG / DXF");
            ContextMenuStrip? first = null;
            for (int i = 0; i < 3; i++)
            {
                button.PerformClick(); Application.DoEvents();
                var menu = FindPopupMenu();
                Require(menu is not null && menu.Visible, "Drawing popup opens");
                Require(menu!.Items.Count == 3, "Drawing actions remain available");
                if (first is not null) Require(ReferenceEquals(first, menu), "Repeated opening reuses the owned popup");
                first = menu; menu.Close(ToolStripDropDownCloseReason.Keyboard); Application.DoEvents();
                Require(!menu.IsDisposed, "Close must not dispose a popup while Windows is still finishing its close message");
            }
        });
        Check("CRS opens search directly without native dropdown flash", () =>
        {
            var selection = Field(form, "conversion"); var source = (ComboBox)Field(selection, "source");
            var before = selection.GetType().GetMethod("SettingsSnapshot")!.Invoke(selection, null);
            var opened = false;
            using var closeSearch = new System.Windows.Forms.Timer { Interval = 30 };
            closeSearch.Tick += (_, _) =>
            {
                var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Text == "Поиск системы координат");
                if (dialog is null) return;
                opened = true; dialog.DialogResult = DialogResult.Cancel; dialog.Close();
            };
            closeSearch.Start(); source.DroppedDown = true;
            var nativeListVisible = source.DroppedDown;
            PumpFor(400);
            Require(opened, "Search dialog opens for the dropdown command");
            Require(!nativeListVisible, "The native list must never become visible before the search dialog");
            Require(Equals(before, selection.GetType().GetMethod("SettingsSnapshot")!.Invoke(selection, null)), "Cancelling direct search preserves the CRS");
        });
        Check("Theme applies control styles once per transition", () =>
        {
            var switcher = Descendants(form).Single(c => c.Name == "ThemeSwitcher");
            var button = Descendants(form).OfType<Button>().Single(b => b.Text == "Импорт");
            var changes = 0; EventHandler changed = (_, _) => changes++;
            button.BackColorChanged += changed;
            try
            {
                switcher.GetType().GetMethod("SetDark")!.Invoke(switcher, new object[] { true }); PumpFor(500);
                Require(form.BackColor == Color.FromArgb(20, 29, 37), "Dark transition completes");
                Require(changes <= 2, $"Native button background was rewritten {changes} times during one transition");
            }
            finally
            {
                button.BackColorChanged -= changed;
                switcher.GetType().GetMethod("SetDark")!.Invoke(switcher, new object[] { false }); PumpFor(500);
            }
        });
        Check("Theme switch retains its compact size when navigation expands", () =>
        {
            var switcher = Descendants(form).Single(c => c.Name == "ThemeSwitcher"); var size = switcher.Size;
            var toggle = Descendants(form).OfType<Button>().Single(b => b.AccessibleName == "Показать или скрыть левую панель");
            toggle.PerformClick(); PumpFor(350);
            try { Require(switcher.Size == size, "Theme switch must have identical bounds in expanded and collapsed navigation"); }
            finally { toggle.PerformClick(); PumpFor(350); }
        });
        Check("Navigation icons show the background through their corners", () =>
        {
            var button = Descendants(form).OfType<Button>().Single(b => b.AccessibleName == "Чертёж DWG / DXF");
            Require(button.BackColor == Color.Transparent, "Unselected navigation icons must not cover the artwork with rectangular background patches");
        });
        Check("Rapid animation reversal leaves the latest theme and navigation state", () =>
        {
            var switcher = Descendants(form).Single(c => c.Name == "ThemeSwitcher");
            var setDark = switcher.GetType().GetMethod("SetDark")!;
            setDark.Invoke(switcher, new object[] { true }); PumpFor(70);
            if (Descendants(form).Any(c => c.Name == "TransitionLayer")) Capture(form, "native-live-theme-frame");
            setDark.Invoke(switcher, new object[] { false }); PumpFor(45);
            setDark.Invoke(switcher, new object[] { true }); PumpFor(350);
            Require(form.BackColor == Color.FromArgb(20, 29, 37), "Last theme request wins");
            Require(!Descendants(form).Any(c => c.Name == "TransitionLayer"), "Completed fades release their overlay and images");
            var rail = Descendants(form).Single(c => c.Name == "NavigationRail"); var width = rail.Width;
            var toggle = Descendants(form).OfType<Button>().Single(b => b.AccessibleName == "Показать или скрыть левую панель");
            toggle.PerformClick(); PumpFor(60); toggle.PerformClick(); PumpFor(300);
            Require(rail.Width == width, "Interrupted navigation expansion returns to its original DPI-scaled width");
            setDark.Invoke(switcher, new object[] { false }); PumpFor(350);
        });
        if (failures.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
        Capture(form, "native-live-interactions");
    }
    private static void PumpFor(int milliseconds)
    {
        var deadline = Environment.TickCount64 + milliseconds;
        while (Environment.TickCount64 < deadline) { Application.DoEvents(); Thread.Sleep(5); }
    }
    private static ContextMenuStrip? FindPopupMenu()
    {
        ContextMenuStrip? result = null;
        EnumThreadWindows(GetCurrentThreadId(), (handle, _) =>
        {
            if (Control.FromHandle(handle) is ContextMenuStrip { Visible: true } menu) result = menu;
            return true;
        }, IntPtr.Zero);
        return result;
    }
    private delegate bool EnumWindow(IntPtr handle, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumThreadWindows(uint threadId, EnumWindow callback, IntPtr parameter);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AnimationSetting(uint action, uint parameter, [MarshalAs(UnmanagedType.Bool)] ref bool value, uint flags);
    private sealed class AnimationSettings : IDisposable
    {
        private bool original;
        private readonly bool restore;
        public AnimationSettings()
        {
            restore = AnimationSetting(0x1042, 0, ref original, 0);
            if (restore) { var enabled = true; AnimationSetting(0x1043, 0, ref enabled, 0); }
        }
        public void Dispose() { if (restore) AnimationSetting(0x1043, 0, ref original, 0); }
    }
}
