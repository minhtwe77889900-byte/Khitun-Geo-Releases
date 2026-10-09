using System.IO;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using System.Text;

namespace KhitunGeo.Native;

internal static class NativeDrawingReader
{
    public static DrawingScene Read(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length < 6 || stream.Length > 16 * 1024 * 1024)
            throw new InvalidDataException("Пустой чертёж или размер больше 16 МБ. Разделите чертёж.");
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var notices = 0;
        var extension = Path.GetExtension(path).ToLowerInvariant();
        CadDocument document;
        if (extension == ".dwg")
        {
            var signature = new byte[6];
            stream.ReadExactly(signature);
            if (Encoding.ASCII.GetString(signature) is not ("AC1014" or "AC1015" or "AC1018" or "AC1021" or "AC1024" or "AC1027" or "AC1032"))
                throw new InvalidDataException("Версия DWG не поддерживается. Сохраните в DWG 2018 или DXF.");
            stream.Position = 0;
            document = DwgReader.Read(stream, (_, _) => notices++);
        }
        else if (extension == ".dxf") document = DxfReader.Read(stream, (_, _) => notices++);
        else throw new InvalidDataException("Выберите DWG или DXF.");

        return DrawingSceneBuilder.Build(document, notices);
    }

}
