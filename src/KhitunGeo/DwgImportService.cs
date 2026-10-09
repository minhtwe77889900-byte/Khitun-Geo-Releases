using System.Text;
using ACadSharp;
using ACadSharp.IO;

namespace KhitunGeo;

internal sealed record DwgConversion(string Dxf, int Notices);

internal static class DwgImportService
{
    public const int MaxInputBytes = 16 * 1024 * 1024;
    private const int MaxOutputBytes = 32 * 1024 * 1024;

    public static DwgConversion ConvertToDxf(byte[] bytes)
    {
        if (bytes.Length < 6 || bytes.Length > MaxInputBytes)
            throw new InvalidDataException("Пустой DWG или размер больше 16 МБ. Разделите чертёж.");
        var signature = Encoding.ASCII.GetString(bytes, 0, 6);
        if (signature is not ("AC1014" or "AC1015" or "AC1018" or "AC1021" or "AC1024" or "AC1027" or "AC1032"))
            throw new InvalidDataException("Эта версия DWG не поддерживается. Сохраните чертёж в формате DWG 2018 или ASCII DXF.");
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var notices = 0;
        using var source = new MemoryStream(bytes, writable: false);
        var document = DwgReader.Read(source, (_, _) => notices++);
        // This conversion is in memory only. The user's file is never overwritten.
        // Modern DXF guarantees UTF-8 layer names, including old Cyrillic DWGs.
        document.Header.Version = ACadVersion.AC1032;
        using var output = new BoundedMemoryStream(MaxOutputBytes);
        DxfWriter.Write(output, document, false, notification: (_, _) => notices++);
        return new DwgConversion(Encoding.UTF8.GetString(output.ToArray()), notices);
    }

    private sealed class BoundedMemoryStream(long limit) : MemoryStream
    {
        private void CheckSize(int count)
        {
            if (Position + count > limit) throw new InvalidDataException("Результат чтения DWG больше 32 МБ. Разделите чертёж.");
        }
        public override void Write(byte[] buffer, int offset, int count) { CheckSize(count); base.Write(buffer, offset, count); }
        public override void Write(ReadOnlySpan<byte> buffer) { CheckSize(buffer.Length); base.Write(buffer); }
        public override void WriteByte(byte value) { CheckSize(1); base.WriteByte(value); }
    }
}
