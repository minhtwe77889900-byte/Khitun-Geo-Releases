# Native preview: Windows verification pending

Launch a locally built executable with `--native-preview`. Default startup must still open the existing UI.

- [ ] Build the entire Windows project without errors.
- [ ] Empty table, Add point, invalid edit, negative height and cleared cells.
- [ ] Paste TSV from Excel, quoted CSV and malformed input; malformed input changes no points.
- [ ] Ctrl/Shift rows and columns; Delete clears only selected cells; Ctrl+Z restores them.
- [ ] Offset heights for every point; one undo restores all heights.
- [ ] Edit 10,000 points without per-keystroke table rebuilding.
- [ ] Resize to minimum, switch light/dark, resize splitter, hide/show preview.
- [ ] Hide preview and verify no paint loop, timer or background worker remains.
- [ ] Measure working set idle, with points, and after hiding preview.
- [ ] Confirm conversion controls remain disabled and no unavailable PDF actions are presented as working; DWG support is explicitly limited.
- [ ] X/Y exchange preserves height and can be undone; invalid active edit prevents exchange.
- [ ] Drop several text files; one malformed file means no partial import.
- [ ] CSV export includes the current committed edit and all rows; reimport restores exact values.
- [ ] Closing edited data offers CSV save; cancelled or failed save prevents closing.

This preview stores data only through explicit CSV export. It is not a release replacement.

Stage 12 drawing geometry checks (pending on Windows; supersedes stage 3 support limits):

- [ ] Real DWG/DXF polylines: bulge ± signs, closing segment, flat legacy POLYLINE; compare against CAD.
- [ ] Nested block base/position/rotation, reflection and nonuniform scale: circles become correctly oriented ellipses. Repeated block instances use common geometry.
- [ ] Points determine fit despite distant drawing background. Geometry is clipped to the point bounds plus 5% margin; crossing lines remain. One point and no-point overview work.
- [ ] Layers show initial off/frozen state, zero-layer inheritance and explicit child layers. Hide parent hides all children; show/hide all and cancel work. No file reread and no table changes.
- [ ] Open layers at minimum window size and both themes; Cyrillic names and scrollbars remain readable. Import/clear actions cannot apply stale dialog state to a replacement scene.
- [ ] Failed import retains prior scene and layers. Close during reading resumes the normal save flow after cleanup; no continuation accesses disposed controls.
- [ ] Hide visualization, change points/import, then show: no hidden painting loop and correct new view. Partial paint and splitter resize preserve clipping.
- [ ] Traversal/rendering work limit and curve-detail limit are displayed without recurring invalidation. Large repeated-block files keep the window usable; record actual paint time/responsiveness.
- [ ] After a limited full paint, invalidate a small cheap region: partial-display/detail warnings stay visible until a complete successful repaint. Verify both budget and detail flags.
- [ ] Measure complete-process RAM/load/paint on identical drawings against the existing UI. Portable managed-allocation/traversal results do not substitute for this check.

Stage 3 drawing checks (pending on Windows):

- [ ] Import supported DWG/DXF: lines, points, circles, XY arcs; check direction and absolute position against CAD.
- [ ] Unsupported entities and reader notices are shown explicitly; no claim of full drawing support.
- [ ] Failed import retains the previous drawing; source file bytes are unchanged.
- [ ] Table points stay untouched when loading or clearing a drawing.
- [ ] Repeated import and closing while reading cannot dispose controls underneath the continuation.
- [ ] Import while preview hidden; it performs no painting, then fits correctly when shown.
- [ ] Clearing drawing, theme switching and splitter resizing redraw correctly.

Stage 4 coordinate module checks:

- [ ] Verify full Windows startup accepts updated integrity hashes and loads coordinate-core.js locally/offline.
- [ ] Confirm existing conversion and negative-height preservation work; native conversion controls remain disabled.

Stage 6 native CRS selection checks (pending):

- [ ] Select builtin WGS-84 / GSK-2011 and regional MSK/SK-63 entries using autocomplete.
- [ ] Verify source/target default to WGS-84 and existing MSK-164 preset; lists are independent.
- [ ] Switch GK 3° / GK 6° / UTM: manual maximum becomes 120 / 60 / 60 and invalid values cannot remain.
- [ ] Manual zone remains available for source UTM even with auto target zone enabled.
- [ ] Prefix options only apply to GK systems; disabled options do not affect the request.
- [ ] Resize to minimum and switch themes; selector text, zone controls and table remain usable.
- [ ] Missing/corrupt catalogue leaves selectors unavailable with a visible error; no successful conversion is reported.
- [ ] Conversion remains disabled until the embedded runtime passes controls and the UI integration is checked.

Stage 10 export checks (pending on Windows):

- [ ] Open Export points after committing a grid edit; all filled rows are exported regardless of selection.
- [ ] With X=123.125 northing, Y=456.875 easting, Z=-7.25, AutoCAD outputs 456.875 / 123.125 / -7.250; geodetic XYZ outputs 123.125 / 456.875 / -7.250.
- [ ] PNEZD/PENZD and NEZ/ENZ use the chosen north/east order. Check resulting files in Civil 3D using the matching text format.
- [ ] B,L,H is available only for a geographic source; planar export is rejected for geographic coordinates.
- [ ] Check comma, semicolon, tab and space; 0–9 decimal places, optional headers, Cyrillic and negative heights.
- [ ] Confirm the notice explains the existing generic export convention for missing height (zero in the output only); Save CSV still preserves missing height and exact values.
- [ ] Invalid/incomplete coordinates disable saving; cancelled dialog or failed save leaves points and existing file unchanged.
- [ ] Export during conversion is unavailable; export must not mark a rounded file as an exact saved copy of the workspace.
- [ ] Resize the export dialog, switch themes in the parent, and check preview/options/buttons remain usable. Sidebar scroll exposes all actions at minimum window size.

Stage 11 passport/PDF checks (pending on Windows; supersedes earlier disabled-calculation checks):

- [ ] Successful native calculation enables Passport PDF; failed/cancelled/stale calculation leaves the previous passport intact.
- [ ] Original source/target names and resolved definitions, explicit zone/prefix settings, output zones, UTC time and point count match the actual operation.
- [ ] Missing heights and repeated numbers produce the same warnings as the existing UI; geographic bounds errors block conversion. No successful quality check is claimed on runtime failure.
- [ ] Undo restores previous passport and coordinate interpretation with the points, including identity conversion and two conversions/two undos.
- [ ] Editing/reordering points marks the last passport as referring to a previous result; undoing the edit restores correspondence.
- [ ] Russian text, actual system Arial fonts, page breaks, repeated quality headers, 500 retained warnings and total warning count remain readable.
- [ ] Cancel SaveFileDialog, cancel during creation, close during creation and try an unwritable/occupied destination; existing file remains intact, no temporary file remains.
- [ ] Close once during successful/cancelled/failed PDF creation: after worker cleanup, the window continues the usual close/save flow once. Cancelling the save prompt keeps the window usable; closing again works. If a drawing read also runs, close waits for both operations.
- [ ] Repeated saving works; font failure gives an error. No WebView2 or browser opens during native PDF creation.
- [ ] Passport button stays unavailable while calculating/exporting; separate Cancel PDF button remains responsive; parent survives completion/cancellation.
- [ ] Full Release SDK build and tools/verify-native-runtime.ps1 must pass on Windows before accepting this stage for production UI replacement.
