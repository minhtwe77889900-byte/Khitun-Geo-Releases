# Khitun Geo

Windows x64 geodesy application. Source version 1.7.9.

The application starts with its native WinForms interface. It includes
point editing/import/export, the unchanged coordinate mathematics hosted in Jint,
direct PDF passports, shared nested drawing blocks, curved polylines and layers.
Native drawing fits the point region and culls distant geometry. WebView2 is not
used or required. Points are saved only when the user exports or saves CSV;
there are no projects or autosave sessions.
See `docs/superpowers/plans/2026-10-08-native-progress.md` and
`tests/NativeWorkspace/WINDOWS_SMOKE.md` for verified scope and remaining checks.

## Coordinate systems

Offline WGS 84, GSK-2011, PZ-90, PZ-90.02, PZ-90.11, SK-42, SK-95,
Gauss-Kruger, UTM, Web Mercator, regional MSK and SK-63 transformations.
Regional systems have independent source and destination region/zone
selectors, a search field, and persistent project settings.

The reference catalogue contains 262 MSK definitions and 103 SK-63 zones,
including Krasnoyarsk city and MSK-163 through MSK-170. Definitions remain
reference data and must be checked against official surveying control points.
See `COORDINATE_SYSTEMS.md` for sources, validation and compatibility limits.

The compact left sidebar contains project, import/export, edit, zone, service,
settings and help commands. Icons remain visible when collapsed; the expanded
sidebar can overlay the workspace or be pinned. Its state is remembered.
The former native menu row and redundant navigation tabs have been removed.
The plot fills available space without a legend or selected-point range card.

Only Light and Dark themes remain; the sidebar button switches them in one click.
Help contains the current source/destination coordinate system details.
The Zone settings dialog provides automatic/manual mode, zone number and
source/destination prefixes; changes take effect only after Apply.
Select all switches to Clear selection when every point is selected.
Ctrl+Z undoes point edits, additions, deletions, imports, transformations and
zone settings (up to 30 actions in the current project). Text fields outside
the point table retain their normal text undo behavior. Ctrl+Y or Ctrl+Shift+Z
repeats an undone action. New edits invalidate redo.

Drag a file into the app to import it. Recognized labeled text columns can be
imported directly; ambiguous formats open the mapping wizard. Existing points
require an explicit append/replace choice. Invalid rows abort the import without
changing existing points. JSON projects open separately and preserve CRS settings.
The plot's Add Point mode opens a confirmation dialog on click, including Z.

Middle-button dragging pans the point plot without browser autoscroll or
table selection. Cursor X/Y appear in a fixed overlay at the bottom right.
The point-label menu includes height Z, depth, and number + depth. Depth is
reference level minus Z (metres, same height system); missing values show a
dash. The reference level is saved per project and retained after horizontal
coordinate conversion, which preserves the entered Z. Labels do
not modify coordinates or exports.

Row headers support drag selection with table autoscroll, Shift ranges,
Ctrl/Command additive selection and range removal. With a row header focused,
Shift+Up/Down extends the selection, Ctrl+A selects all and Escape clears it.
Clicking an input keeps its editing caret instead of rebuilding the table.
Column headers support drag selection, Shift ranges and Ctrl toggles, independently
of row selection. The table has one point-number column. Ctrl+F finds a point.
The Change Z dialog assigns a value, adds/subtracts an offset or reverses the sign of selected
or all heights. Every affected Z must be numeric; validation is atomic and
Ctrl+Z restores the entire operation. Negative heights, decimal commas and
the Unicode minus sign are accepted. A lone minus is allowed while typing
but must be completed before calculations or export.

Coordinate-system conversion changes X/Y and preserves the entered Z text
exactly, including an empty value. Khitun Geo does not silently reinterpret a
survey elevation as an ellipsoidal height. Use Change Z for an explicit height
offset; geoid and vertical-datum conversion is not implemented yet.

## Build

GitHub Actions builds the application and Inno Setup installer on pushes to
`main`, pull requests, and manual runs. Open Actions > Build Windows to find
`KhitunGeo-Setup-x64` and `KhitunGeo-App-x64` artifacts after a successful run.
Regression checks run before compilation. Successful pushes
publish the new v1.7.9 installer and its SHA256 in GitHub Releases; existing
release assets are not overwritten by repeat builds.
The application uses the native Windows Forms interface and includes .NET 8;
Microsoft Edge WebView2 Runtime is not required. The installer is unsigned.

See `README_SAFE.txt` for Russian usage instructions and
`Khitun_Geo_Audit_RU.txt` for the original validation limits. `SHA256SUMS.txt`
covers the reviewed source and runtime coordinate definition files.

## Offline numerical checks

Run `node tests/coordinate-systems.cjs` and
`node tests/CoordinateCore/regional-control.cjs`. Reference controls are
generated independently with PROJ; public GeoProj responses are cached as test
fixtures. Normal tests make no network requests.
For graphical smoke tests: install Playwright 1.62.1 and its Chromium browser,
then run `node tests/ui-smoke.cjs`. GitHub Actions performs these steps on Windows.

`tools/check-geoproj.py` is a manual diagnostic tool; it requires pyproj and
must not run in CI. Synthetic coordinates are used for service comparisons.

## Windows acceptance check

1. Install on Windows x64 build 19041 or newer.
2. Launch and confirm integrity status in About.
3. Import a copy of the known MSK-164 control points.
4. Export ENZ with TAB separators and compare in Civil 3D.
5. Save, close, reopen the project and confirm coordinates and CRS.
6. Check uninstall preserves user projects.
7. Drag the plot with the middle button and release outside it: the application
   must not scroll, select a table row, or keep dragging. Check X/Y changes do
   not resize the toolbar, then save/reopen a project with depth labels.
8. Drag across row headers to select a range, including beyond the visible
   table edge. Check Ctrl/Shift selection, release outside the window and
   keyboard selection. Edit Z to a negative value, change selected heights
   with each operation, undo, save/reopen and export the result.

Round-trip numerical tests do not establish absolute geodetic accuracy.
Independent control points are required before production surveying use.
