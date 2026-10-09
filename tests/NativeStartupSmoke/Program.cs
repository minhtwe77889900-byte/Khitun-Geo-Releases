using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
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
        form.MaximumSize = new Size(4000, 3000);
        var settings = Field(form, "settings");
        settings.GetType().GetProperty("AutoUpdateEnabled")!.SetValue(settings, false);
        var pointType = assembly.GetType("KhitunGeo.Native.SurveyPoint", true)!;
        var points = Array.CreateInstance(pointType, 8);
        for (int i = 0; i < points.Length; i++)
            points.SetValue(Activator.CreateInstance(pointType, new object?[] { (i + 1).ToString(), 5651203.214 + i * 24, 2301044.873 + i * 14, 12.0 + i * .25, i == 0 ? "Дно озера" : "Точка съёмки" }), i);
        var workspace = Field(form, "workspace");
        workspace.GetType().GetMethod("ReplacePoints")!.Invoke(workspace, new object?[] { points, null, false });
        form.Show(); Application.DoEvents();
        if (scale != 1) form.Scale(new SizeF(scale, scale));
        form.ClientSize = new Size((int)(1280 * scale), (int)(760 * scale));
        form.PerformLayout(); Application.DoEvents();
        Console.WriteLine($"Layout scale={scale}; form={form.Size}; client={form.ClientSize}; screen={Screen.PrimaryScreen!.Bounds}");
        Capture(form, $"native-main-{scale}");
        var import = Descendants(form).OfType<Button>().Single(b => b.Text == "Импорт");
        var export = Descendants(form).OfType<Button>().Single(b => b.Text == "Экспорт");
        var view = Descendants(form).OfType<Button>().Single(b => b.Text == "Вид: скрыть");
        foreach (var button in new[] { import, export, view }) CheckButton(button);
        Require(import.BackColor == Color.FromArgb(8, 127, 115) && import.ForeColor == Color.White, "Primary import button retains the concept accent");
        Require(Descendants(form).OfType<PictureBox>().Any(p => p.Visible && p.Image is not null && p.Height >= 24 * scale), "Application logo is visible");
        Require(form.Icon is not null, "Window has application icon");
        var toggle = Descendants(form).OfType<Button>().Single(b => b.AccessibleName == "Показать или скрыть левую панель");
        toggle.PerformClick(); view.PerformClick(); Application.DoEvents();
        Require(import.Visible && export.Visible, "Import/export stay accessible with sidebar and preview hidden");
        foreach (var button in new[] { import, export, view }) CheckButton(button);
        Capture(form, $"native-hidden-{scale}");
        toggle.PerformClick(); view.PerformClick();
        form.ClientSize = new Size((int)(880 * scale), (int)(560 * scale));
        Application.DoEvents();
        foreach (var button in new[] { import, export, view }) CheckButton(button);
        Capture(form, $"native-small-{scale}");
        // Inspect the actual height dialog created by the production UI.
        var createDialog = form.GetType().GetMethod("CreateHeightDialog", BindingFlags.NonPublic | BindingFlags.Instance);
        Require(createDialog is not null, "Height tool exposes the actual dialog for UI inspection");
        using var dialog = (Form)createDialog!.Invoke(form, null)!;
        dialog.Show(form); Application.DoEvents();
        if (scale != 1) dialog.Scale(new SizeF(scale, scale));
        Application.DoEvents();
        var operation = Descendants(dialog).OfType<ComboBox>().Single();
        Descendants(dialog).OfType<NumericUpDown>().Single().Value = 65.635m;
        Require(operation.Items.Cast<object>().Any(x => x.ToString()!.Contains("Абсолютная отметка − Z")), "Absolute minus Z is selectable");
        Require(operation.Items.Cast<object>().Any(x => x.ToString()!.Contains("Абсолютная отметка + Z")), "Absolute plus Z is selectable");
        foreach (var button in Descendants(dialog).OfType<Button>()) CheckButton(button);
        Capture(dialog, $"native-height-{scale}");
        Console.WriteLine($"PASS native layout, logo, persistent actions and height dialog at {scale * 100}%");
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
    private static void Capture(Form form, string name)
    {
        Directory.CreateDirectory("test-artifacts");
        using var image = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
        image.Save(Path.Combine("test-artifacts", name + ".png"), ImageFormat.Png);
        if (name == "native-main-1" || name == "native-height-1")
        {
            using var bytes = new MemoryStream(); image.Save(bytes, ImageFormat.Png);
            Console.WriteLine("SCREENSHOT " + name + " " + Convert.ToBase64String(bytes.ToArray()));
        }
    }
}
