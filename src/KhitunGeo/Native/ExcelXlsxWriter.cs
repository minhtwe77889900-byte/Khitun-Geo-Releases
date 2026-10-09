using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace KhitunGeo.Native;

internal static class ExcelXlsxWriter
{
    private const string SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelationshipsNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string PackageRelationshipsNs = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string ContentTypesNs = "http://schemas.openxmlformats.org/package/2006/content-types";

    public static void Save(string path, PointExportResult data, string coordinateSystem)
    {
        if (data.Columns.Length != 5 || data.Rows.Any(row => row.Length != 5))
            throw new ArgumentException("Некорректная таблица для книги Excel.");
        if (data.Rows.Count > 1_048_573)
            throw new ArgumentException("Excel поддерживает не более 1 048 573 точек на одном листе.");

        var fullPath = Path.GetFullPath(path);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false, Encoding.UTF8))
            {
                WriteContentTypes(archive);
                WritePackageRelationships(archive);
                WriteWorkbook(archive);
                WriteWorkbookRelationships(archive);
                WriteWorksheet(archive, data, coordinateSystem);
            }
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static void WriteContentTypes(ZipArchive archive) => WriteXml(archive, "[Content_Types].xml", xml =>
    {
        xml.WriteStartElement("Types", ContentTypesNs);
        xml.WriteStartElement("Default", ContentTypesNs); xml.WriteAttributeString("Extension", "rels"); xml.WriteAttributeString("ContentType", "application/vnd.openxmlformats-package.relationships+xml"); xml.WriteEndElement();
        xml.WriteStartElement("Default", ContentTypesNs); xml.WriteAttributeString("Extension", "xml"); xml.WriteAttributeString("ContentType", "application/xml"); xml.WriteEndElement();
        xml.WriteStartElement("Override", ContentTypesNs); xml.WriteAttributeString("PartName", "/xl/workbook.xml"); xml.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"); xml.WriteEndElement();
        xml.WriteStartElement("Override", ContentTypesNs); xml.WriteAttributeString("PartName", "/xl/worksheets/sheet1.xml"); xml.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"); xml.WriteEndElement();
        xml.WriteEndElement();
    });

    private static void WritePackageRelationships(ZipArchive archive) => WriteXml(archive, "_rels/.rels", xml =>
    {
        xml.WriteStartElement("Relationships", PackageRelationshipsNs);
        xml.WriteStartElement("Relationship", PackageRelationshipsNs);
        xml.WriteAttributeString("Id", "rId1");
        xml.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument");
        xml.WriteAttributeString("Target", "xl/workbook.xml");
        xml.WriteEndElement(); xml.WriteEndElement();
    });

    private static void WriteWorkbook(ZipArchive archive) => WriteXml(archive, "xl/workbook.xml", xml =>
    {
        xml.WriteStartElement("workbook", SpreadsheetNs);
        xml.WriteAttributeString("xmlns", "r", null, RelationshipsNs);
        xml.WriteStartElement("sheets", SpreadsheetNs);
        xml.WriteStartElement("sheet", SpreadsheetNs);
        xml.WriteAttributeString("name", "Точки"); xml.WriteAttributeString("sheetId", "1");
        xml.WriteAttributeString("r", "id", RelationshipsNs, "rId1");
        xml.WriteEndElement(); xml.WriteEndElement(); xml.WriteEndElement();
    });

    private static void WriteWorkbookRelationships(ZipArchive archive) => WriteXml(archive, "xl/_rels/workbook.xml.rels", xml =>
    {
        xml.WriteStartElement("Relationships", PackageRelationshipsNs);
        xml.WriteStartElement("Relationship", PackageRelationshipsNs);
        xml.WriteAttributeString("Id", "rId1");
        xml.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet");
        xml.WriteAttributeString("Target", "worksheets/sheet1.xml");
        xml.WriteEndElement(); xml.WriteEndElement();
    });

    private static void WriteWorksheet(ZipArchive archive, PointExportResult data, string coordinateSystem) => WriteXml(archive, "xl/worksheets/sheet1.xml", xml =>
    {
        var lastRow = 3 + data.Rows.Count;
        xml.WriteStartElement("worksheet", SpreadsheetNs);
        xml.WriteStartElement("dimension", SpreadsheetNs); xml.WriteAttributeString("ref", $"A1:E{lastRow}"); xml.WriteEndElement();
        xml.WriteStartElement("sheetViews", SpreadsheetNs);
        xml.WriteStartElement("sheetView", SpreadsheetNs); xml.WriteAttributeString("workbookViewId", "0"); xml.WriteEndElement(); xml.WriteEndElement();
        xml.WriteStartElement("sheetFormatPr", SpreadsheetNs); xml.WriteAttributeString("defaultRowHeight", "15"); xml.WriteEndElement();
        xml.WriteStartElement("cols", SpreadsheetNs);
        foreach (var (index, width) in new[] { (1, 16), (2, 18), (3, 18), (4, 14), (5, 36) })
        {
            xml.WriteStartElement("col", SpreadsheetNs); xml.WriteAttributeString("min", index.ToString(CultureInfo.InvariantCulture));
            xml.WriteAttributeString("max", index.ToString(CultureInfo.InvariantCulture)); xml.WriteAttributeString("width", width.ToString(CultureInfo.InvariantCulture));
            xml.WriteAttributeString("customWidth", "1"); xml.WriteEndElement();
        }
        xml.WriteEndElement();
        xml.WriteStartElement("sheetData", SpreadsheetNs);
        xml.WriteStartElement("row", SpreadsheetNs); xml.WriteAttributeString("r", "1");
        WriteTextCell(xml, "A1", "Текущая система координат"); WriteTextCell(xml, "B1", coordinateSystem); xml.WriteEndElement();
        xml.WriteStartElement("row", SpreadsheetNs); xml.WriteAttributeString("r", "3");
        for (var column = 0; column < data.Columns.Length; column++) WriteTextCell(xml, CellAddress(column + 1, 3), data.Columns[column]);
        xml.WriteEndElement();
        for (var rowIndex = 0; rowIndex < data.Rows.Count; rowIndex++)
        {
            var rowNumber = rowIndex + 4;
            var values = data.Rows[rowIndex];
            xml.WriteStartElement("row", SpreadsheetNs); xml.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
            WriteTextCell(xml, CellAddress(1, rowNumber), values[0]);
            WriteNumberCell(xml, CellAddress(2, rowNumber), values[1]);
            WriteNumberCell(xml, CellAddress(3, rowNumber), values[2]);
            WriteNumberCell(xml, CellAddress(4, rowNumber), values[3]);
            WriteTextCell(xml, CellAddress(5, rowNumber), values[4]);
            xml.WriteEndElement();
        }
        xml.WriteEndElement(); xml.WriteEndElement();
    });

    private static void WriteTextCell(XmlWriter xml, string address, string value)
    {
        xml.WriteStartElement("c", SpreadsheetNs); xml.WriteAttributeString("r", address); xml.WriteAttributeString("t", "inlineStr");
        xml.WriteStartElement("is", SpreadsheetNs); xml.WriteStartElement("t", SpreadsheetNs);
        if (value.Length != value.Trim().Length) xml.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
        xml.WriteString(value); xml.WriteEndElement(); xml.WriteEndElement(); xml.WriteEndElement();
    }

    private static void WriteNumberCell(XmlWriter xml, string address, string value)
    {
        if (value.Length == 0) return;
        xml.WriteStartElement("c", SpreadsheetNs); xml.WriteAttributeString("r", address);
        xml.WriteStartElement("v", SpreadsheetNs); xml.WriteString(value); xml.WriteEndElement(); xml.WriteEndElement();
    }

    private static string CellAddress(int column, int row)
    {
        var name = "";
        while (column > 0)
        {
            column--; name = (char)('A' + column % 26) + name; column /= 26;
        }
        return name + row.ToString(CultureInfo.InvariantCulture);
    }

    private static void WriteXml(ZipArchive archive, string path, Action<XmlWriter> write)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var xml = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false, CloseOutput = false });
        write(xml);
    }
}
