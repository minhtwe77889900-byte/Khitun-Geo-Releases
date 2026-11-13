using System.Globalization;

namespace KhitunGeo.Native;

internal sealed class PointGrid : DataGridView
{
    private PointWorkspace? workspace;
    private int columnAnchor;
    private bool selectionUpdatePending;
    public event Action<IReadOnlySet<int>>? PointSelectionChanged;

    public PointGrid()
    {
        VirtualMode = true;
        AllowUserToAddRows = false;
        AllowUserToDeleteRows = false;
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        SelectionMode = DataGridViewSelectionMode.CellSelect;
        MultiSelect = true;
        Dock = DockStyle.Fill;
        RowHeadersVisible = false;
        BackgroundColor = Color.White;
        BorderStyle = BorderStyle.None;
        DoubleBuffered = true;
        foreach (var (name, width) in new[] { ("№ точки", 90), ("X", 130), ("Y", 130), ("Z", 90), ("Описание", 180) })
            Columns.Add(new DataGridViewTextBoxColumn { HeaderText = name, Width = width, FillWeight = width, MinimumWidth = name is "X" or "Y" ? 105 : name == "Описание" ? 110 : 64, SortMode = DataGridViewColumnSortMode.NotSortable });
        CellValueNeeded += (_, e) =>
        {
            if (workspace is null || e.RowIndex >= workspace.Points.Count) return;
            var p = workspace.Points[e.RowIndex];
            e.Value = e.ColumnIndex switch { 0 => p.Name, 1 => p.X, 2 => p.Y, 3 => p.Height, _ => p.Description };
        };
        CellValidating += (_, e) =>
        {
            if (e.ColumnIndex is < 1 or > 3) return;
            try { TabularPaste.Number(Convert.ToString(e.FormattedValue, CultureInfo.CurrentCulture) ?? ""); }
            catch (FormatException) { e.Cancel = true; Rows[e.RowIndex].ErrorText = "Введите число или оставьте поле пустым."; }
        };
        CellValuePushed += (_, e) =>
        {
            if (workspace is null) return;
            var text = Convert.ToString(e.Value, CultureInfo.CurrentCulture) ?? "";
            var p = workspace.Points[e.RowIndex];
            var next = e.ColumnIndex switch
            {
                0 => p with { Name = text }, 1 => p with { X = TabularPaste.Number(text) },
                2 => p with { Y = TabularPaste.Number(text) }, 3 => p with { Height = TabularPaste.Number(text) },
                _ => p with { Description = text }
            };
            workspace.SetPoint(e.RowIndex, next);
            Rows[e.RowIndex].ErrorText = "";
        };
        ColumnHeaderMouseClick += (_, e) =>
        {
            if (e.ColumnIndex < 0) return;
            var shift = (ModifierKeys & Keys.Shift) != 0;
            var control = (ModifierKeys & Keys.Control) != 0;
            if (!control) ClearSelection();
            var start = shift ? Math.Min(columnAnchor, e.ColumnIndex) : e.ColumnIndex;
            var end = shift ? Math.Max(columnAnchor, e.ColumnIndex) : e.ColumnIndex;
            for (var c = start; c <= end; c++)
            {
                var allSelected = RowCount > 0 && Enumerable.Range(0, RowCount).All(r => Rows[r].Cells[c].Selected);
                var select = !control || !allSelected;
                for (var r = 0; r < RowCount; r++) Rows[r].Cells[c].Selected = select;
            }
            if (!shift) columnAnchor = e.ColumnIndex;
        };
    }

    private HashSet<int> SelectedPointIndices() =>
        SelectedCells.Cast<DataGridViewCell>().Select(cell => cell.RowIndex).ToHashSet();

    protected override void OnSelectionChanged(EventArgs e)
    {
        base.OnSelectionChanged(e);
        if (selectionUpdatePending || !IsHandleCreated || IsDisposed || Disposing) return;
        selectionUpdatePending = true;
        BeginInvoke((Action)(() =>
        {
            selectionUpdatePending = false;
            if (!IsDisposed) PointSelectionChanged?.Invoke(SelectedPointIndices());
        }));
    }

    public void SelectPoint(int index)
    {
        if (index < 0 || index >= RowCount || !EndEdit()) return;
        CurrentCell = Rows[index].Cells[0];
        ClearSelection();
        foreach (DataGridViewCell cell in Rows[index].Cells) cell.Selected = true;
    }

    public void DeleteSelectedPoints()
    {
        if (workspace is null || !EndEdit()) return;
        var selected = SelectedPointIndices();
        if (selected.Count > 0)
            workspace.ReplacePoints(workspace.Points.Where((_, index) => !selected.Contains(index)).ToArray());
    }

    public void DeleteAllPoints()
    {
        if (workspace is not null && EndEdit()) workspace.ReplacePoints(Array.Empty<SurveyPoint>());
    }

    public void Bind(PointWorkspace value)
    {
        if (workspace is not null) workspace.Changed -= WorkspaceChanged;
        workspace = value;
        workspace.Changed += WorkspaceChanged;
        WorkspaceChanged(this, EventArgs.Empty);
    }

    private void WorkspaceChanged(object? sender, EventArgs e)
    {
        if (workspace is null) return;
        if (RowCount != workspace.Points.Count) RowCount = workspace.Points.Count;
        Invalidate();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (workspace is null) return base.ProcessCmdKey(ref msg, keyData);
        if (keyData == (Keys.Control | Keys.V))
        {
            if (IsCurrentCellInEditMode && !EndEdit()) return true;
            try
            {
                var clipboard = Clipboard.GetText();
                var rows = TabularPaste.ParseCells(clipboard);
                if (rows.Length == 0) return true;
                var columns = rows.Length == 0 ? 0 : rows.Max(row => row.Length);
                if (columns >= 3)
                {
                    var imported = TabularPaste.Parse(clipboard);
                    if (imported.Count == 0) return true;
                    workspace.ReplacePoints(workspace.Points.Concat(imported).ToArray());
                }
                else
                {
                    if (CurrentCell is null) throw new FormatException("Для вставки столбца выберите начальную ячейку таблицы.");
                    workspace.ReplacePoints(TabularPaste.ApplyCells(workspace.Points, rows, CurrentCell.RowIndex, CurrentCell.ColumnIndex));
                }
            }
            catch (FormatException ex) { MessageBox.Show(this, ex.Message, "Вставка данных"); }
            return true;
        }
        if (IsCurrentCellInEditMode) return base.ProcessCmdKey(ref msg, keyData);
        if (keyData == (Keys.Control | Keys.Z)) { workspace.Undo(); return true; }
        if (keyData is Keys.Delete or Keys.Back)
        {
            DeleteSelectedPoints();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && workspace is not null) workspace.Changed -= WorkspaceChanged;
        base.Dispose(disposing);
    }
}
