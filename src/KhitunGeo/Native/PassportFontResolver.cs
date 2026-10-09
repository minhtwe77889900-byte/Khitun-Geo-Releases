using System.IO;
using PdfSharp.Fonts;

namespace KhitunGeo.Native;

internal sealed class PassportFontResolver : IFontResolver
{
    private static readonly object Sync = new();
    private static PassportFontResolver? configured;
    private readonly string regularPath, boldPath;
    private readonly byte[] regular, bold;

    private PassportFontResolver(string regularPath, string boldPath)
    {
        this.regularPath = regularPath; this.boldPath = boldPath;
        regular = File.ReadAllBytes(regularPath); bold = File.ReadAllBytes(boldPath);
    }

    public static void Configure(string regularFontPath, string boldFontPath)
    {
        var regular = Path.GetFullPath(regularFontPath); var bold = Path.GetFullPath(boldFontPath);
        lock (Sync)
        {
            if (configured is not null)
            {
                if (regular != configured.regularPath || bold != configured.boldPath)
                    throw new InvalidOperationException("Шрифты PDF уже настроены иначе. Перезапустите приложение.");
                return;
            }
            var resolver = new PassportFontResolver(regular, bold);
            GlobalFontSettings.FontResolver = resolver;
            configured = resolver;
        }
    }

    internal static void EnsureConfigured()
    { lock (Sync) if (configured is null) throw new InvalidOperationException("Не настроены шрифты PDF."); }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        if (familyName != "KhitunPassport" || isItalic) throw new InvalidOperationException("Неизвестный шрифт PDF.");
        return new FontResolverInfo(isBold ? "khitun-bold" : "khitun-regular");
    }

    public byte[] GetFont(string faceName) => faceName switch
    {
        "khitun-regular" => regular,
        "khitun-bold" => bold,
        _ => throw new InvalidOperationException("Неизвестное начертание PDF.")
    };
}
