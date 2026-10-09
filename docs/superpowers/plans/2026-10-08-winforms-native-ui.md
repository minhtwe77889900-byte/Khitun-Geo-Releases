# WinForms Native UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prepare a native Windows workspace without changing coordinate mathematics or replacing the existing production screen prematurely.

**Architecture:** Introduce a standalone native workspace behind an explicit preview switch. Separate point data and undo commands from controls. Keep the existing MainForm as the default until native feature parity and Windows verification are complete.

**Tech Stack:** C#, .NET 8, WinForms, DataGridView, System.Drawing; no new runtime dependencies in this stage.

**Spec:** `docs/superpowers/specs/2026-10-08-winforms-native-ui-design.md`

## Global Constraints

- Offline operation; x64 Windows.
- Do not change coordinate formulas or CRS parameters.
- No GitHub upload, installer release, or removal of WebView2 in this stage.
- Height operations apply to all points and support undo.
- Hidden visualization performs no rendering, timers, or background work.
- Validate table values when editing finishes, not on each character.
- Keep the existing import/export workflows working until replacements are verified.

## Review Focus

- Invalid or incomplete numeric edits must not overwrite valid coordinates.
- Empty tables and an empty undo stack must be safe.
- Multi-cell deletion must preserve the schema and be reversible.
- A hidden preview must not schedule drawing or retain temporary drawing buffers.
- Small windows and theme changes must not hide conversion controls or lose table edits.

---

### Task 1: Point State and Undo

**Files:** Create `src/KhitunGeo/Native/SurveyPoint.cs`, `src/KhitunGeo/Native/PointWorkspace.cs`, `tests/NativeWorkspace/NativeWorkspace.csproj`, `tests/NativeWorkspace/Program.cs`.

**Interfaces:** `SurveyPoint(string Name, double? X, double? Y, double? Height, string Description)`; `PointWorkspace.Points` as `IReadOnlyList<SurveyPoint>`; `ReplacePoints(IReadOnlyList<SurveyPoint>)`, `SetPoint(int, SurveyPoint)`, `OffsetHeight(double)`, `Undo()` returning `bool`.

- [ ] Write a dependency-free console regression harness linking the model source files. Assert editing one point and undo restores every field; negative heights remain negative; offset affects every nonempty height; empty undo returns false; nonfinite coordinates are rejected without mutation.
- [ ] Run `dotnet run --project tests/NativeWorkspace`; confirm failure before implementation.
- [ ] Implement immutable point values and bounded undo snapshots (100 commands). Nullable coordinates represent cleared cells; do not substitute zero for missing data.
- [ ] Run the harness; require exit code 0.

### Task 2: Native Table

**Files:** Create `src/KhitunGeo/Native/PointGrid.cs`, `src/KhitunGeo/Native/TabularPaste.cs`; extend `tests/NativeWorkspace/Program.cs` and its linked sources.

**Interfaces:** `PointGrid.Bind(PointWorkspace)`; `TabularPaste.Parse(string)` returning `IReadOnlyList<SurveyPoint>`; parsing supports tab-delimited spreadsheet data and quoted CSV with invariant or Russian decimal values.

- [ ] Add parser tests for tabs, semicolon CSV, quoted descriptions, decimal comma, negative heights, missing values and invalid numbers. Invalid input must leave workspace unchanged.
- [ ] Run the harness and confirm new tests fail.
- [ ] Implement parsing using a structured CSV parser from the available framework; reject ambiguous layouts rather than guessing coordinate order.
- [ ] Implement DataGridView with fixed column sizing and deferred validation. Add Ctrl/Shift row and column selection, Delete to clear selected cells, Ctrl+V and Ctrl+Z. Each bulk operation is one undo command.
- [ ] Run parser/model tests. On Windows verify invalid edits, empty selection, mixed selection deletion and undo; edit 10,000 rows without rebuilding the grid per keystroke.

### Task 3: Native Workspace Preview

**Files:** Create `src/KhitunGeo/Native/NativeWorkspaceForm.cs`, `src/KhitunGeo/Native/NativeTheme.cs`, `src/KhitunGeo/Native/PointPreview.cs`; modify `src/KhitunGeo/Program.cs` only to recognize `--native-preview`.

**Interfaces:** `NativeWorkspaceForm()` is opt-in and never replaces default startup; `PointPreview.SetPoints(IReadOnlyList<SurveyPoint>)`, `SetActive(bool)`; themes named light and dark. The preview cannot transform coordinates yet.

- [ ] Add a Windows smoke checklist covering default startup, native startup, small window, theme switching, splitter resize, empty points, negative height and hide/show preview.
- [ ] Implement the compact sidebar, aligned source/target CRS row, table/preview splitter and status strip. Show conversion controls as unavailable until a verified adapter exists; do not report a fake successful conversion.
- [ ] Implement paint-on-demand point preview with bounded camera and visible-area culling. No persistent rendering timer; hidden preview clears temporary graphics resources and ignores redraw requests.
- [ ] Implement theme toggle and the all-points height-offset dialog using Task 1 commands. Keep animations off initially; add sidebar-only animation later with a reduced-motion switch and disposed timer.
- [ ] Build the Windows target with `dotnet build src/KhitunGeo/KhitunGeo.csproj -p:EnableWindowsTargeting=true`. Run the model/parser harness; perform Windows smoke checks when a Windows runner is available, otherwise explicitly record them as pending.

### Task 4: Integration Gate and Next Stage

**Files:** Update this plan with measured results; prepare a separate integration plan only after inspecting relevant methods in `MainForm.cs`, `MainForm.Drawing.cs` and the coordinate module.

- [ ] Inventory native import/export adapters and PDF dependencies by symbol search; do not rewrite those modules in the preview task.
- [ ] Define the browser-free calculation approach before selecting its runtime. Keep existing formulas and capture the fixed control-point regression set.
- [ ] Compare preview memory at idle, with points, and hidden visualization on Windows; report actual measurements rather than estimated savings.
- [ ] Obtain approval for the integration approach before replacing the production UI or removing WebView2. Keep GitHub publication and installer release separate.

## Completion Gate

Each task is complete only after its targeted checks pass. Windows visual and memory tests are mandatory before replacing the production screen. Full coordinate checks are unnecessary for this UI-only stage; calculation-runtime changes in the next stage require the fixed coordinate regression set.
