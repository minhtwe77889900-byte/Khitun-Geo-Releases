"""Inspect actual production PDF output with an independent parser."""
import sys
from pathlib import Path
from pypdf import PdfReader

directory = Path(sys.argv[1])
for name in ("short", "long", "many"):
    reader = PdfReader(directory / (name + ".pdf"))
    text = "\n".join(page.extract_text() for page in reader.pages)
    assert "паспорт преобразования" in text, name
    assert "МСК-164" in text, name
    assert "479F642610254F147B10973F560256ED0C6F658AC67EEB853D6C01CDBE762DD6" in text.replace("\n", ""), name
    for page in reader.pages:
        assert abs(float(page.mediabox.width) - 595.276) < 1
        assert abs(float(page.mediabox.height) - 841.89) < 1
        fonts = page["/Resources"]["/Font"].get_object()
        for font in fonts.values():
            font = font.get_object()
            if "/DescendantFonts" in font:
                font = font["/DescendantFonts"][0].get_object()
            descriptor = font["/FontDescriptor"].get_object()
            assert any(key in descriptor for key in ("/FontFile", "/FontFile2", "/FontFile3")), name
    if name == "short":
        assert len(reader.pages) == 1
        assert "Таблица соответствует результату расчёта" in text
    if name == "long":
        assert len(reader.pages) > 1
        assert text.count("Ж") == 1500
        assert "Таблица изменена после расчёта" in text
    if name == "many":
        assert len(reader.pages) > 2
        assert "Всего замечаний: 700" in text
        assert "В документе: 500" in text
        for i in range(500):
            assert f"Замечание {i:04d}:" in text, i
        assert "Замечание 0500:" not in text
print("PASS independently parsed PDFs: Cyrillic, content, hashes, fonts, A4 and all 500 retained issues")
