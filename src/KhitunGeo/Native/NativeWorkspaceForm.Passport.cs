using System.IO;

namespace KhitunGeo.Native;

internal sealed partial class NativeWorkspaceForm
{
    private readonly PassportState passportState = new();
    private readonly DeferredCloseRequest pendingClose = new();
    private readonly Button passportButton = new() { Text = "Паспорт PDF", Width = 130, Height = 40, FlatStyle = FlatStyle.Flat, Enabled = false };
    private readonly ToolStripButton cancelPdf = new() { Text = "Отменить PDF", Visible = false };
    private NativeQualityRuntime? qualityRuntime;
    private CancellationTokenSource? pdfCancellation;

    private async Task ExportPassportPdf()
    {
        if (pdfCancellation is not null || conversionCancellation is not null || !grid.EndEdit()) return;
        var passport = passportState.Current;
        if (passport is null) return;
        var matches = passport.Matches(workspace.Points);
        var summary = $"{passport.SourceName} → {passport.TargetName}\nТочек: {passport.Count}\n" +
            (matches ? "Таблица соответствует расчёту." : "Таблица изменена; паспорт относится к прежнему расчёту.") + "\n\nСохранить паспорт в PDF?";
        if (MessageBox.Show(this,summary,"Паспорт преобразования",MessageBoxButtons.OKCancel,MessageBoxIcon.Information) != DialogResult.OK) return;
        using var dialog = new SaveFileDialog { Filter = "Паспорт PDF|*.pdf", DefaultExt = "pdf", AddExtension = true, OverwritePrompt = true, FileName = "Khitun-Geo-passport.pdf" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var path = Path.GetFullPath(dialog.FileName);
        using var cancellation = new CancellationTokenSource();
        var outcome = "";
        try
        {
            pdfCancellation = cancellation; cancelPdf.Visible = true; WorkspaceChanged(this,EventArgs.Empty);
            var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            await Task.Run(() =>
            {
                cancellation.Token.ThrowIfCancellationRequested();
                PassportFontResolver.Configure(Path.Combine(fonts,"arial.ttf"),Path.Combine(fonts,"arialbd.ttf"));
                PassportPdfWriter.Save(path,passport,matches,cancellation.Token);
            }, cancellation.Token);
            outcome = "Паспорт PDF сохранён: " + path;
        }
        catch (OperationCanceledException) { outcome = "Экспорт PDF отменён."; }
        catch (Exception ex)
        {
            outcome = "PDF не сохранён.";
            if (!IsDisposed && !Disposing) MessageBox.Show(this,ex.Message,"Ошибка сохранения PDF",MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
        finally
        {
            pdfCancellation = null;
            if (!IsDisposed && !Disposing)
            { cancelPdf.Visible = false; WorkspaceChanged(this,EventArgs.Empty); status.Text = outcome; ResumeCloseAfterOperation(); }
        }
    }

    private void ResumeCloseAfterOperation()
    {
        if (IsDisposed || Disposing || pdfCancellation is not null || conversionCancellation is not null || importingDrawing) return;
        pendingClose.Complete(() => BeginInvoke((Action)Close));
    }
}
