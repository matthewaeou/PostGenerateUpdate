# EPLAN Automated Project Generation (eBuild / Cogineer) — Reference

How EPLAN generates projects from templates, and how our **Script‑Typicals** hook into
that generation. Companion doc: [EPLAN C# Scripting Reference](EPLAN-Scripting-Reference.md)
— read it for the script‑side API (actions, `CommandLineInterpreter`, DataModel,
properties). This doc focuses on the **generation pipeline** and the rules that only
apply inside it.

## How to read this document

- ✅ **Confirmed** — proven by this repo's working Script‑Typicals or the project memory.
- 📘 **From docs** — EPLAN Help / community research, not yet verified in our setup.
- ⚠️ **Caution** — contested, version‑specific, or a known trap.

When ✅ and 📘 disagree, **✅ wins.** The web research made a couple of strong claims
(e.g. "no DataModel API during generation", "eBuild is purely cloud SaaS") that we have
**not** confirmed and that conflict with general EPLAN usage — they're flagged ⚠️ below
rather than stated as fact.

---

## 1. What this tool is

EPLAN's automated project generation builds a finished project from reusable templates
plus a set of input values, instead of drawing it by hand. Naming has shifted over the
years — **Cogineer** (desktop) and **eBuild** (the cloud‑hosted evolution) are the same
family; the user here refers to it as "Automated Project Generation (formerly eBuild)."
The three moving parts:

| Component | Role | Status |
|---|---|---|
| **Designer** | Authoring tool: build the templates ("Typicals"), define configuration variables, attach Script‑Typicals | ✅ (we attach scripts here) |
| **Project Builder** | The generation engine: pick a configuration, supply variable values, name the output, run generation | ✅ |
| **Script‑Typicals** | C# scripts that run **during/after** generation to post‑process the project | ✅ (our scripts) |

Flow: **Designer** (define rules + attach scripts) → **Project Builder** (enter values,
generate) → **Script‑Typicals** (numbering, exports, custom logic) run automatically.

⚠️ The research repeatedly framed eBuild as "cloud SaaS with no REST API." The cloud
variant exists, but our work runs the **desktop** pipeline locally (Script‑Typicals
reference local `.cs` files and must be reloaded in the Designer). Don't assume the
cloud framing applies to this setup.

---

## 2. Designer: authoring & attaching scripts

📘 Authoring workflow (templates side — we mostly consume, not author, so verify against
your Designer version):
- A **Library** holds **Configurators**; a Configurator bundles **Macro‑Typicals**
  (symbol/macro templates with placeholders), **Typical‑Groups**, and **Script‑Typicals**.
- **Configuration variables** are named inputs (text/number/list, optional default) that
  the Project Builder will prompt for and that substitute into macro placeholders.

✅ / 📘 Attaching a Script‑Typical (this is the part that governs our scripts):
1. Add the `.cs` file as a Script‑Typical under the Configurator.
2. ⚠️ The Designer **references the file by path and caches/compiles it at load** — so
   after editing the script you **must reload it in the Designer** before the new code
   runs. This is the #1 source of "my change didn't take" confusion. Our defense: a
   `ScriptVersion` constant printed to the log (see §7).
3. 📘 To pass extra inputs, declare Designer **parameters** for the script and bind them
   to configuration variables — **but never declare `ProjectName`** (it's injected, §4).

---

## 3. Project Builder: configuring a generation run

📘 (from docs + general use):
1. Pick the **Library → Configurator**.
2. **Enter configuration‑variable values** in the generated form.
3. **Choose the template project** — its settings, schemes, and structure seed the
   output. ⚠️ This is why your numbering/PDF/label **scheme names must exist in the
   template** (the scripts reference them by exact name).
4. **Name & locate the output.** The Project Builder creates `<Name>.elk` plus the
   `<Name>.edb\` data folder. ⚠️ Avoid spaces/diacritics/non‑ASCII in the name — they
   corrupt paths downstream.
5. **Generate** (single or queued). Generation builds the schematic, then runs the
   attached Script‑Typicals.

---

## 4. The Script‑Typical contract ✅

This is the part we've nailed down in code.

```csharp
[Start]
public void RunFromEBuild(string ProjectName)   // ← the eBuild signature
{
    // ProjectName is the FULL PATH to the generated .elk, injected by the generator.
}
```
Confirmed in [PostGenerationNumbering_eBuild.cs:23‑24](../PostGenerationNumbering_eBuild.cs)
and [PostGenerationExports_eBuild.cs:63‑64](../PostGenerationExports_eBuild.cs).

Rules:
- ✅ **`ProjectName` is auto‑injected** by the generator — the full path to the `.elk`
  (e.g. `C:\…\MyProject.elk`), **not** the `.edb` folder.
- ✅ **Do NOT declare `ProjectName` as a Designer parameter.** It is reserved; declaring
  it breaks the binding. (Project memory + research agree.)
- ✅ **Unattended context**: no `SelectionSet`, no `Decider`/dialogs. Everything is
  driven through `CommandLineInterpreter` actions and logged to a file.
- ✅ The method name is irrelevant; `[Start]` + the `string` parameter is what matters.
  We call it `RunFromEBuild` by convention.

### Interactive ⇄ eBuild, side by side ✅
The same logic ships to both contexts by swapping the entry point — the difference is
*how the project is identified*:

```csharp
// INTERACTIVE: project comes from the selection
[Start] public void RunManual() { Execute(/* SelectionSet path */); }

// EBUILD: project path is injected
[Start] public void RunFromEBuild(string ProjectName) { Execute(ProjectName); }
```
See the commented dual entry points in
[PostGenerationNumbering.cs:29‑42](../PostGenerationNumbering.cs).

---

## 5. Passing extra parameters / template variables 📘

⚠️ **Untested in our code** — documented pattern only:
```csharp
[Start]
public void RunFromEBuild(string ProjectName, string Voltage, int Count) { … }
```
- Declare `Voltage` and `Count` as Script‑Typical **parameters in the Designer** and
  **bind them to configuration variables**; the generator injects them as **method
  arguments** after `ProjectName`, in declared order.
- An unbound parameter takes its Designer default.
- Names/types in the C# signature must match the Designer declarations.

Verify the injection order and type coercion (e.g. `int` vs `string`) with a throwaway
Script‑Typical that just logs its arguments before relying on this for real work.

---

## 6. Generation sequence & available context

✅ what we know from running scripts:
- Script‑Typicals run **after** the schematic content is generated, with the project
  **open** in EPLAN; `ProjectName` points at it on disk.
- ⚠️ Pass `/PROJECTNAME` to actions anyway (§8) — the unattended context has **no
  "current selection"**, so actions that default to the selection find nothing.
- ✅ `CommandLineInterpreter` actions (`generate`, `renumber`, `label`, `export`) work.
- ✅ File I/O works (that's how we log and export).
- ⚠️ eBuild **saves the project itself** after the scripts — `XGedSaveProject` doesn't
  exist in this context (memory).

📘 Ordering: multiple Script‑Typicals run in Designer order; a failure in one is not
supposed to cascade. Verify if you depend on it.

### ✅ DataModel during generation — confirmed findings (build 25625, 2026‑06‑08)

A Script‑Typical is compiled with the same restricted assembly allowlist as
`Scripts ▸ Run…` — see [scripting reference §0](EPLAN-Scripting-Reference.md).
You **cannot `using` `Eplan.EplApi.DataModel`/`HEServices`** (CS0234 compile error).

**Confirmed by `ValidateApi_eBuild.cs` running as a Script‑Typical during generation:**

| Question | Result |
|---|---|
| DataModel assembly loaded at runtime? | ✅ Yes — both `DataModelu` and `HEServicesu` are in the AppDomain |
| Can a Script‑Typical get a `Project` handle? | ❌ No — `SelectionSet.GetCurrentProject()` throws the same `NoLockingStepException S063110` as in a simple script. Reflection reaches the types but can't create a `Project`. `ProjectManager` is present (could `OpenProject` by path) but risky to attempt during generation. |
| Does `selectionset TYPE=PROJECT` return the generated project? | ⚠️ **No — it returns the MACRO/TEMPLATE project**, not the generated one. Use the injected `ProjectName` parameter — it is the only reliable path to the generated project inside a Script‑Typical. |

Two real options inside a Script‑Typical:
1. **Actions** — `CommandLineInterpreter` (`generate`/`renumber`/`label`/`export`,
   `GetProjectProperty`). Use the injected `ProjectName`, not `selectionset`, to
   reference the generated project. This is what all our working eBuild scripts use.
2. **Reflection** 🧰 — DataModel types are reachable by string, but you still can't get
   a `Project` handle (same locking barrier as a simple script). Useful for inspecting
   types; not useful for reading/writing project data.

For full object‑model work (read/write functions, properties, connections) a compiled
**add‑in** is the only route. Trigger it from a Script‑Typical via
`CommandLineInterpreter.Execute("YourActionName")`.

---

## 7. Logging discipline (no dialogs!) ✅

Unattended ⇒ **never** call `Decider`/message boxes (they hang generation). Log to a
file in the project's `DOC` folder instead. The house skeleton, proven across the
eBuild scripts:

```csharp
string docPath = Path.Combine(Path.ChangeExtension(ProjectName, ".edb"), "DOC");
Directory.CreateDirectory(docPath);
string logPath = Path.Combine(docPath, "PostGenerationExports.log");

var log = new StringBuilder();
log.AppendLine("=== PostGenerationExports  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
log.AppendLine("Script version : " + ScriptVersion);   // ⚠ bump every edit (§2)
log.AppendLine("user           : " + Environment.UserName + " @ " + Environment.MachineName);
log.AppendLine("project        : " + ProjectName);
// … append a line per action result …
try { File.AppendAllText(logPath, log.ToString() + Environment.NewLine, new UTF8Encoding(true)); } catch { }
```
- ✅ Logs land in `<project>.edb\DOC\`, derived via
  `Path.ChangeExtension(ProjectName, ".edb")` — **not** `%TEMP%`. (`DOC` is the EPLAN
  standard folder name; not `DOCS`.)
- ✅ `File.AppendAllText` (append, don't overwrite) so successive runs accumulate.
- ✅ Wrap the final write in try/catch so a locked file never throws out of generation.
- ✅ Write UTF‑8 explicitly (`new UTF8Encoding(true)`) — EPLAN's default encoding is not UTF‑8.
- ✅ **Print `ScriptVersion`** so you can prove which compiled copy actually ran — the
  antidote to the Designer's cache (§2). Currently the scripts carry stamps like
  `2026-06-08.1`.

---

## 8. `generate` + `renumber` in the generation context ✅

The post‑generation numbering recipe (order matters):
```csharp
cli.Execute("generate /TYPE:CONNECTIONS");                       // 1) build def. points

cli.Execute("renumber /TYPE:DEVICES " +                          // 2) device tags
    "/CONFIGSCHEME:\"ECLIPSE ROW_IDENTIFIER\" " +
    "/PROJECTNAME:\"" + ProjectName + "\" " +
    "/STARTVALUE:1 /STEPVALUE:1 /ALSONUMERATEDBYPLC:0 /POSTNUMERATE:0");

cli.Execute("renumber /TYPE:CONNECTIONS " +                      // 3) wire numbers
    "/CONFIGSCHEME:\"Eclipse NFPA Standard without PLC address\" " +
    "/PROJECTNAME:\"" + ProjectName + "\"");
```
- ✅ **`generate /TYPE:CONNECTIONS` first** so connection definition points exist before
  you renumber connections.
- ⚠️ **`/PROJECTNAME` is mandatory here.** This was *the* fix: without it the renumber
  finds nothing (no selection in unattended mode) and silently no‑ops. With it, both
  device and connection renumbering work.
- ✅ `/POSTNUMERATE:0` = renumber everything (not just `?`‑marked).
- ⚠️ `/CONFIGSCHEME` names are **case‑sensitive** and must exist in the template project.
- ⚠️ `generate /TYPE:NUMBERING` is **not a thing** — numbering is `renumber`.

Full action‑parameter detail is in the scripting reference, §5.

---

## 9. Exports during generation ✅

### Parts list — `label` ✅ (works in‑context)
```csharp
cli.Execute(
    "label /CONFIGSCHEME:\"Summarized parts list\" " +
    "/EXPORTFILE:\"" + partsFile + "\" /LANGUAGE:en_US " +
    "/PROJECTNAME:\"" + ProjectName + "\"");
```
Uses the data/label engine, so it's reliable unattended.
[PostGenerationExports_eBuild.cs:81‑84](../PostGenerationExports_eBuild.cs).

### PDF — `export` with `TYPE:PDFPROJECTSCHEME` ✅ (works in‑context — with the right values)
```csharp
ActionCallingContext ctx = new ActionCallingContext();
ctx.AddParameter("TYPE",        "PDFPROJECTSCHEME");
ctx.AddParameter("EXPORTSCHEME", "EPLAN_default_value");  // ⚠ internal NAME, not "Default"
ctx.AddParameter("EXPORTFILE",   pdfFile);
ctx.AddParameter("PROJECTNAME",  ProjectName);
cli.Execute("export", ctx);
_log.AppendLine("PDF file exists : " + File.Exists(pdfFile));   // ✅ verify on disk
```
This is the **resolved** state of a long debugging saga (memory):
- ⚠️ Earlier `S025019 "operation not supported. Parameter name: PDF"` was caused by an
  **invalid `TYPE`** (`/TYPE:PDF` or a bare `export`), **not** a blocked context. With
  `TYPE=PDFPROJECTSCHEME`, PDF export **does run inside eBuild generation.**
- ⚠️ The last blocker was the **scheme name**: the dialog shows "Default" but the real
  name is **`EPLAN_default_value`** (EPLAN error `S029123` listed the valid name).
- Use `PDFPAGESSCHEME` for a page‑scoped PDF. `LANGUAGE` is optional for `export`.

> Historical note: the docs you'll find online often say "PDF/print engine isn't
> available during generation." In **our** testing that turned out to be **false** once
> the `TYPE` and scheme name were correct. Keep the fallback below only as insurance.

### Fallback: interactive finalize ✅
[FinalizeProject_Manual.cs](../FinalizeProject_Manual.cs) does the same label + PDF
exports but as an **interactive** `[Start] Run()` you trigger via `Scripts ▸ Run…` after
generation (resolving the project with `SelectionSet`). Kept as insurance in case PDF
export ever regresses in‑context; **not wired into eBuild.**

---

## 10. Command‑line / batch invocation 📘 ⚠️

The research reported EPLAN can be driven headless, e.g.:
```text
eplan.exe /NoSplash /Frame:"0" /Auto  <action> <parameters…>
```
with `/Auto` to exit afterward and `/Frame:"0"` for no window. ⚠️ **Unverified in our
environment** and the exact switches vary by version/product — confirm against
`eplan.help` "command line parameters" for your installed platform before scripting a
batch pipeline. For now, generation is driven from the Project Builder UI/queue.

---

## 11. Gotchas — generation context

1. ⚠️ **Reload the script in the Designer after every edit** (cached compile). Prove the
   running copy with a logged `ScriptVersion`.
2. ⚠️ **`/PROJECTNAME` on every action** — no selection exists unattended.
3. ⚠️ **Scheme names**: exact case, internal name (`EPLAN_default_value`), and they must
   exist in the **template** project.
4. ⚠️ **No dialogs** — `Decider`/message boxes hang generation; log to `DOC` instead.
5. ⚠️ **Don't declare `ProjectName`** as a Designer parameter.
6. ⚠️ **`ProjectName` is the `.elk` path**, not the `.edb` folder — derive `DOC` with
   `Path.ChangeExtension(ProjectName, ".edb")`.
7. ⚠️ **No `XGedSaveProject`** — eBuild saves the project itself.
8. ⚠️ **Project names**: no spaces/diacritics/non‑ASCII (path corruption).
9. ✅ **DataModel object model is NOT available in a Script‑Typical** (§6) — same locking
   barrier as a simple script. DataModel assemblies ARE loaded, but `GetCurrentProject()`
   throws. Use actions or a compiled add‑in for object‑model work.
10. ⚠️ **`selectionset TYPE=PROJECT` returns the MACRO project**, not the generated one —
    always use the injected `ProjectName` to reference the generated project (§6).
10. ⚠️ **Check action `bool` returns + verify side effects on disk** — failures are
    silent.

---

## 12. Worked examples in this repo

| Script | What it shows about the generation pipeline |
|---|---|
| [PostGenerationNumbering_eBuild.cs](../PostGenerationNumbering_eBuild.cs) | The canonical Script‑Typical: `[Start](string ProjectName)`, `generate`+`renumber` with `/PROJECTNAME`, `DOC` logging, version stamp |
| [PostGenerationExports_eBuild.cs](../PostGenerationExports_eBuild.cs) | `label` xlsx + `export` PDF in‑context, `Try`/`TryCtx` logging wrappers, on‑disk verification |
| [PostGenerationNumbering.cs](../PostGenerationNumbering.cs) | Interactive twin (dual entry‑point pattern for shipping the same logic both ways) |
| [FinalizeProject_Manual.cs](../FinalizeProject_Manual.cs) | Interactive post‑generation fallback via `SelectionSet` |

---

## 13. Open questions — status after empirical testing

[`ValidateApi_eBuild.cs`](../ValidateApi_eBuild.cs) ran as a Script‑Typical during
generation (2026‑06‑08, build 25625). Log: `<project>.edb\DOC\ValidateApi_eBuild.log`.

- ✅ **DataModel during generation (Q1/Q2 — answered):** Both `DataModelu` and
  `HEServicesu` ARE loaded. But `SelectionSet.GetCurrentProject()` throws
  `NoLockingStepException` — same barrier as a simple script. Object‑model reads/writes
  are **not possible** from a Script‑Typical. Use a compiled add‑in triggered via
  `CommandLineInterpreter.Execute("ActionName")` (§6).
- ✅ **`selectionset TYPE=PROJECT` in eBuild (Q3 — answered):** Returns the **macro/
  template** project path, **not** the generated project. Always use the injected
  `ProjectName` parameter for the generated project path.
- 📘 **Extra parameter injection** — confirm bound config‑variable values arrive as
  method args in declared order, with expected types (§5).
- 📘 **Headless invocation** — whether the Project Builder/generation can be triggered
  from the command line for true batch runs (§10).
- 📘 **Multi‑script ordering & failure isolation** — does one failing Script‑Typical
  stop the rest?

---

## 14. Sources

- EPLAN Help / Infoportal — eBuild "Script‑Typicals: Basics", "Project Builder: Basics",
  action references (`renumber`, `export`, `label`), command‑line parameters
  (`eplan.help`).
- `github.com/musray/PDFPerLocation`; Suplanus EPLAN scripting guide.
- This repo's eBuild Script‑Typicals + project memory note
  `eplan-ebuild-script-context` (every ✅ here).

> ⚠️ Treat 📘/⚠️ items as leads, not facts — especially the "no DataModel" and "cloud
> SaaS / headless" claims, which we have **not** verified and which partly conflict with
> how our local pipeline actually behaves.
