using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var assembly = Assembly.Load("KhitunGeo");
        var type = assembly.GetType("KhitunGeo.Native.CoordinateSelectionPanel", true)!;
        var factory = type.GetMethod("Picker", BindingFlags.NonPublic | BindingFlags.Static)!;
        using var picker = (ComboBox)factory.Invoke(null, null)!;
        Require(picker.DropDownStyle == ComboBoxStyle.DropDownList && picker.AutoCompleteSource == AutoCompleteSource.ListItems, "CRS picker initializes");
        float scale = args.Length > 0 ? float.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture) : 1;
        using var form = (Form)Activator.CreateInstance(assembly.GetType("KhitunGeo.Native.NativeWorkspaceForm", true)!)!;
        var settings = Field(form, "settings");
        settings.GetType().GetProperty("AutoUpdateEnabled")!.SetValue(settings, false);
        var pointType = assembly.GetType("KhitunGeo.Native.SurveyPoint", true)!;
        var points = Array.CreateInstance(pointType, 8);
        for (int i = 0; i < points.Length; i++)
            points.SetValue(Activator.CreateInstance(pointType, new object?[] { (i + 1).ToString(), 5651203.214 + i * 24, 2301044.873 + i * 14, 12.0 + i * .25, i == 0 ? "Дно озера" : "Точка съёмки" }), i);
        var workspace = Field(form, "workspace");
        workspace.GetType().GetMethod("ReplacePoints")!.Invoke(workspace, new object?[] { points, null, false });
        form.Show(); Application.DoEvents();
        CheckMskSelection(form);
        CheckCrsSearch(assembly, form);
        CheckWorkspaceLayout(form);
        CheckTheme(form, scale);
        var savedSize = form.Size; form.Size = form.MinimumSize; Application.DoEvents();
        var zoomTools = Descendants(form).Single(c => c.Name == "ZoomTools");
        Require(zoomTools.Parent!.ClientRectangle.Contains(zoomTools.Bounds), "Zoom and Fit controls fit the real minimum-size window");
        Capture(form, "native-actual-minimum"); form.Size = savedSize; Application.DoEvents();
        if (scale == 1) CheckConversionExport(assembly, form, workspace, pointType);
        if (scale == 1) Capture(form, "native-live-window-1");
        // Check the real shown window first, then render its actual controls in an
        // offscreen host: hosted CI desktops are limited to 1024x768 pixels.
        foreach (var button in Descendants(form).OfType<Button>().Where(b => b.Text is "Импорт" or "Экспорт" or "⌃ Скрыть")) CheckButton(button);
        var root = form.Controls.Cast<Control>().Single();
        form.Controls.Remove(root);
        using var host = new Panel { Size = new Size(1280, 760), Font = form.Font };
        host.Controls.Add(root); host.CreateControl(); root.CreateControl();
        ScaleUi(host, scale);
        host.PerformLayout(); Application.DoEvents();
        Capture(host, $"native-main-{scale}");
        var theme = Descendants(host).Single(c => c.Name == "ThemeSwitcher");
        var apply = assembly.GetType("KhitunGeo.Native.NativeTheme")!.GetMethod("Apply")!;
        theme.GetType().GetMethod("SetDark")!.Invoke(theme, new object[] { true });
        apply.Invoke(null, new object[] { host, true }); Capture(host, $"native-main-dark-{scale}");
        theme.GetType().GetMethod("SetDark")!.Invoke(theme, new object[] { false });
        apply.Invoke(null, new object[] { host, false });
        var import = Descendants(host).OfType<Button>().Single(b => b.Text == "Импорт");
        var export = Descendants(host).OfType<Button>().Single(b => b.Text == "Экспорт");
        var view = Descendants(host).OfType<Button>().Single(b => b.Text == "⌃ Скрыть");
        foreach (var button in new[] { import, export, view }) CheckButton(button);
        Require(import.BackColor == export.BackColor && import.Size == export.Size, "Import/export use symmetric reference buttons");
        Require(Descendants(host).OfType<PictureBox>().Any(p => p.Visible && p.Image is not null && p.Height >= 24 * scale), "Application logo is visible");
        Require(form.Icon is not null, "Window has application icon");
        var toggle = Descendants(host).OfType<Button>().Single(b => b.AccessibleName == "Показать или скрыть левую панель");
        var collapsedRailWidth = Descendants(host).Single(c => c.Name == "NavigationRail").Width;
        toggle.PerformClick(); view.PerformClick(); Application.DoEvents();
        Require(import.Visible && export.Visible, "Import/export stay accessible with sidebar and preview hidden");
        foreach (var button in new[] { import, export, view }) CheckButton(button);
        Capture(host, $"native-hidden-{scale}");
        toggle.PerformClick();
        Require(Descendants(host).Single(c => c.Name == "NavigationRail").Width == collapsedRailWidth, "Navigation preserves its DPI scale after expanding and collapsing");
        ((Button)Field(form, "visualizationSidebarButton")).PerformClick();
        Require(!((SplitContainer)Field(form, "split")).Panel2Collapsed, "Preview restored through the visible sidebar action");
        host.Size = new Size((int)(880 * scale), (int)(560 * scale));
        host.PerformLayout();
        Application.DoEvents();
        foreach (var button in new[] { import, export, view }) CheckButton(button);
        Capture(host, $"native-small-{scale}");
        // Inspect the actual height dialog created by the production UI.
        var createDialog = form.GetType().GetMethod("CreateHeightDialog", BindingFlags.NonPublic | BindingFlags.Instance);
        Require(createDialog is not null, "Height tool exposes the actual dialog for UI inspection");
        using var dialog = (Form)createDialog!.Invoke(form, null)!;
        dialog.Show(form); Application.DoEvents();
        if (scale == 1) Capture(dialog, "native-live-height-1");
        var fields = dialog.Controls.Cast<Control>().Single();
        dialog.Controls.Remove(fields);
        using var dialogHost = new Panel { Size = dialog.ClientSize, Font = dialog.Font };
        dialogHost.Controls.Add(fields); dialogHost.CreateControl(); fields.CreateControl();
        ScaleUi(dialogHost, scale);
        Application.DoEvents();
        var operation = Descendants(dialogHost).OfType<ComboBox>().Single();
        Descendants(dialogHost).OfType<NumericUpDown>().Single().Value = 65.635m;
        Require(operation.Items.Cast<object>().Any(x => x.ToString()!.Contains("Абсолютная отметка − Z")), "Absolute minus Z is selectable");
        Require(operation.Items.Cast<object>().Any(x => x.ToString()!.Contains("Абсолютная отметка + Z")), "Absolute plus Z is selectable");
        foreach (var button in Descendants(dialogHost).OfType<Button>()) CheckButton(button);
        Capture(dialogHost, $"native-height-{scale}");
        Console.WriteLine($"PASS native layout, logo, persistent actions and height dialog at {scale * 100}%");
    }

    private static void CheckConversionExport(Assembly assembly, Form form, object workspace, Type pointType)
    {
        var selection = Field(form, "conversion");
        Console.WriteLine("Export diagnostic initial CRS: " + selection.GetType().GetProperty("SourceName")!.GetValue(selection) + " -> " + selection.GetType().GetProperty("TargetName")!.GetValue(selection));
        var source = (ComboBox)Field(selection, "source");
        var target = (ComboBox)Field(selection, "target");
        Require((string)target.SelectedItem!.GetType().GetProperty("Id")!.GetValue(target.SelectedItem)! == "msk164",
            "Showing the window reset target CRS to the first catalogue item");
        object Option(ComboBox picker, string id) => picker.Items.Cast<object>().Single(x => (string)x.GetType().GetProperty("Id")!.GetValue(x)! == id);
        source.SelectedItem = Option(source, "wgs");
        target.SelectedItem = Option(target, "msk164");
        var input = Array.CreateInstance(pointType, 2);
        for (var i = 0; i < 2; i++) input.SetValue(Activator.CreateInstance(pointType, new object?[] { "00" + (i + 1), 65.0 + i * .001, 84.0 + i * .001, -12.0 + i, "Глубина" }), i);
        workspace.GetType().GetMethod("ReplacePoints")!.Invoke(workspace, new object?[] { input, null, false });
        var task = (Task)form.GetType().GetMethod("ConvertCoordinates", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(form, null)!;
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!task.IsCompleted && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(10); }
        Require(task.IsCompleted, "Actual conversion UI must complete");
        task.GetAwaiter().GetResult();
        var current = workspace.GetType().GetProperty("Points")!.GetValue(workspace)!;
        var values = ((System.Collections.IEnumerable)current).Cast<object>().ToArray();
        Require(values.Length == 2 && (double)pointType.GetProperty("X")!.GetValue(values[0])! > 1000, "Actual conversion writes projected points to workspace");
        // Reproduce the reported case: projected table coordinates with WGS-84
        // selected for the next calculation. Export must still use the table.
        source.SelectedItem = Option(source, "wgs");
        Exception? buttonFailure = null;
        bool openedByButton = false;
        using (var timer = new System.Windows.Forms.Timer { Interval = 100 })
        {
            timer.Tick += (_, _) =>
            {
                var active = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.GetType().Name == "PointExportDialog");
                if (active is null) return;
                timer.Stop(); openedByButton = true;
                try
                {
                    ((ComboBox)Field(active, "format")).SelectedIndex = 0;
                    Require(((DataGridView)Field(active, "preview")).Rows.Count == 2,
                        "Export button lost transformed table points: " + ((Label)Field(active, "notice")).Text);
                    Capture(active, "native-export-button");
                }
                catch (Exception ex) { buttonFailure = ex; }
                finally { active.Close(); }
            };
            timer.Start();
            Descendants(form).OfType<Button>().Single(b => b.Text == "Экспорт").PerformClick();
        }
        Require(openedByButton, "Export button must open the production export dialog");
        if (buttonFailure is not null) throw buttonFailure;
        var exportType = assembly.GetType("KhitunGeo.Native.PointExportDialog", true)!;
        using var export = (Form)Activator.CreateInstance(exportType, new[] { current, selection.GetType().GetProperty("SourceIsGeographic")!.GetValue(selection)!, selection.GetType().GetProperty("SourceName")!.GetValue(selection)!, false })!;
        export.Show(form); Application.DoEvents();
        var format = (ComboBox)Field(export, "format");
        var preview = (DataGridView)Field(export, "preview");
        foreach (var index in new[] { 0, 1, 3, 4, 5, 6 })
        {
            format.SelectedIndex = index; Application.DoEvents();
            var notice = ((Label)Field(export, "notice")).Text;
            Console.WriteLine("Export diagnostic " + format.Text + ": " + notice);
            Capture(export, "native-export-" + index);
            Require(preview.Rows.Count == 2, "Converted points missing from " + format.Text + ": " + notice);
            Require(((Button)Field(export, "save")).Enabled, "Saving converted points is disabled: " + notice);
            Require(preview.Rows[0].Cells.Cast<DataGridViewCell>().Any(c => Convert.ToString(c.Value) == "-12.000"), "Converted height must appear in export preview");
        }
        Console.WriteLine("PASS actual Export button and AutoCAD/Civil 3D previews retain projected points and heights even with WGS-84 selected");
    }

    private static void CheckMskSelection(Form form)
    {
        var selection = Field(form, "conversion"); var type = selection.GetType();
        var undo = (Action)type.GetMethod("CaptureUndo")!.Invoke(selection, null)!;
        var select = type.GetMethod("SelectSource")!;
        Require((bool)select.Invoke(selection, new object[] { "msk:10|6-градусная|2" })!, "Import can select a concrete MSK zone");
        var before = (string)type.GetMethod("SettingsSnapshot")!.Invoke(selection, null)!;
        Require(type.GetProperty("SourceName")!.GetValue(selection)!.ToString()!.Contains("зона 2"), "Selected zone remains visible in source name");
        using (var timer = new System.Windows.Forms.Timer { Interval = 80 })
        {
            timer.Tick += (_, _) =>
            {
                var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.GetType().Name == "MskZoneDialog");
                if (dialog is null) return;
                timer.Stop();
                var zones = Descendants(dialog).OfType<ListBox>().Single();
                Require(zones.Items.Count > 1 && zones.Items.Cast<object>().All(z => z.GetType().GetProperty("Id")!.GetValue(z)!.ToString()!.StartsWith("msk:10|")), "Zone dialog contains only matching catalogue records");
                Require(zones.SelectedItem!.GetType().GetProperty("Id")!.GetValue(zones.SelectedItem)!.ToString() == "msk:10|6-градусная|2", "Opening zone dialog preserves selected concrete zone");
                Descendants(dialog).OfType<Button>().Single(b => b.Text == "Отмена").PerformClick();
            };
            timer.Start();
            type.GetMethod("ChooseZone", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(selection, new[] { Field(selection, "source") });
        }
        Require(before == (string)type.GetMethod("SettingsSnapshot")!.Invoke(selection, null)!, "Cancel zone dialog preserves exact projection parameters");
        // Cancellation must restore the ambiguous-zone state as well as CRS IDs.
        var sourcePicker = (ComboBox)Field(selection, "source");
        var targetPicker = (ComboBox)Field(selection, "target");
        targetPicker.SelectedItem = targetPicker.Items.Cast<object>().Single(x => x.GetType().GetProperty("Id")!.GetValue(x)!.ToString() == "gsk3");
        ((CheckBox)Field(selection, "targetPrefix")).Checked = false;
        type.GetMethod("AcceptResult")!.Invoke(selection, new object[] { new[] { 3, 4 } });
        Require((bool)Field(selection, "ambiguousSourceZones"), "Different unprefixed result zones require explicit source selection");
        before = (string)type.GetMethod("SettingsSnapshot")!.Invoke(selection, null)!;
        sourcePicker.SelectedItem = sourcePicker.Items.Cast<object>().Single(x => x.GetType().GetProperty("Id")!.GetValue(x)!.ToString() == "msk-group:10");
        using (var cancelTimer = new System.Windows.Forms.Timer { Interval = 80 })
        {
            cancelTimer.Tick += (_, _) =>
            {
                var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.GetType().Name == "MskZoneDialog");
                if (dialog is null) return;
                cancelTimer.Stop(); dialog.DialogResult = DialogResult.Cancel; dialog.Close();
            };
            cancelTimer.Start();
            type.GetMethod("ChooseZone", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(selection, new object[] { sourcePicker });
        }
        Require(before == (string)type.GetMethod("SettingsSnapshot")!.Invoke(selection, null)! && (bool)Field(selection, "ambiguousSourceZones"), "Cancelling a different MSK preserves the full previous settings and ambiguous-zone state");
        Require(!((Button)Field(selection, "convert")).Enabled, "Cancel must retain the prior conversion state");
        undo();
        Console.WriteLine("PASS MSK grouping, exact imported zone, actual zone dialog and atomic cancel/undo");
    }

    private static void CheckCrsSearch(Assembly assembly, Form form)
    {
        var selection = Field(form, "conversion");
        var picker = (ComboBox)Field(selection, "source");
        var before = (string)selection.GetType().GetMethod("SettingsSnapshot")!.Invoke(selection, null)!;
        var dialogType = assembly.GetType("KhitunGeo.Native.CrsSearchDialog");
        Require(dialogType is not null, "CRS dropdown must offer a searchable system catalogue");
        using var dialog = (Form)Activator.CreateInstance(dialogType!, new object?[] { picker.DataSource, picker.SelectedItem, false })!;
        dialog.Show(form); Application.DoEvents();
        var search = Descendants(dialog).OfType<TextBox>().Single();
        var results = Descendants(dialog).OfType<ListBox>().Single();
        var count = results.Items.Count;
        search.Text = "мск 164"; Application.DoEvents();
        Require(results.Items.Count > 0 && results.Items.Count < count, "CRS search filters Cyrillic names ignoring separators");
        search.Text = "not-a-coordinate-system"; Application.DoEvents();
        Require(results.Items.Count == 0 && !((Button)dialog.AcceptButton!).Enabled, "Empty search cannot select a CRS");
        search.Text = ""; Application.DoEvents();
        Require(results.Items.Count == count, "Clearing search restores the full catalogue");
        search.Text = "гск2011"; Application.DoEvents();
        Require(results.Items.Count > 0, "Search supports GSK-2011 without spaces or hyphens");
        Capture(dialog, "native-crs-search");
        dialog.Close();
        Require(before == (string)selection.GetType().GetMethod("SettingsSnapshot")!.Invoke(selection, null)!, "Searching and cancelling must not mutate conversion settings");
        Console.WriteLine("PASS searchable CRS catalogue, empty results and cancellation");
    }

    private static void CheckWorkspaceLayout(Form form)
    {
        var selection = Field(form, "conversion");
        var source = (ComboBox)Field(selection, "source"); var target = (ComboBox)Field(selection, "target");
        Require(Math.Abs(source.Width - target.Width) <= 1 && source.Top == target.Top && source.Height == target.Height, "Source and target selectors are symmetric");
        Require(Descendants(form).Single(c => c.Name == "NavigationRail").Width == 76, "Navigation starts as the compact reference icon rail");
        var split = (SplitContainer)Field(form, "split");
        Require(split.SplitterWidth == 8, "Workspace divider is a clean narrow line");
        var left = split.Panel1.Controls[0]; var right = split.Panel2.Controls[0];
        Require(left.Padding == right.Padding && left.Top == right.Top && left.Height == right.Height, "Workspace panes share equal padding and vertical edges");
        var initial = split.SplitterDistance;
        split.SplitterDistance = Math.Min(initial + 25, split.Width - split.Panel2MinSize - split.SplitterWidth);
        Application.DoEvents();
        Require(left.Height == right.Height, "Moving divider preserves pane alignment");
        split.Panel2Collapsed = true; split.Panel2Collapsed = false; split.SplitterDistance = initial;
        Console.WriteLine("PASS divider movement, hide/restore and workspace pane alignment");
    }

    private static void CheckTheme(Form form, float scale)
    {
        var switcher = Descendants(form).Single(c => c.Name == "ThemeSwitcher");
        switcher.GetType().GetMethod("SetDark")!.Invoke(switcher, new object[] { true });
        var deadline = DateTime.UtcNow.AddMilliseconds(450);
        while (DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(10); }
        Require(form.BackColor == Color.FromArgb(20, 29, 37), "Dark theme transition reaches the graphite palette");
        if (scale == 1) Capture(form, "native-live-dark-1");
        switcher.GetType().GetMethod("SetDark")!.Invoke(switcher, new object[] { false });
        deadline = DateTime.UtcNow.AddMilliseconds(450);
        while (DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(10); }
        Require(form.BackColor == Color.FromArgb(244, 245, 241), "Light theme transition restores the approved palette");
        Console.WriteLine("PASS reversible theme transitions and both native palettes");
    }

    private static object Field(object instance, string name) => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
    private static IEnumerable<Control> Descendants(Control root) => root.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void CheckButton(Button button)
    {
        for (Control? parent = button.Parent; parent is not null; parent = parent.Parent)
            Require(parent.RectangleToScreen(parent.ClientRectangle).Contains(button.RectangleToScreen(button.ClientRectangle)), $"Button '{button.Text}' is clipped by ancestor {parent.GetType().Name}");
        Require(button.Parent!.ClientRectangle.Contains(button.Bounds), $"Button '{button.Text}' is clipped by its parent: {button.Bounds} / {button.Parent.ClientRectangle}");
        var text = TextRenderer.MeasureText(button.Text, button.Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        Require(button.Height >= text.Height + 8 && button.Width >= text.Width + 12, $"Button '{button.Text}' is too small for its text");
    }
    private static void ScaleUi(Control root, float scale)
    {
        if (scale == 1) return;
        var fonts = new[] { root }.Concat(Descendants(root)).Select(c => (Control: c, Font: c.Font)).ToArray();
        root.Scale(new SizeF(scale, scale));
        foreach (var item in fonts) item.Control.Font = new Font(item.Font.FontFamily, item.Font.Size * scale, item.Font.Style);
    }
    private static void Capture(Control form, string name)
    {
        Directory.CreateDirectory("test-artifacts");
        using var image = new Bitmap(form.Width, form.Height);
        if (form is Form live && live.Visible && name.StartsWith("native-live-"))
        {
            using var graphics = Graphics.FromImage(image);
            graphics.CopyFromScreen(live.Location, Point.Empty, image.Size);
        }
        else form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
        image.Save(Path.Combine("test-artifacts", name + ".png"), ImageFormat.Png);
        if (name == "native-live-window-1" || name == "native-live-height-1" || name == "native-live-dark-1")
        {
            using var bytes = new MemoryStream(); image.Save(bytes, ImageFormat.Png);
            Console.WriteLine("SCREENSHOT " + name + " " + Convert.ToBase64String(bytes.ToArray()));
        }
    }
}
