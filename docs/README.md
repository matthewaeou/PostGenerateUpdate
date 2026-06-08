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
  + version‑stamp discipline, in‑context exports (PDF/parts list), logging rules, and
  the open questions still to settle empirically.

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

Two harnesses in the repo root turn the 📘/⚠️ items into measured facts:

- **[../ValidateApi.cs](../ValidateApi.cs)** — run interactively (`Utilities ▸ Scripts ▸
  Run…`, project selected). Read-only by default. Reflection-probes every uncertain API
  and runtime-probes project/DataModel/properties/settings/paths/actions, then writes
  `…\DOC\ValidateApi.log` and shows a PASS/FAIL/INFO summary.
- **[../ValidateApi_eBuild.cs](../ValidateApi_eBuild.cs)** — attach as a Script-Typical
  and generate a project. Answers the big open question — *does the DataModel work during
  eBuild generation?* — into `…\DOC\ValidateApi_eBuild.log`.

Workflow: run them, read the logs, then promote the doc grades (📘→✅, or correct ⚠️) to
match what this EPLAN version actually does.
