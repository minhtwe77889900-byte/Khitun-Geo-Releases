namespace KhitunGeo.Native;

internal static class TrainingExportGuide
{
    private static readonly Dictionary<PointExportFormat, string> formats = new()
    {
        [PointExportFormat.AutoCad] = "AutoCAD XYZ — таблица координат для САПР. Сначала восток (X в AutoCAD), затем север (Y) и высота Z.",
        [PointExportFormat.AutoCadScript] = "AutoCAD SCR — команды создания объектов POINT в мировой системе чертежа. Запустите SCRIPT и выберите файл .scr.",
        [PointExportFormat.ExcelXlsx] = "Excel XLSX — книга с номером точки, X, Y, высотой, описанием и названием СК. Отсутствующая высота остаётся пустой.",
        [PointExportFormat.Pnezd] = "Civil 3D PNEZD — номер, север, восток, высота, описание.",
        [PointExportFormat.Penzd] = "Civil 3D PENZD — номер, восток, север, высота, описание.",
        [PointExportFormat.Nez] = "Civil 3D NEZ — север, восток, высота. Номер и описание опускаются.",
        [PointExportFormat.Enz] = "Civil 3D ENZ — восток, север, высота. Номер и описание опускаются.",
        [PointExportFormat.Xyz] = "Геодезические XYZ — X из таблицы (север), Y (восток), Z (высота).",
        [PointExportFormat.Blh] = "Географические BLH — широта B и долгота L в десятичных градусах, затем высота H. Используется после выбора географической СК."
    };

    private static readonly Dictionary<string, string> delimiters = new()
    {
        [","] = "Запятая — разделяет поля CSV: 101,5520000,432000. Десятичный знак в числах — точка.",
        [";"] = "Точка с запятой — вариант CSV: 101;5520000;432000. При чтении выберите тот же разделитель.",
        ["\t"] = "Табуляция — один символ Tab между полями TXT. При вставке в электронную таблицу значения попадают в разные столбцы.",
        [" "] = "Пробел — разделяет поля TXT: 101 5520000 432000. Описания из нескольких слов могут быть разбиты при импорте."
    };

    public static string Describe(PointExportOptions options)
    {
        bool fixedFormat = options.Format is PointExportFormat.ExcelXlsx or PointExportFormat.AutoCadScript;
        var sections = new List<string>
        {
            "Ваш файл", formats[options.Format],
            fixedFormat ? "Структура этого файла задана форматом. Выбирать разделитель не требуется." : delimiters[options.Delimiter]
        };
        if (PointExport.IsCivilFormat(options.Format))
            sections.Add("В Civil 3D откройте импорт точек и укажите тот же порядок полей и разделитель. Заголовок в файл не записывается.");
        sections.Add(""); sections.Add("Другие форматы");
        sections.AddRange(formats.Where(pair => pair.Key != options.Format).Select(pair => pair.Value));
        sections.Add(""); sections.Add("Разделители текстовых файлов");
        sections.AddRange(delimiters.Where(pair => fixedFormat || pair.Key != options.Delimiter).Select(pair => pair.Value));
        return string.Join(Environment.NewLine + Environment.NewLine, sections);
    }
}
