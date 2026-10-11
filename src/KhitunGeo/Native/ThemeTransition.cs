using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace KhitunGeo.Native;

internal static class ThemeTransition
{
    private static readonly ConditionalWeakTable<Control, System.Windows.Forms.Timer> running = new();
    public static void Apply(Control root, bool dark)
    {
        if (running.TryGetValue(root, out var old)) { old.Dispose(); running.Remove(root); }
        var controls = Walk(root).ToArray();
        var colors = controls.SelectMany(ColorProperties).ToArray();
        var before = colors.Select(c => c.Read()).ToArray();
        NativeTheme.Apply(root, dark);
        var after = colors.Select(c => c.Read()).ToArray();
        for (var i = 0; i < colors.Length; i++) colors[i].Write(before[i]);
        var clock = Stopwatch.StartNew(); var timer = new System.Windows.Forms.Timer { Interval = 16 };
        running.Add(root, timer);
        timer.Tick += (_, _) =>
        {
            if (root.IsDisposed) { timer.Dispose(); running.Remove(root); return; }
            var t = Math.Min(1, clock.Elapsed.TotalMilliseconds / 320); t = t * t * (3 - 2 * t);
            for (var i = 0; i < colors.Length; i++)
                if (!colors[i].Owner.IsDisposed) colors[i].Write(t >= 1 || before[i].IsEmpty || after[i].IsEmpty ? after[i] : Blend(before[i], after[i], t));
            root.Invalidate(true);
            if (t >= 1) { timer.Dispose(); running.Remove(root); }
        };
        timer.Start();
    }
    private sealed record ColorProperty(Control Owner, Func<Color> Read, Action<Color> Write);
    private static IEnumerable<ColorProperty> ColorProperties(Control c)
    {
        yield return new(c, () => c.BackColor, v => c.BackColor = v);
        yield return new(c, () => c.ForeColor, v => c.ForeColor = v);
        if (c is not DataGridView grid) yield break;
        yield return new(c, () => grid.BackgroundColor, v => grid.BackgroundColor = v);
        yield return new(c, () => grid.GridColor, v => grid.GridColor = v);
        // Fetch styles at each tick: applying a theme replaces the row header style.
        foreach (var style in new Func<DataGridViewCellStyle>[] { () => grid.DefaultCellStyle, () => grid.AlternatingRowsDefaultCellStyle, () => grid.ColumnHeadersDefaultCellStyle, () => grid.RowHeadersDefaultCellStyle })
        {
            yield return new(c, () => style().BackColor, v => style().BackColor = v);
            yield return new(c, () => style().ForeColor, v => style().ForeColor = v);
            yield return new(c, () => style().SelectionBackColor, v => style().SelectionBackColor = v);
            yield return new(c, () => style().SelectionForeColor, v => style().SelectionForeColor = v);
        }
    }
    private static IEnumerable<Control> Walk(Control control) => new[] { control }.Concat(control.Controls.Cast<Control>().SelectMany(Walk));
    private static Color Blend(Color a, Color b, double t) => Color.FromArgb((int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
}
