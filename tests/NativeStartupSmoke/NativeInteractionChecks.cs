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
            Capture(form, "native-live-expanded-navigation");
            try { Require(switcher.Size == size, "Theme switch must have identical bounds in expanded and collapsed navigation"); }
            finally { toggle.PerformClick(); PumpFor(350); }
        });
        Check("Navigation icons show the background through their corners", () =>
        {
            var button = Descendants(form).OfType<Button>().Single(b => b.AccessibleName == "Чертёж DWG / DXF");
            Cursor.Position = form.PointToScreen(new Point(form.ClientSize.Width - 30, 70)); PumpFor(180);
            var backdrop = button.Parent!;
            while (backdrop.GetType().Name != "BrandPanel") backdrop = backdrop.Parent!;
            using var expected = new Bitmap(backdrop.Width, backdrop.Height);
            using (var graphics = Graphics.FromImage(expected))
                backdrop.GetType().GetMethod("OnPaintBackground", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(backdrop, new object[] { new PaintEventArgs(graphics, backdrop.ClientRectangle) });
            using var actual = new Bitmap(button.Width, button.Height);
            using (var graphics = Graphics.FromImage(actual)) graphics.CopyFromScreen(button.PointToScreen(Point.Empty), Point.Empty, actual.Size);
            var offset = backdrop.PointToClient(button.PointToScreen(Point.Empty));
            foreach (var patch in new[] { new Point(2, 2), new Point(button.Width - 8, 2), new Point(2, button.Height - 8), new Point(button.Width - 8, button.Height - 8) })
                for (int x = 0; x < 5; x++) for (int y = 0; y < 5; y++)
                {
                    var a = actual.GetPixel(patch.X + x, patch.Y + y); var b = expected.GetPixel(offset.X + patch.X + x, offset.Y + patch.Y + y);
                    Require(Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B) <= 6,
                        "Icon corner pixels must match the artwork behind them instead of a rectangle or stale sibling text");
                }
        });
        Check("Rapid animation reversal leaves the latest theme and navigation state", () =>
        {
            var switcher = Descendants(form).Single(c => c.Name == "ThemeSwitcher");
            var setDark = switcher.GetType().GetMethod("SetDark")!;
            setDark.Invoke(switcher, new object[] { true }); PumpFor(70);
            if (Application.OpenForms.Cast<Form>().Any(c => c.Name == "TransitionLayer")) Capture(form, "native-live-theme-frame");
            setDark.Invoke(switcher, new object[] { false }); PumpFor(45);
            setDark.Invoke(switcher, new object[] { true }); PumpFor(350);
            Require(form.BackColor == Color.FromArgb(20, 29, 37), "Last theme request wins");
            Require(!Application.OpenForms.Cast<Form>().Any(c => c.Name == "TransitionLayer"), "Completed fades release their overlay and images");
            var rail = Descendants(form).Single(c => c.Name == "NavigationRail"); var width = rail.Width;
            var toggle = Descendants(form).OfType<Button>().Single(b => b.AccessibleName == "Показать или скрыть левую панель");
            toggle.PerformClick(); PumpFor(60); toggle.PerformClick(); PumpFor(300);
            Require(rail.Width == width, "Interrupted navigation expansion returns to its original DPI-scaled width");
            setDark.Invoke(switcher, new object[] { false }); PumpFor(350);
        });
        Check("Ctrl+Z undoes workspace actions with focus outside the table", () =>
        {
            var workspace = Field(form, "workspace");
            var points = ((System.Collections.IEnumerable)workspace.GetType().GetProperty("Points")!.GetValue(workspace)!).Cast<object>().ToArray();
            workspace.GetType().GetMethod("SwapXY")!.Invoke(workspace, null);
            Descendants(form).OfType<Button>().Single(b => b.Text == "Импорт").Focus();
            Require(SendUndo(form), "Main window handles Ctrl+Z when a toolbar button has focus");
            var restored = ((System.Collections.IEnumerable)workspace.GetType().GetProperty("Points")!.GetValue(workspace)!).Cast<object>().ToArray();
            Require(restored.SequenceEqual(points), "Ctrl+Z restores all point fields outside table focus");
        });
        Check("Explicit GSK-2011 zone 14 converts the saved control and undoes with button focus", () =>
        {
            var selection = Field(form, "conversion");
            selection.GetType().GetMethod("SelectSource")!.Invoke(selection, new object[] { "gsk6" });
            var zoneButton = (Button)Field(selection, "sourceZone");
            Require(zoneButton.Visible, "GSK-2011 exposes a visible explicit source-zone selector");
            bool opened = false; Exception? dialogFailure = null;
            using var accept = new System.Windows.Forms.Timer { Interval = 30 };
            accept.Tick += (_, _) =>
            {
                var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.GetType().Name == "ZonedCrsDialog");
                if (dialog is null) return;
                accept.Stop(); opened = true;
                try
                {
                    Descendants(dialog).OfType<NumericUpDown>().Single(c => c.Name == "ZoneNumber").Value = 14;
                    Descendants(dialog).OfType<CheckBox>().Single(c => c.Name == "ZonePrefix").Checked = false;
                    Require(Descendants(dialog).OfType<Label>().Any(c => c.Text.Contains("78–84")), "Zone 14 shows its 78–84 degree range");
                    dialog.DialogResult = DialogResult.OK;
                }
                catch (Exception ex) { dialogFailure = ex; dialog.DialogResult = DialogResult.Cancel; }
                finally { dialog.Close(); }
            };
            accept.Start(); zoneButton.PerformClick();
            if (dialogFailure is not null) throw dialogFailure;
            Require(opened, "Zone button opens the actual coordinate-zone dialog");
            var snapshot = (string)selection.GetType().GetMethod("SettingsSnapshot")!.Invoke(selection, null)!;
            using (var request = System.Text.Json.JsonDocument.Parse(snapshot))
            {
                var zone = request.RootElement.GetProperty("zone");
                Require(!zone.GetProperty("auto").GetBoolean() && zone.GetProperty("manual").GetInt32() == 14 && !zone.GetProperty("sourcePrefix").GetBoolean(),
                    "Explicit zone selection supplies zone 14 with unprefixed input Y");
            }
            var workspace = Field(form, "workspace"); var pointType = form.GetType().Assembly.GetType("KhitunGeo.Native.SurveyPoint", true)!;
            var input = Array.CreateInstance(pointType, 1);
            var original = Activator.CreateInstance(pointType, new object?[] { "Контроль", 7465965.922, 599112.246, -12.0, "ГСК-2011 зона 14" })!;
            input.SetValue(original, 0);
            workspace.GetType().GetMethod("ReplacePoints")!.Invoke(workspace, new object?[] { input, null, false });
            var task = (Task)form.GetType().GetMethod("ConvertCoordinates", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(form, null)!;
            var deadline = Environment.TickCount64 + 30000;
            while (!task.IsCompleted && Environment.TickCount64 < deadline) PumpFor(10);
            Require(task.IsCompleted, "Real conversion completes"); task.GetAwaiter().GetResult();
            var converted = ((System.Collections.IEnumerable)workspace.GetType().GetProperty("Points")!.GetValue(workspace)!).Cast<object>().Single();
            Require(Math.Abs((double)pointType.GetProperty("X")!.GetValue(converted)! - 921553.841) < .002 &&
                Math.Abs((double)pointType.GetProperty("Y")!.GetValue(converted)! - 55956.792) < .002, "Real Windows conversion matches the previous control within 2 mm");
            Require((double)pointType.GetProperty("Height")!.GetValue(converted)! == -12, "Conversion preserves entered Z");
            ((Button)Field(selection, "convert")).Focus();
            Require(SendUndo(form), "Ctrl+Z is handled with Convert button focus");
            var restored = ((System.Collections.IEnumerable)workspace.GetType().GetProperty("Points")!.GetValue(workspace)!).Cast<object>().Single();
            Require(Equals(original, restored) && snapshot == (string)selection.GetType().GetMethod("SettingsSnapshot")!.Invoke(selection, null)!,
                "Conversion undo restores point fields, source CRS, zone and prefix together");
        });
        if (failures.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
        Capture(form, "native-live-interactions");
    }
    private static bool SendUndo(Form form)
    {
        var message = Message.Create(form.Handle, 0x100, new IntPtr((int)Keys.Z), IntPtr.Zero);
        return (bool)form.GetType().GetMethod("ProcessCmdKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(form, new object[] { message, Keys.Control | Keys.Z })!;
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
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetAnimationSetting(uint action, uint parameter, IntPtr value, uint flags);
    private sealed class AnimationSettings : IDisposable
    {
        private bool original;
        private readonly bool restore;
        public AnimationSettings()
        {
            restore = AnimationSetting(0x1042, 0, ref original, 0);
            if (restore) SetAnimationSetting(0x1043, 0, new IntPtr(1), 0);
        }
        public void Dispose() { if (restore) SetAnimationSetting(0x1043, 0, original ? new IntPtr(1) : IntPtr.Zero, 0); }
    }
}
