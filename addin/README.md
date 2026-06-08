# EPLANCA add-ins

Two compiled EPLAN add-ins. They exist as add-ins (not simple scripts) because the
DataModel object model is **not reachable from a simple script** on EPLAN 2026 — the
script runtime can't create a `LockingStep`, so `SelectionSet.GetCurrentProject` throws
`NoLockingStepException`, and `using Eplan.EplApi.DataModel` fails to compile (CS0234).
An add-in runs inside EPLAN's action framework, which **can** read and write the model.
See [../docs/EPLAN-Scripting-Reference.md](../docs/EPLAN-Scripting-Reference.md) §0/§6.

## Files
- `Eplan.EplAddIn.EngravingData.cs` — engraving round-trip (two actions).
- `Eplan.EplAddIn.ProjectCheck.cs` — project quality checks (one action) **and** owner of
  the shared **EPLANCA ribbon tab** (built in `OnInitGui`).
- `build.ps1` — compiles **both** DLLs with the .NET Framework `csc` against the EPLAN API.
- `bin\Eplan.EplAddIn.EngravingData.dll`, `bin\Eplan.EplAddIn.ProjectCheck.dll` — the built
  add-ins (compiled against EPLAN 2026.0.3, .NET Framework v4.0.30319, x64).

## Actions registered
| Action | Add-in | Effect |
|---|---|---|
| `ProjectCheck` | ProjectCheck | quality checks → `<project>.edb\DOC\ProjectCheck.log` + summary dialog |
| `EngravingDataExport` | EngravingData | writes `<project>.edb\DOC\EngravingData.csv` (Key, DT, Page, Location, PartNumber, FunctionText, EngravingText) for every function with engraving text |
| `EngravingDataImport` | EngravingData | reads that CSV back; writes `FunctionText` + `EngravingText` onto functions matched by `Key` (= device tag) |

## EPLANCA ribbon tab
The ProjectCheck add-in builds an **EPLANCA** tab in `OnInitGui` via
`RibbonBar.AddDelayedAction` (which fires *after* EPLAN restores its saved ribbon config,
so the tab survives restart). Groups and buttons:

| Group | Button | Runs |
|---|---|---|
| Checks | Check Project | `ProjectCheck` action |
| Engraving | Export Engraving / Import Engraving | `EngravingDataExport` / `EngravingDataImport` (needs EngravingData.dll loaded) |
| Post-Generation | Run Numbering / Finalize Project | `ExecuteScript` on `PostGenerationNumbering.cs` / `FinalizeProject_Manual.cs` |
| Diagnostics | Validate API | `ExecuteScript` on `ValidateApi.cs` |

The Engraving buttons call actions that live in **EngravingData.dll**, so that add-in must
also be loaded for them to work. The Post-Generation / Diagnostics buttons launch the
matching `[Start]` scripts in the repo root via the built-in `ExecuteScript` action.

> **Ribbon timing gotcha:** registering ribbon commands directly in `OnInitGui` (or in
> `OnRegister`) doesn't stick — EPLAN overwrites the ribbon when it restores its saved
> config. The working pattern is `AddDelayedAction` + the explicit
> `AddTab → AddCommandGroup → AddCommand` chain.

## Build (only needed to rebuild)
```
powershell -ExecutionPolicy Bypass -File addin\build.ps1
```
Builds both DLLs; stops with exit code 1 on the first failure.
⚠️ EPLAN holds the add-in DLLs in memory while running, so a rebuilt DLL only takes effect
after EPLAN is **fully restarted** (a normal restart is enough; you don't need to
unregister). If a restart doesn't pick up the change, unload/reload the add-in in
**Utilities ▸ API ▸ Add-Ins…**.

## Deploy (one time, per DLL)
1. EPLAN ▸ **Utilities ▸ API ▸ Add-Ins…**
2. **Add…** → browse to the DLL under
   `C:\Users\Public\EPLAN\Data\Scripts\EPLANCA\addin\bin\`
3. Make sure it's set to **load on start**; confirm. Repeat for both DLLs.

## Use / test the engraving round-trip
1. **Single-click the project** node in the Pages navigator.
2. **Export:** click **Export Engraving** on the EPLANCA tab (or run
   [`../RunEngravingExport.cs`](../RunEngravingExport.cs)). Check `…\DOC\EngravingData.csv`
   and `…\DOC\EngravingDataExport.log`.
3. **Edit** the CSV — change the `FunctionText` / `EngravingText` columns only. **Do not
   edit `Key`** (it's the match key). Newlines are encoded as the literal `\n`.
4. **Import:** click **Import Engraving** (or run
   [`../RunEngravingImport.cs`](../RunEngravingImport.cs)). Check the result dialog and
   `…\DOC\EngravingDataImport.log`. ⚠️ **Test on a project copy first** — it modifies the project.

## Notes
- **Import language:** writes the value for the **current GUI language** only (a
  `MultiLangString` with one entry). Multi-language texts may need refining once we see real data.
- **`FUNC_GRAVINGTEXT` type:** the writer tries `MultiLangString` then falls back to a
  plain string — both engraving export and import are confirmed working in this project.
