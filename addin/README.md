# EngravingData add-in

A compiled EPLAN add-in for the field-item **engraving-text round trip** (export → edit →
import). Built as an add-in because the DataModel object model is **not reachable from a
simple script** on EPLAN 2026 (script runtime can't create a `LockingStep` — see
[../docs/EPLAN-Scripting-Reference.md](../docs/EPLAN-Scripting-Reference.md) §0/§6). An
add-in runs in EPLAN's action framework, which **can** read and write the model.

## Files
- `Eplan.EplAddIn.EngravingData.cs` — source (add-in lifecycle + two actions).
- `build.ps1` — compiles with the .NET Framework `csc` against the EPLAN API DLLs.
- `bin\Eplan.EplAddIn.EngravingData.dll` — the built add-in (✅ compiled against EPLAN
  2026.0.3, .NET Framework v4.0.30319).

## Actions it registers
| Action | Effect |
|---|---|
| `EngravingDataExport` | writes `<project>.edb\DOCS\EngravingData.csv` (Key, DT, Page, Location, PartNumber, FunctionText, EngravingText) for every function with engraving text |
| `EngravingDataImport` | reads that CSV back; writes `FunctionText` + `EngravingText` onto functions matched by `Key` (= device tag) |

## Build (only needed to rebuild)
```
powershell -ExecutionPolicy Bypass -File addin\build.ps1
```
⚠️ EPLAN locks the DLL while loaded — **close EPLAN (or unregister the add-in)** before
rebuilding, or the build will fail to overwrite `bin\…dll`.

## Deploy (one time)
1. EPLAN ▸ **Utilities ▸ API ▸ Add-Ins…**
2. **Add…** → browse to
   `C:\Users\Public\EPLAN\Data\Scripts\EPLANCA\addin\bin\Eplan.EplAddIn.EngravingData.dll`
3. Make sure it's set to **load on start**; confirm. (It registers the two actions.)

## Use / test
1. **Single-click the project** node in the Pages navigator.
2. **Export:** run [`../RunEngravingExport.cs`](../RunEngravingExport.cs) via
   `Utilities ▸ Scripts ▸ Run…`. Check `…\DOCS\EngravingData.csv` and
   `…\DOCS\EngravingDataExport.log`.
3. **Edit** the CSV — change the `FunctionText` / `EngravingText` columns only. **Do not
   edit `Key`** (it's the match key). Newlines are encoded as the literal `\n`.
4. **Import:** run [`../RunEngravingImport.cs`](../RunEngravingImport.cs). Check the result
   dialog and `…\DOCS\EngravingDataImport.log`. ⚠️ **Test on a project copy first.**

## Known caveats (to verify on first run)
- **Trigger path:** the launchers call the action via `CommandLineInterpreter` from a
  script. If that re-hits `NoLockingStepException`, the script context isn't handing the
  action a writable project — in that case trigger the action from the EPLAN GUI instead
  (Utilities ▸ API) and tell me; I'll add a **ribbon/menu button** to the add-in
  (`OnInitGui`) for a guaranteed-good context.
- **Import language:** writes the value for the **current GUI language** only (a
  `MultiLangString` with one entry). If your texts are multi-language this may need
  refining once we see real data.
- **`FUNC_GRAVINGTEXT` type:** the writer tries `MultiLangString` then falls back to a
  plain string — the first run's log will tell us which the property actually is.
