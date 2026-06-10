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

## 2a. Designer model — from the official trainingsbook 📘

Source: *3‑00 eBUILD Trainingsbook V2* (EPLAN training, 128 p., local PDF). All 📘 —
authoritative for concepts, but UI details may shift between eBUILD releases.

**Object model.** A **Macro‑Typical (MT)** is a container that calls macros and holds
their configuration (one MT ≈ one function/page of the machine). A **Typical‑Group
(TG)** groups MTs and other TGs. A **Configurator** is what the Project Builder user
runs; it bundles MTs/TGs. Roles: *Designer* (Designer + Project Builder) vs *User*
(Project Builder only). Product tiers: eBUILD **Free** (Project Builder + free
libraries), **Designer**, **Project Builder**.

**Configuration variables (CVs)**: types `Integer`, `Double`, `String`, `Boolean`;
per‑CV options — *Display name* (what the Project Builder shows), *Mandatory* (input
marked `*`), *Visibility* (show the field only under a condition), *Selection values*
(dropdown; free entry then disabled). ⚠️ Type matters in formulas: `1+2` is `3` for
Integers but `12` (concatenation) for Strings.

**Macro entry in an MT** — the per‑macro panel is *Status* (generate or not — formula
allowed), *Position*, *Structure* (structure identifiers via formulas), *Variables*
(macro/placeholder variables via formulas), *References* (usage points).

**Positioning** (window `.ema` / symbol `.ems` only; page macros `.emp` have no
position): *Apply from macro* (insertion point stored in the macro), *Absolute*
(X/Y, formula‑capable), *Sequential* (Direction + Offset **relative to the edge of the
preceding macro's macro box** — not available for the first macro in the list).

**Formula language** (in Status/Structure/Variables fields): leading `=`; string
literals in single quotes; `+` concatenates strings / adds numbers; `!` negates a
Boolean; `==`, `||`, `<` comparisons; `=if(cond) then 'a' else 'b' endif` with
`else if` chains; **Ctrl+Space** code completion in the editor.

**Internal variables** (instantiation):

| Variable | Type | Meaning |
|---|---|---|
| `_elementindex_` | Integer | position of the element in the element list |
| `_index_` | Integer | index of the current instance, **0‑based** |
| `_count_` | Integer | total number of instances |
| `_first_` / `_last_` | Boolean | true on first / last instance |
| `_even_` | Boolean | true on even indices |

Worked idioms from the training solutions: `='MA'+index`,
`=_elementindex_+1`, `=_index_+1<_count_` (power line continues except on last axis),
`=((_index_+1)==2)||((_index_+1)==3)` (option only on axes 2 and 3).

**Three library‑design methods**: *additive* (minimal fragment TG + add MTs),
*subtractive* (maximal TG; hide elements via Status), *instantiation* ("smart" — one
TG instantiated N times; leave a called variable **empty** to surface it as a Project
Builder input per instance; instances added with the `+` button in Project Builder).

**Synchronization** ⚠️: after editing macros in the P8 macro project, you must
**Synchronize (update) the whole library** in the Designer or eBUILD keeps using stale
macro copies — the library holds copies, not live references. (Same family of trap as
the script reload in §2.)

**Placeholders** ⚠️: the **placeholder object name is the identity** for variable
assignment — renaming a placeholder afterwards **resets its variable assignment** in
eBUILD. Identically‑named placeholders group together in the Designer.

---

## 2b. Library authoring rules — the eBUILD Style Guide 📘

Source: *eBUILD Library Rules* (EPLAN Style Guide, release 2020‑09‑21, written for
platform 2.9 SP1 — conventions, not APIs, so still applicable). Matters to us when
reading/diagnosing the macro project a configurator is built on.

**Library naming**: `eBUILD-Library_<VC>_<LD>_<LC>_<SC>` — Version Code (P8 version,
e.g. `V29SP1`), Library Description, Language Code (`de-DE` — only needed if selection
lists are language‑bound), Standard Code (`IEC`/`NFPA`…, must match the master data).
Project description property `<10011>` carries `{{eBUILD Library: …}}`.

**Hard rules** (because nothing can be assumed about the target project): no new
layers, no own user supplementary fields, no new named display‑property arrangements;
all non‑translatable texts in English; think hard before using "From layer" graphical
formats.

**Macro boxes**:
- eBUILD macro boxes are needed **only in the library project** — set **"Also insert
  macro box: No"** so they don't land in the generated target project. (Trick: with
  *Type of usage: Referencing* the option is greyed — temporarily set *Defining*, set
  the option, set back.)
- Macros placed by eBUILD **cannot be meaningfully updated** by P8's *Update macros…* —
  another reason the boxes shouldn't survive into the target project.
- **Never modify existing parts macros** to add placeholders — wrap them in a **nested
  macro box** (outer box for eBUILD with the placeholders; inner box keeps the original
  parts macro updatable, placeholders associate to the nested macro's functions).

**Placeholder taxonomy** (the Style Guide's three use cases + one opt‑out):

| Type | Naming | Kept after generation? |
|---|---|---|
| Generation‑only (fills properties, no value sets) | `[Purpose]`, identical names group in Designer | No |
| Permanent value‑set control (user re‑selects value sets later) | `#<SEL_[Purpose]>` | **Yes** |
| Temporary value‑set control (value sets just to bundle variables) | `#<SEL_[Purpose]>`, description `[generation only]` | No |
| Ignored by eBUILD | any — deactivate **"Use placeholders in EPLAN Cogineer"** on the placeholder | n/a |

**CV naming convention** (System Hungarian — justified because a CV's type can never
be changed after creation): prefixes `n` Integer, `b` Boolean, `s` String
(non‑translatable), `sml` String (multilanguage/translatable), `d` Double; `sel_`/`Sel_`
prefix for pre‑defined value lists (`sel_sMachineType`); suffix `ProjProp` marks a
**project**‑property variable (no suffix ⇒ function property). Common abbreviations:
FT (function text), HL/ML (higher‑level function / mounting location), DocType, DT
(device tag), IP (interruption point), Desig, Descr, Conn/ConnP, Opt, Txt, PageDescr;
two‑digit counters for repeats (`sConnP_Desig_01`).

**MT/TG naming**: prefix `MT_` / `TG_` — the Designer has **no search/filter**, names
are the only navigation.

**`$(DOC)` transfer** 📘: files in the **base project's** `$(DOC)` directory are
transferred to the **target project's** `$(DOC)` when generating — i.e. our log/export
folder is also eBUILD's sanctioned channel for shipping files into generated projects.

**Markdown/info file**: written in HTML; should cover Description,
Prerequisites/Recommendations, Release Notes.

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
  it breaks the binding. (Project memory + research agree — and now the **official
  trainingsbook confirms it verbatim**: "The ProjectName is automatically filled by
  eBUILD and it is not necessary to transfer it as a parameter into Script‑Typicals.")
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

📘 From the trainingsbook, the Script‑Typical panel in the Designer is: **Status**
(formula‑controlled, like a macro — a script can be conditionally skipped), **Script**
(content **preview only — scripts cannot be edited in the Designer**; re‑upload the
`.cs` to change it), **Parameters** (fixed values or formulas over configuration
variables), **References**. The training course wires its PDF‑export script by creating
a CV (e.g. `PDFEXPORTFILE`) on the TG and binding it to the script parameter — exactly
the §5 pattern. It also warns to **copy/paste parameter names** into the Designer to
avoid case/spelling drift.

📘 **Official example Script‑Typicals exist in eBUILD Free** — library
`3.-GEN-eBUILD-Script-Examples_en-US_mm`, with sources installed under
`…\EPLAN\Data\Scripts\EPL\eBUILD\` (e.g. `eBUILD_ExportPDF.cs`,
`eBUILD_ReNumberTerminals.cs`, and the training's `eBUILD_HalloWelt.cs` /
`eBUILD_SayMyName.cs`). Worth diffing against our scripts — they are EPLAN's own
reference implementations of the same contract.

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

### ⚠️ Terminal (re)numbering is structurally broken in generated projects 📘

From the trainingsbook (bonus chapter, stated as a known limitation): **eBUILD places
macros with insertion mode "Do not modify"**. With that mode, *Terminal: Device
position* and *Sort code (terminal/pin)* do **not** carry over from one inserted macro
to the next — so terminals from repeated macros can't be renumbered correctly
afterwards, **neither by a script nor by P8's "Number terminals" function**. The
training's own `eBUILD_ReNumberTerminals.cs` Script‑Typical hits this wall. Device tags
and wire numbers (our §8 recipe) are unaffected. If correct ascending terminal
numbering matters, design it into the macros instead (one macro per terminal count, or
placeholder formulas — the trainingsbook's "approach 2"/"approach 1").

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
> 📘 Further confirmation: EPLAN's own `eBUILD_ExportPDF.cs` (official examples library,
> §5) does PDF export as a Script‑Typical — in‑context PDF export is *supported*, not a
> hack.

### Fallback: interactive finalize ✅
[FinalizeProject_Manual.cs](../FinalizeProject_Manual.cs) does the same label + PDF
exports but as an **interactive** `[Start] Run()` you trigger via `Scripts ▸ Run…` after
generation (resolving the project with `SelectionSet`). Kept as insurance in case PDF
export ever regresses in‑context; **not wired into eBuild.**

---

## 10. Headless / batch generation — "Silent mode" 📘 (now documented!)

The trainingsbook (ch. 6) documents official **silent generation**: a batch file runs
the generation **without opening EPLAN / the Project Builder UI**. The batch invokes an
executable with these parameters (each documented in the course; `^` is just cmd.exe
line continuation):

| Parameter | Meaning |
|---|---|
| file path | path + name of the executable to run silently |
| **token** | a **Silent‑mode token** identifying you to the software (obtained from the eBUILD UI) |
| library | name of the library containing the element to generate |
| configurator | name of the configurator |
| storage location | where the target project goes (only for a **new** target project) |
| target project | name of the target project |
| template | project template (only for a new target project) |
| configuration file | path to the **XML or XLSX** file with the configuration values |
| overwrite | `1` = overwrite an existing project, `0` = don't |

⚠️ Exact executable name/switch spellings aren't captured in the slide text (they're in
the screenshots) — pull them from the training batch file
`…\Attendee_Material_Part_03-06\GenerateProject_wSilentMode.bat` before building a
pipeline. The older `eplan.exe /NoSplash /Frame:"0" /Auto …` framing from web research
is a different (action‑level) mechanism and remains unverified.

### Configuration via XML/XLSX 📘
- Project Builder configurations can be **exported to / imported from XLSX or XML** —
  the "Excel‑Configurator" pattern: export, set values in Excel, re‑import, generate.
  This is also the input format silent mode consumes.
- Whole **libraries export/import as `.ela` files** (the training uses this to
  snapshot/rename libraries between exercises).

### EEC One → eBUILD 📘
A **File Import Feature Add‑in** (download: eplan‑software.com ▸ downloads ▸
eplan‑cogineer) lets the Project Builder build a project from an **EEC One XLSX
export**. Setup: register the add‑in, then create a **Personal Access Token** with your
EPLAN ID (organization must hold the eBUILD product; token shown once) and store it in
the Project Builder.

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
11. ⚠️ **Check action `bool` returns + verify side effects on disk** — failures are
    silent.
12. 📘 **Terminal renumbering can't be fixed post‑generation** — eBUILD inserts macros
    with "Do not modify", losing terminal device position / sort code (§8). Solve it in
    the macro design, not in a script.
13. 📘 **Synchronize the library after editing macros in P8** — eBUILD works on copies;
    un‑synced libraries generate stale macros (§2a).
14. 📘 **Don't rename placeholder objects** — the name is the assignment key; renaming
    resets the variable binding in the Designer (§2a/§2b).

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
  method args in declared order, with expected types (§5). The trainingsbook's
  `eBUILD_SayMyName.cs` exercise demonstrates exactly this — partially de‑risked, still
  unverified in our setup.
- 📘 **Headless invocation — ANSWERED in principle (§10):** official "silent mode"
  exists (batch + token + library/configurator + XML/XLSX config). Remaining unknown:
  the exact executable/switch spellings (in the training `.bat`, not the slide text)
  and whether our Script‑Typicals run identically in a silent run.
- 📘 **Multi‑script ordering & failure isolation** — does one failing Script‑Typical
  stop the rest?

---

## 14. Sources

- EPLAN Help / Infoportal — eBuild "Script‑Typicals: Basics", "Project Builder: Basics",
  action references (`renumber`, `export`, `label`), command‑line parameters
  (`eplan.help`).
- **eBUILD training material** (local PDFs, read 2026‑06‑09):
  `…\Downloads\Fichiers (5)\eBuild_Training\eBuild_Training\` —
  *3‑00 eBUILD Trainingsbook V2* (§2a, §5, §8 terminal caveat, §10 silent mode/EEC One)
  and *eBUILD Library Rules / Style Guide 2020‑09‑21* (§2b). Training attendee material
  (`Attendee_Material_*`) referenced by the book includes the silent‑mode `.bat` and the
  example macro project `[EES]_Master_Macro_project_EES_Stacking_System_Part2.zw1`.
- **EPLAN Consulting Macro Utility V2.0.1** (macro‑authoring script add‑on + manual) —
  patterns folded into the scripting reference §5a/§18a.
- `github.com/musray/PDFPerLocation`; Suplanus EPLAN scripting guide.
- This repo's eBuild Script‑Typicals + project memory note
  `eplan-ebuild-script-context` (every ✅ here).

> ⚠️ Treat 📘/⚠️ items as leads, not facts — especially the "no DataModel" and "cloud
> SaaS / headless" claims, which we have **not** verified and which partly conflict with
> how our local pipeline actually behaves.
