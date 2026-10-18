# Civil 3D export compatibility implementation plan

Goal: numeric point files must start with the first point, preserve all rows, and match the selected axis order.
Scope: Windows native export only; no coordinate math or UI animation changes.

- Add byte-level NativeExport regressions for PNEZD/PENZD/NEZ/ENZ/AutoCAD: no UTF-8 BOM, distinct axes, 304 rows including first/last, empty descriptions.
- Run Windows CI and observe the BOM failure before changing production code.
- Save CAD/Civil numeric files as UTF-8 without BOM. Preserve other export behavior.
- Disable headers for Civil profiles at UI and serializer level; make the matching Civil import format visible. Test switching from a header-enabled generic profile.
- Verify actual UI profile selection and all rows; do not change already-correct axis mapping without reproduction.
- Run the complete Windows build/test pipeline and review changes.
- Bump to 1.7.18; publish exactly the verified installer as a stable release with checksum and accurate limitations (Civil 3D itself not available here).
