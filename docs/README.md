# EPLAN scripting & automation — reference docs

Working references for the two tools we build on in this repo. Both grade every claim:
✅ confirmed in our own running scripts, 📘 from EPLAN docs/community (verify before
relying), ⚠️ a trap or contested point. When ✅ and 📘 conflict, trust ✅ — the source
code in the parent folder is ground truth.

- **[EPLAN-Scripting-Reference.md](EPLAN-Scripting-Reference.md)** — the C# scripting
  API: entry‑point attributes, `CommandLineInterpreter`/`ActionCallingContext`, the
  `generate`/`renumber`/`label`/`export` actions, DataModel + properties, `Decider`,
  `MultiLangString`, context menus, `PathMap`, gotchas, reusable snippets.
- **[eBuild-Automated-Project-Generation-Reference.md](eBuild-Automated-Project-Generation-Reference.md)**
  — the generation pipeline (Designer / Project Builder / Script‑Typicals): the
  `[Start](string ProjectName)` contract, `/PROJECTNAME` requirement, reload‑after‑edit
  + version‑stamp discipline, in‑context exports (PDF/parts list), logging rules, the
  Designer model + Style‑Guide authoring rules (§2a/§2b), silent‑mode batch generation
  (§10), and the open questions still to settle empirically.

**Primary offline sources** (read 2026‑06‑09, local PDFs under
`…\Downloads\Fichiers (5)\eBuild_Training\eBuild_Training\`): the official *eBUILD
Trainingsbook V2* (128 p.), the *eBUILD Library Rules / Style Guide* (2020‑09‑21), and
the **EPLAN Consulting Macro Utility V2.0.1** — a shipped production script add‑on whose
source code proved several things web research got wrong (scripts CAN write properties
via `XEsSetPropertyAction`, CAN use `Eplan.EplApi.MasterData`, CAN create custom
settings; see scripting reference §5a/§18a).

The ✅ items are distilled from the working scripts one level up
([../PostGenerationNumbering_eBuild.cs](../PostGenerationNumbering_eBuild.cs),
[../PostGenerationExports_eBuild.cs](../PostGenerationExports_eBuild.cs),
[../PageNavi_ContextMenu_OpenFolders.cs](../PageNavi_ContextMenu_OpenFolders.cs), …), the
compiled add-ins ([../addin/README.md](../addin/README.md)), and the project memory note.
The DataModel work lives in the **add-ins**, not in simple scripts — the early
`ExportEngravingData.cs` / `FinalizeProject_Manual.cs` simple-script attempts could not
bind to the object model (CS0234); `ExportEngravingData.cs` is now superseded by the
`EngravingDataExport` action and `FinalizeProject_Manual.cs` was reworked to use only
actions.

**External examples:** the best free library of runnable EPLAN scripts is
[Suplanus/EplanScriptingProjectBySuplanus](https://github.com/Suplanus/EplanScriptingProjectBySuplanus)
("All scripts from Suplanus"). The companion website `eplan-scripting.suplanus.de` has
free *beginner* pages but its *expert* pages are paywalled — use the GitHub repo for the
actual code. The scripting reference §18 maps that repo's folders by topic.

## Validating the docs against this install

Several throwaway-but-kept harnesses in the repo root turn the 📘/⚠️ items into measured
facts. **They are diagnostic probes, not production tools** — keep them for re-checking
after an EPLAN upgrade or when a doc claim is in doubt; each writes a timestamped log and
has a self-describing header explaining exactly what it tested and why.

- **[../ValidateApi.cs](../ValidateApi.cs)** — run interactively (`File ▸ Extras ▸
  Interfaces ▸ Scripts ▸ Run`, project selected). Read-only by default. Reflection-probes
  every uncertain API and runtime-probes project/DataModel/properties/settings/paths/
  actions, then writes `…\DOC\ValidateApi.log` and shows a PASS/FAIL/INFO summary.
- **[../ValidateApi_eBuild.cs](../ValidateApi_eBuild.cs)** — attach as a Script-Typical
  and generate a project. Answers the big open question — *does the DataModel work during
  eBuild generation?* — into `…\DOC\ValidateApi_eBuild.log`.

### Property/parts probes (2026-06-09/10) — what they proved

Written to settle whether a *simple script* can touch object data without a compiled
add-in (it can, more than we thought — see scripting reference §0, §5a). Kept as
regression checks; re-run after an EPLAN version bump.

- **[../Probe_MasterData.cs](../Probe_MasterData.cs)** — *Run* (no selection needed).
  Proved `using Eplan.EplApi.MasterData;` compiles in a script and the parts DB is
  readable (`MDPartsManagement().OpenDatabase()` → 542 parts); dumps the `MDPart` surface
  to `%TEMP%\EPLAN_Scripts\Probe_MasterData.log`. ✅ MasterData allowed in scripts.
- **[../Probe_XEsSetProperty.cs](../Probe_XEsSetProperty.cs)** — *Run*, device selected.
  Action capability map (`ActionManager.FindAction`) + the §17 parameter-metadata dump,
  and the key negative result: `XEsSetPropertyAction` returns `True` from *Run* **without
  writing** (CSV-verified no-op). ⚠️ `Execute()==True` ≠ success.
- **[../Probe_XEsSetProperty_Menu.cs](../Probe_XEsSetProperty_Menu.cs)** — *Load* (it's a
  `[DeclareMenu]`/`[DeclareAction]` script), then right-click a device in the GED. The
  decisive one: from a **context-menu** action, `XEsSetPropertyAction` **writes** (20011 +
  20025, CSV-verified) and `XEsGetPropertyAction` **reads back** (output slot
  `propertyvalue`, found by enumerating the context). ✅ selected-object read+write from a
  plain script — *only* via a context-menu invocation. Appends to
  `…\DOC\Probe_XEsSetProperty.log`.

Workflow: run them, read the logs, then promote the doc grades (📘→✅, or correct ⚠️) to
match what this EPLAN version actually does.
