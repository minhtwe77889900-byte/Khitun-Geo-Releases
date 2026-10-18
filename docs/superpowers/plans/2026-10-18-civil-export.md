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

Validation evidence:
- Local NativeExport tests failed on all four Civil BOM/header cases and AutoCAD BOM before the implementation, then passed after it.
- An additional local check passed every row of the supplied 304-point data through production PNEZD/PENZD/ENZ/AutoCAD exports; coordinates and heights were preserved. User files were not added to the repository.
- Windows build/test candidate: 02a2c3191f1e77873b35ec32d712d733752da496, Actions run 39116703321. All test stages and installer packaging passed. Verified installer artifact: 12133080314.
- Independent code review found no critical or important issues. The captured native Civil export dialog has the disabled header and readable import instructions.
- Actual Civil 3D import remains a user-machine validation; the original 303/304 difference was not reproduced as an export defect.
