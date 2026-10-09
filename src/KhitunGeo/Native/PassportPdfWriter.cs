using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace KhitunGeo.Native;

internal static class PassportPdfWriter
{
    public static void Save(string path, ConversionPassport passport, bool matchesCurrentTable, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PassportFontResolver.EnsureConfigured();
        var fullPath = Path.GetFullPath(path);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var document = new PdfDocument())
            {
                document.Info.Title = "Khitun Geo - паспорт преобразования";
                document.Info.Creator = "Khitun Geo " + passport.AppVersion;
                document.RenderEvents.RenderTextEvent += (_, e) =>
                {
                    if (e.CodePointGlyphIndexPairs.Any(p => p.GlyphIndex == 0))
                        throw new InvalidOperationException("В выбранном шрифте PDF нет символа документа. Файл не заменён.");
                };
                using (var layout = new Layout(document, cancellationToken))
                {
                    layout.Paragraph("Khitun Geo - паспорт преобразования", true);
                    layout.Paragraph("Запись о выполненном расчёте. Не является подтверждением геодезической точности или сертификатом.");
                    layout.Field("Версия приложения", passport.AppVersion);
                    layout.Field("Начало расчёта (UTC)", passport.StartedAt.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture));
                    layout.Field("Исходная СК", passport.SourceName);
                    layout.Field("Выходная СК", passport.TargetName);
                    layout.Field("Число преобразованных точек", passport.Count.ToString(CultureInfo.InvariantCulture));
                    layout.Field("Выбранные выходные зоны", passport.Zones.Count == 0 ? "Не применяются" : string.Join(", ", passport.Zones));
                    layout.Field("Высоты Z", "Сохранены без изменения. Пересчёт вертикальной системы и модели геоида не выполнялся.");
                    layout.Field("Формат хеширования", ConversionPassport.HashFormat);
                    layout.Field("SHA-256 исходных точек", passport.BeforeHash);
                    layout.Field("SHA-256 результата", passport.AfterHash);
                    layout.Field("Состояние таблицы на момент экспорта", matchesCurrentTable ? "Таблица соответствует результату расчёта." : "Таблица изменена после расчёта; паспорт относится к ранее выполненному преобразованию.");
                    layout.Field("Параметры СК и настройки расчёта", passport.Settings);
                    layout.Field("Контроль исходных данных", $"Всего замечаний: {passport.IssueCount}. В документе: {passport.Issues.Count} (не более 500).");
                    if (passport.IssueCount == 0) layout.Paragraph("Замечаний базовой проверки нет.");
                    else layout.IssueTable(passport.Issues);
                }
                for (var i = 0; i < document.PageCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var graphics = XGraphics.FromPdfPage(document.Pages[i]);
                    graphics.DrawString($"{i + 1} / {document.PageCount}", Font(9,false), XBrushes.Gray,
                        new XRect(0, document.Pages[i].Height.Point - 30, document.Pages[i].Width.Point, 15), XStringFormats.TopCenter);
                }
                cancellationToken.ThrowIfCancellationRequested();
                document.Save(temporary);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static XFont Font(double size, bool bold) => new("KhitunPassport", size,
        bold ? XFontStyleEx.Bold : XFontStyleEx.Regular, new XPdfFontOptions(PdfFontEncoding.Unicode, PdfFontEmbedding.TryComputeSubset));

    private sealed class Layout : IDisposable
    {
        private readonly PdfDocument document;
        private readonly CancellationToken cancellation;
        private readonly XFont normal = Font(10,false), bold = Font(11,true);
        private readonly double margin = XUnit.FromMillimeter(15).Point;
        private XGraphics graphics = null!;
        private PdfPage page = null!;
        private double y;
        private double Width => page.Width.Point - 2 * margin;

        public Layout(PdfDocument document, CancellationToken cancellation)
        { this.document=document; this.cancellation=cancellation; NewPage(); }

        private void NewPage()
        {
            cancellation.ThrowIfCancellationRequested(); graphics?.Dispose();
            page = document.AddPage(); page.Size = PageSize.A4;
            graphics = XGraphics.FromPdfPage(page); y = margin;
        }

        private bool EnsureLine(double height = 16)
        { cancellation.ThrowIfCancellationRequested(); if (y + height <= page.Height.Point - margin) return false; NewPage(); return true; }

        public void Field(string label, string value) { Paragraph(label, true); Paragraph(value); }

        public void Paragraph(string text, bool strong = false)
        {
            var font = strong ? bold : normal;
            foreach (var line in Wrap(text, font, Width))
            { EnsureLine(); graphics.DrawString(line, font, XBrushes.Black,new XRect(margin,y,Width,16),XStringFormats.TopLeft); y += 16; }
            y += 5;
        }

        public void IssueTable(IReadOnlyList<QualityIssue> issues)
        {
            void Header()
            {
                graphics.DrawString("Строка",bold,XBrushes.Black,new XRect(margin,y,60,16),XStringFormats.TopLeft);
                graphics.DrawString("Предупреждение",bold,XBrushes.Black,new XRect(margin+65,y,Width-65,16),XStringFormats.TopLeft);
                y += 20;
            }
            EnsureLine(40); Header();
            foreach (var issue in issues)
            {
                var first = true;
                foreach (var line in Wrap(issue.Message,normal,Width-65))
                {
                    if (EnsureLine()) Header();
                    if (first) graphics.DrawString((issue.Row+1).ToString(CultureInfo.InvariantCulture),normal,XBrushes.Black,new XRect(margin,y,60,16),XStringFormats.TopLeft);
                    graphics.DrawString(line,normal,XBrushes.Black,new XRect(margin+65,y,Width-65,16),XStringFormats.TopLeft);
                    first = false; y += 16;
                }
                y += 5;
            }
        }

        private IEnumerable<string> Wrap(string text, XFont font, double width)
        {
            foreach (var paragraph in text.Replace("\r\n","\n").Replace('\r','\n').Split('\n'))
            {
                var current = new StringBuilder();
                foreach (var word in paragraph.Replace('\t',' ').Split(' '))
                {
                    cancellation.ThrowIfCancellationRequested();
                    var combined = current.Length == 0 ? word : current + " " + word;
                    if (graphics.MeasureString(combined,font).Width <= width) { current.Clear().Append(combined); continue; }
                    if (current.Length > 0) { yield return current.ToString(); current.Clear(); }
                    var elements = StringInfo.GetTextElementEnumerator(word);
                    while (elements.MoveNext())
                    {
                        var element = elements.GetTextElement();
                        if (graphics.MeasureString(current.ToString()+element,font).Width > width && current.Length > 0)
                        { yield return current.ToString(); current.Clear(); }
                        current.Append(element);
                    }
                }
                yield return current.ToString();
            }
        }

        public void Dispose() => graphics.Dispose();
    }
}
