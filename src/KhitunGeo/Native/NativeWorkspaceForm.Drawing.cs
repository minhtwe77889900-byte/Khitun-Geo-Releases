namespace KhitunGeo.Native;

internal sealed partial class NativeWorkspaceForm
{
    private IReadOnlySet<string> drawingLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private void ClearDrawing()
    {
        if (importingDrawing) return;
        drawing = null; drawingLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        preview.SetDrawing(null); WorkspaceChanged(this, EventArgs.Empty);
    }
    private void ShowDrawingLayers()
    {
        if (importingDrawing || drawing is null) return;
        var scene = drawing;
        using var dialog = new DrawingLayersDialog(scene, drawingLayers, dark);
        if (dialog.ShowDialog(this) != DialogResult.OK || !ReferenceEquals(scene, drawing) || IsDisposed || Disposing) return;
        drawingLayers = dialog.VisibleLayers; preview.SetVisibleLayers(drawingLayers); RefreshStatus();
    }
    private async Task ImportDrawing()
    {
        if (importingDrawing) return;
        using var dialog = new OpenFileDialog { Filter = "Чертежи|*.dwg;*.dxf", Multiselect = false };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        importingDrawing = true; WorkspaceChanged(this, EventArgs.Empty);
        try
        {
            var loaded = await Task.Run(() => NativeDrawingReader.Read(dialog.FileName));
            if (IsDisposed || Disposing) return;
            drawing = loaded;
            drawingLayers = loaded.Layers.Where(l => l.InitiallyVisible).Select(l => l.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            preview.SetDrawing(loaded);
            if (loaded.SkippedEntities > 0 || loaded.ReaderNotices > 0)
                MessageBox.Show(this, $"Сохранено элементов геометрии и вставок: {loaded.StoredElementCount}. Пропущено неподдерживаемых: {loaded.SkippedEntities}. Сообщений чтения: {loaded.ReaderNotices}.\nПоддерживаются линии, точки, плоские окружности, дуги, полилинии и вложенные блоки. Пространственные объекты, тексты, штриховки, внешние ссылки и массивы вставок не отображаются.", "Импорт чертежа");
        }
        catch (Exception ex) { if (!IsDisposed && !Disposing) MessageBox.Show(this, $"Не удалось прочитать чертёж: {ex.Message}", "Импорт чертежа"); }
        finally
        {
            importingDrawing = false;
            if (!IsDisposed && !Disposing) { WorkspaceChanged(this, EventArgs.Empty); ResumeCloseAfterOperation(); }
        }
    }
}
