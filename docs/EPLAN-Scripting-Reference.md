# EPLAN Electric P8 — C# Scripting Reference

A working reference for writing C# scripts that run inside EPLAN Electric P8 (the
`Utilities ▸ Scripts ▸ Run…` mechanism and eBuild Script‑Typicals). Companion doc:
[eBuild / Automated Project Generation](eBuild-Automated-Project-Generation-Reference.md).

## How to read this document

Every non‑obvious claim is tagged with how much we trust it:

- ✅ **Confirmed** — exercised in this repo's working scripts or recorded in the
  project memory. Safe to rely on.
- 📘 **From docs** — from EPLAN Help / community research, not yet run in our own
  code. Probably right; verify the exact spelling/signature before leaning on it.
- ⚠️ **Caution** — contested, version‑dependent, or a known trap.

When ✅ and 📘 disagree, **✅ wins** — the web research guessed at several API
shapes that our own code contradicts. Those corrections are called out inline.

Repo scripts referenced throughout (all in the parent folder):
[PageNavi_ContextMenu_OpenFolders.cs](../PageNavi_ContextMenu_OpenFolders.cs),
[PostGenerationNumbering.cs](../PostGenerationNumbering.cs),
[PostGenerationNumbering_eBuild.cs](../PostGenerationNumbering_eBuild.cs),
[PostGenerationExports_eBuild.cs](../PostGenerationExports_eBuild.cs),
[FinalizeProject_Manual.cs](../FinalizeProject_Manual.cs),
[ExportEngravingData.cs](../ExportEngravingData.cs).

---

## 0. CRITICAL — what a simple script may reference ⚠️ (learned the hard way, 2026‑06‑08)

A **simple script** (anything run via `Utilities ▸ Scripts ▸ Run…` *or* an eBuild
Script‑Typical) is compiled by EPLAN against a **fixed, small set of assemblies**. On
EPLAN build 25625 the usable set is:

> `System` (+ core sub‑namespaces `System.IO`, `System.Text`, `System.Reflection`,
> `System.Collections.Generic`), `System.Xml`, `System.Drawing`,
> `System.Windows.Forms`, `Eplan.EplApi.Base`, `Eplan.EplApi.ApplicationFramework`,
> `Eplan.EplApi.Gui`, `Eplan.EplApi.Scripting`

**You CANNOT reference `Eplan.EplApi.DataModel`, `Eplan.EplApi.DataModel.Filters`, or
`Eplan.EplApi.HEServices` in a simple script.** A `using` for any of them fails to
compile with `CS0234 "the type or namespace name 'DataModel' does not exist in the
namespace 'Eplan.EplApi'"`. This is by design — the official EPLAN help states a script
"cannot reference additional assemblies." (Confirmed when `ValidateApi.cs` first failed.)

Consequences:
- The whole **object model** — `Project`, `Page`, `Function`, `Connection`,
  `PropertyValue`, `DMObjectsFinder`, `SelectionSet` — is **off‑limits to a direct
  `using`** in a simple script. §6 covers the three ways around this.
- EPLAN **auto‑injects** `using System;`, `using Eplan.EplApi.ApplicationFramework;`,
  `using Eplan.EplApi.Base;`, `using Eplan.EplApi.Scripting;`. **Do not re‑declare them**
  (gives a `CS0105` "appeared previously" warning). You get those namespaces and their
  types — `CommandLineInterpreter`, `ActionCallingContext`, `Decider`, `MultiLangString`,
  `Languages`, `ISOCode`, `PathMap`, the `[Start]` attribute — for free.
- ⚠️ `Settings` exists in **both** `Eplan.EplApi.Base` and
  `Eplan.EplApi.ApplicationFramework`; both are auto‑imported, so the bare name is
  **ambiguous** — fully‑qualify `Eplan.EplApi.Base.Settings`.
- **This corrects earlier ✅ grades below.** [ExportEngravingData.cs](../ExportEngravingData.cs)
  and [FinalizeProject_Manual.cs](../FinalizeProject_Manual.cs) `using` `DataModel`/
  `HEServices`, so they **never compiled as simple scripts** — their object‑model usage
  was *documented, not run*. Treat every `DataModel`/`SelectionSet` snippet below as
  🧰 **reflection‑ or add‑in‑only** (§6), not drop‑in simple‑script code.

**To get project data without the object model, use actions** (fully available):
`selectionset` (`TYPE=PROJECT` → project path; `TYPE=PAGES` → selected pages),
`GetProjectProperty` (`id`/`index` → `value`), plus `label`/`export`/`renumber` (§4–§5).
This is exactly why the Suplanus standalone scripts lean on actions. The other two routes
to the object model (reflection; a compiled add‑in) are in §6.

---

## 1. Anatomy of a script

EPLAN compiles a `.cs` file on load. A script is a plain public class; entry points
are public methods marked with EPLAN attributes. There is **no `Main`**.

```csharp
//[C#]                                  // optional language hint EPLAN recognizes
// System, Eplan.EplApi.ApplicationFramework, Eplan.EplApi.Base and
// Eplan.EplApi.Scripting are AUTO-INJECTED (§0) — omit them. Add only what EPLAN does
// not inject and that a simple script is ALLOWED to use:
using System.IO;                           // Path, File, Directory
using System.Text;                         // StringBuilder
// using Eplan.EplApi.DataModel;           // ✗ WON'T COMPILE in a simple script (§0)

public class MyScript
{
    [Start]                                // entry point: shows under Scripts ▸ Run…
    public void Run()
    {
        // CommandLineInterpreter, ActionCallingContext, Decider, PathMap … all usable
        // here via the auto-injected usings — no `using` needed.
    }
}
```

Notes:
- ✅ The class is instantiated by EPLAN; instance methods are fine. Statics work too
  (see the `global_GuiLanguage` field in
  [PageNavi_ContextMenu_OpenFolders.cs:20](../PageNavi_ContextMenu_OpenFolders.cs)).
- ✅ You may declare **multiple** attributed methods in one class/file (e.g. an action
  *and* a menu that calls it — that whole pattern is one file in the PageNavi script).
- ⚠️ EPLAN caches the compiled script. **After every edit you must reload it** (re‑run
  from `Scripts ▸ Run…`, or in eBuild re‑select the file in the Designer). This bites
  constantly — see the version‑stamp discipline in §13.
- 📘 Reference additional assemblies with a header comment line when a type lives
  outside the default set, e.g. `//#reference System.Windows.Forms.dll`. The PageNavi
  script uses `System.Windows.Forms` and `System.IO` directly, so the common ones are
  available by default.

---

## 2. Entry‑point attributes

All live in `Eplan.EplApi.Scripting` (the attribute classes) and are recognized by the
script host.

### `[Start]` ✅
The everyday entry point. Two shapes, and **which one you use is the single most
important decision in the script**:

```csharp
[Start] public void Run() { … }                       // interactive: Scripts ▸ Run…
[Start] public void Run(string ProjectName) { … }      // eBuild: ProjectName injected
```

- ✅ **No‑arg form** runs in the interactive GUI. Use `SelectionSet` to find the
  project the user has selected (§9). This is how
  [ExportEngravingData.cs:33](../ExportEngravingData.cs) and
  [FinalizeProject_Manual.cs:32](../FinalizeProject_Manual.cs) work.
- ✅ **`string ProjectName` form** is called automatically by the eBuild Project
  Builder, which passes the full path to the generated `.elk`. **Do not** also declare
  `ProjectName` as a parameter in the Designer — it is reserved and always injected.
  See [PostGenerationNumbering_eBuild.cs:24](../PostGenerationNumbering_eBuild.cs).
- ✅ A common pattern keeps both and comments one out, so the same logic ships to both
  contexts ([PostGenerationNumbering.cs:29‑42](../PostGenerationNumbering.cs)).
- 📘 Extra parameters after `ProjectName` can be bound to Designer configuration
  variables (`public void Run(string ProjectName, string Voltage)`). Covered in the
  eBuild doc §5 — **untested in our code.**

### `[DeclareAction("Name")]` ✅
Registers a named action callable from menus, other scripts, or the command line. The
method **may take parameters**, supplied as `/Key:Value` pairs in the call string.

```csharp
[DeclareAction("OpenFolder")]
public void XOpenFolder(string FolderName)   // bound from "OpenFolder /FolderName:…"
{ … }
```
Proven in [PageNavi_ContextMenu_OpenFolders.cs:23‑24](../PageNavi_ContextMenu_OpenFolders.cs),
invoked from a menu item as `"OpenFolder /FolderName:$(PROJECTPATH)"`.

### `[DeclareMenu]` ✅
Marks the method that builds menu / context‑menu items at load time.

```csharp
[DeclareMenu()]
public void CreateMenu() { … }   // construct menu only — no dialogs, no work here
```
⚠️ Build menu structure only; don't do real work or show dialogs in here. See
[PageNavi_ContextMenu_OpenFolders.cs:45](../PageNavi_ContextMenu_OpenFolders.cs).

### `[DeclareEventHandler]` 📘 (Suplanus — verbatim example, not yet run here)
Runs a method when an EPLAN event fires. The event string is
`onActionStart.String.<ActionName>` or `onActionEnd.String.<ActionName>`:
```csharp
using Eplan.EplApi.Scripting;
[DeclareEventHandler("onActionStart.String.XPrjActionProjectClose")]
public void Function() { … }     // fires as a project starts to close
```
💡 **Discover event/action names:** press **Ctrl+\\** in EPLAN right after doing the
thing you want to hook — the diagnostics dialog shows the internal action name to wrap
in `onActionStart.String.…`. (Source: Suplanus attributes page.)

### `[DeclareRegister]` / `[DeclareUnregister]` 📘 (Suplanus — verbatim)
Run once when the script add‑in is loaded / unloaded — use for setup that should persist
(e.g. enabling a setting, registering a ribbon). Both are `public void` no‑arg:
```csharp
[DeclareRegister]   public void Register()   { … }   // on load
[DeclareUnregister] public void UnRegister() { … }   // on unload
```

---

## 3. Assemblies & namespaces

| Namespace | What you use from it | Status |
|---|---|---|
| `Eplan.EplApi.ApplicationFramework` | `CommandLineInterpreter`, `ActionCallingContext` | ✅ |
| `Eplan.EplApi.Base` | `Decider`, `EnumDecisionType`, `EnumDecisionReturn`, `MultiLangString`, `Languages`, `ISOCode`, `PathMap` | ✅ |
| `Eplan.EplApi.DataModel` | `Project`, `Page`, `Function`, `Connection`, `PropertyValue`, `AnyPropertyId`, `Properties.*`, `DMObjectsFinder` | ⚠️ **NOT in simple scripts** — add‑in/reflection only (§0, §6) |
| `Eplan.EplApi.DataModel.Filters` | `FunctionsFilter`, `PagesFilter`, … | ⚠️ **NOT in simple scripts** (§0, §6) |
| `Eplan.EplApi.HEServices` | `SelectionSet` (and other high‑level services) | ⚠️ **NOT in simple scripts** (§0, §6) |
| `Eplan.EplApi.Gui` | `ContextMenu`, `ContextMenuLocation`, ribbon menus | ✅ |
| `Eplan.EplApi.Scripting` | the entry‑point attributes (auto‑injected) | ✅ |

⚠️ **`SelectionSet` is in `Eplan.EplApi.HEServices`** — but that assembly is **not
referenceable in a simple script** (§0), so `SelectionSet` is reflection/add‑in only.
(The web research wrongly placed it in `Eplan.EplApi.Base`.)

⚠️ `Decider`/`MultiLangString`/`PathMap` (`Eplan.EplApi.Base`) and
`CommandLineInterpreter`/`ActionCallingContext` (`Eplan.EplApi.ApplicationFramework`) are
**auto‑injected** (§0) — available without a `using`.

⚠️ A `[Start]` script that calls `Decider` needs `using Eplan.EplApi.Base;` — this was
a real omission once (noted in memory; fixed in FinalizeProject_Manual).

---

## 4. Executing built‑in EPLAN actions

Most heavy lifting is done by calling EPLAN's own **actions** (the same operations the
menus trigger). Two ways to call them, both via `CommandLineInterpreter`.

### 4a. String form — simple parameters ✅
```csharp
CommandLineInterpreter cli = new CommandLineInterpreter();
bool ok = cli.Execute("generate /TYPE:CONNECTIONS");
```
- Parameters are `/KEY:VALUE`, space‑separated.
- Quote any value containing spaces: `/CONFIGSCHEME:\"Summarized parts list\"`.
- ✅ Returns `bool` (true = action succeeded). ⚠️ It generally **does not throw** on an
  action‑level failure — it returns `false`. So *check the return value*; a silent
  `false` is the common failure mode.

### 4b. ActionCallingContext — path‑safe, structured ✅
Preferred whenever values contain file paths or special characters.
```csharp
ActionCallingContext ctx = new ActionCallingContext();
ctx.AddParameter("TYPE",        "PDFPROJECTSCHEME");
ctx.AddParameter("EXPORTSCHEME", "EPLAN_default_value");
ctx.AddParameter("EXPORTFILE",   pdfFile);
ctx.AddParameter("PROJECTNAME",  ProjectName);
bool ok = cli.Execute("export", ctx);
```
- ✅ `AddParameter(key, value)` — keys are the action's parameter names **without** the
  leading `/`, and are case‑sensitive.
- ⚠️ **Reuse:** a fresh context per call is the safe default, but reuse *does* work —
  Suplanus's `PagePdf` calls `AddParameter` (overwriting the same key) and `Execute`
  repeatedly on one context inside a loop. If you reuse, re‑set every key you care about.

### 4c. Reading values *back* from an action ✅ (Suplanus, verbatim)
Some actions return data through the context. Pattern: declare the action with an `out`
parameter; the caller reads it with `GetParameter(key, ref var)` after `Execute`:
```csharp
// Provider side — an action that returns a value:
[DeclareAction("GetProjectProperty")]
public void Action(string id, string index, out string value) { … }

// Caller side — read the returned "value":
string value = null;
ActionCallingContext acc = new ActionCallingContext();
acc.AddParameter("id", id);
acc.AddParameter("index", index);
new CommandLineInterpreter().Execute("GetProjectProperty", acc);
acc.GetParameter("value", ref value);     // ← out value comes back here
```
- ✅ `GetParameter(string key, ref string var)` retrieves an output parameter.
- ⚠️ **`GetProjectProperty` is NOT a built‑in action** — it's a *custom* `[DeclareAction]`
  from Suplanus's own script; without that script loaded it returns `false` (confirmed on
  2026.0.3 — every id returned `ok=False`). To read a project property without the object
  model you'd load such a custom action, or use reflection/an add‑in.
- ✅ **`selectionset TYPE=PROJECT` returns the full `.elk` path** in `PROJECT` (confirmed
  2026.0.3) — the reliable way to get the project path in a simple script. `TYPE=PAGES`
  returns `;`‑separated selected page names in `PAGES`.
- ✅ The **`selectionset`** action reports the current selection. `PagePdf` uses it to
  get selected page names:
  ```csharp
  string strPages = "";
  acc.AddParameter("TYPE", "PAGES");
  cli.Execute("selectionset", acc);
  acc.GetParameter("PAGES", ref strPages);          // "Page1;Page2;…"
  foreach (string p in strPages.Split(';')) { … }
  ```
- ✅ **`ActionManager`** is an alternative to `CommandLineInterpreter`:
  `new ActionManager().FindAction("selectionset")` returns an `Action` you can
  `.Execute(ctx)`. `selectionset` with `TYPE=PROJECT` returns the current project name in
  the `PROJECT` parameter. (Source: Suplanus `ExportProjectMissingTranslation`.)

### 4d. Reusable wrappers (lift these from our code) ✅
[PostGenerationExports_eBuild.cs:29‑61](../PostGenerationExports_eBuild.cs) defines two
helpers worth copying into any batch script — they log every result and never throw out
of the batch:

```csharp
// Execute a command STRING, log the result, never throw.
private bool Try(string label, string command)
{
    try { bool ok = _cli.Execute(command);
          _log.AppendLine(label.PadRight(30) + ": " + ok); return ok; }
    catch (Exception ex) {
          _log.AppendLine(label.PadRight(30) + ": EXCEPTION " + ex.Message); return false; }
}

// Execute an action via ActionCallingContext (key/value pairs), log result.
private bool TryCtx(string label, string action, params string[] kv)
{
    try {
        ActionCallingContext ctx = new ActionCallingContext();
        for (int i = 0; i + 1 < kv.Length; i += 2) ctx.AddParameter(kv[i], kv[i + 1]);
        bool ok = _cli.Execute(action, ctx);
        _log.AppendLine(label.PadRight(30) + ": " + ok); return ok;
    } catch (Exception ex) {
        _log.AppendLine(label.PadRight(30) + ": EXCEPTION " + ex.Message); return false; }
}
```

---

## 5. Key actions catalog

| Action | Purpose | Key parameters | Status |
|---|---|---|---|
| `generate /TYPE:CONNECTIONS` | (Re)build connection definition points | `/TYPE` | ✅ |
| `renumber /TYPE:DEVICES` | Renumber device tags | `/CONFIGSCHEME`, `/PROJECTNAME`, `/STARTVALUE`, `/STEPVALUE`, `/POSTNUMERATE`, `/ALSONUMERATEDBYPLC`, `/USESELECTION` | ✅ |
| `renumber /TYPE:CONNECTIONS` | Renumber wire numbers | `/CONFIGSCHEME`, `/PROJECTNAME` | ✅ |
| `label` | Export parts list / labels (xlsx, csv, xml…) | `/CONFIGSCHEME`, `/EXPORTFILE`, `/LANGUAGE`, `/PROJECTNAME` | ✅ |
| `export` | Export PDF / graphics / DXF / DWG | `TYPE`, `EXPORTSCHEME`, `EXPORTFILE`, `PROJECTNAME`, `LANGUAGE` | ✅ (PDF) |
| `XPrint` | Legacy print/PDF | — | ⚠️ prefer `export` |

### `generate` ✅
```csharp
cli.Execute("generate /TYPE:CONNECTIONS");   // needed before renumbering connections
```
⚠️ `generate /TYPE:NUMBERING` **does not exist** (web research flagged this as a common
mistake). Numbering is the separate `renumber` action.

### `renumber` ✅
```csharp
// Devices — whole project, renumber everything (not just "?")
string devCmd =
    "renumber /TYPE:DEVICES /CONFIGSCHEME:\"ECLIPSE ROW_IDENTIFIER\" " +
    "/PROJECTNAME:\"" + ProjectName + "\" " +
    "/STARTVALUE:1 /STEPVALUE:1 /ALSONUMERATEDBYPLC:0 /POSTNUMERATE:0";
cli.Execute(devCmd);

// Connections (wire numbers)
cli.Execute("renumber /TYPE:CONNECTIONS " +
    "/CONFIGSCHEME:\"Eclipse NFPA Standard without PLC address\" " +
    "/PROJECTNAME:\"" + ProjectName + "\"");
```
- ✅ `/POSTNUMERATE:0` renumbers **all** items; `/POSTNUMERATE:1` only those marked `?`.
- ✅ `/CONFIGSCHEME` is **case‑sensitive** and must match the project's scheme name
  exactly, or the action returns `false` and does nothing.
- ⚠️ **`/PROJECTNAME` is mandatory in the eBuild context.** Without it the action finds
  no project and silently no‑ops (`USESELECTION:0` matches nothing because nothing is
  selected). This was the fix that made device + connection renumbering work
  unattended. In the *interactive* context you can omit `/PROJECTNAME` and it acts on
  the selected project instead. See [PostGenerationNumbering.cs:58‑67](../PostGenerationNumbering.cs)
  (interactive) vs [PostGenerationNumbering_eBuild.cs:41‑52](../PostGenerationNumbering_eBuild.cs) (eBuild).

### `label` (parts list) ✅
```csharp
cli.Execute(
    "label /CONFIGSCHEME:\"Summarized parts list\" " +
    "/EXPORTFILE:\"" + partsFile + "\" /LANGUAGE:en_US " +
    "/PROJECTNAME:\"" + ProjectName + "\"");
```
✅ Works in **both** interactive and eBuild contexts (it uses the data/label engine, not
the print engine). [PostGenerationExports_eBuild.cs:81‑84](../PostGenerationExports_eBuild.cs).

### `export` (PDF) ✅ — with two real traps
```csharp
ActionCallingContext ctx = new ActionCallingContext();
ctx.AddParameter("TYPE",        "PDFPROJECTSCHEME");   // whole project
ctx.AddParameter("EXPORTSCHEME", "EPLAN_default_value"); // ⚠ scheme NAME, not the label
ctx.AddParameter("EXPORTFILE",   pdfFile);
ctx.AddParameter("PROJECTNAME",  ProjectName);
cli.Execute("export", ctx);
```
Two traps we already hit (both ✅ resolved, recorded in memory):
1. ⚠️ **`TYPE` must be a valid value** — `PDFPROJECTSCHEME` (whole project) or
   `PDFPAGESSCHEME` (pages). A bare `export` or `/TYPE:PDF` throws
   `S025019 "operation not supported. Parameter name: PDF"`. That error was a **bad
   parameter value, not a blocked context** — with the correct `TYPE`, PDF export
   *does* run inside eBuild generation.
2. ⚠️ **`EXPORTSCHEME` is the internal scheme NAME, not the dialog label.** The PDF
   dialog shows "Default" but the real name is **`EPLAN_default_value`** (EPLAN error
   `S029123` listed the valid name). Wrong name ⇒ silent failure.

✅ **Per‑page PDF** uses `TYPE=PDFPAGESSCHEME` plus a `PAGENAME` (one `export` call per
page). Suplanus's `PagePdf` independently confirms both this and the
`EPLAN_default_value` scheme name:
```csharp
acc.AddParameter("TYPE",        "PDFPAGESSCHEME");
acc.AddParameter("PAGENAME",    currentPage);        // from the selectionset action
acc.AddParameter("EXPORTFILE",  folder + "\\" + currentPage);
acc.AddParameter("EXPORTSCHEME", "EPLAN_default_value");
cli.Execute("export", acc);
```

📘 Other `export` `TYPE` values (from docs, unverified here): `GRAPHICPROJECT`
(TIF/GIF/PNG/JPG), `DXFPROJECT`, `DWGPROJECT`, `PXFPROJECT`. `LANGUAGE` is accepted but
optional for `export`.

✅ Always confirm the file actually landed — don't trust the return alone:
```csharp
_log.AppendLine("PDF file exists : " + File.Exists(pdfFile));
```

---

## 6. The DataModel object model — 🧰 reflection/add‑in only in simple scripts

> **Read §0 first.** `Eplan.EplApi.DataModel`/`HEServices` can't be referenced in a simple
> script, so **none of the snippets below compile via `Scripts ▸ Run…` as written.** Three
> ways to use the object model:
> 1. **Actions instead** (preferred) — project/page data via `selectionset` /
>    `GetProjectProperty`; mutate via `renumber`/`generate`/`label`/`export` (§4–§5).
> 2. **Reflection** 🧰 — the assemblies *are* loaded when a project is open, so reach them
>    via `Activator.CreateInstance` + `MethodInfo.Invoke` by string. `ValidateApi.cs` is
>    the "escape‑hatch test" that proves whether this works in your build.
> 3. **Compiled add‑in (.dll)** — a Visual Studio project with full references, loaded
>    into EPLAN; the only route with normal compile‑time binding.

The **shapes below are correct** (and validated by reflection in `ValidateApi`); they are
just not directly `using`‑able. This is how the object model looks in an **add‑in**, and
the chain to drive by reflection:

```csharp
// Add-in (compile-time) form — and the reflection target for a simple script:
Project project = new SelectionSet().GetCurrentProject(true);   // HEServices  (§9)
DMObjectsFinder finder = new DMObjectsFinder(project);          // DataModel
Function[] functions = finder.GetFunctions(new FunctionsFilter());  // DataModel.Filters

foreach (Function f in functions)
{
    string dt    = f.Name;          // full identifying device tag (use as a stable key)
    string vis   = f.VisibleName;   // displayed tag
    string page  = f.Page.Name;     // owning page's name
    PropertyValue pv = f.Properties[Properties.Function.FUNC_TEXT];
}
```
- 🧰 `Project.ProjectLinkFilePath` — full path to the `.elk` (add‑in/reflection). In a
  simple script, get the path instead from `selectionset TYPE=PROJECT` (§4c).
- 🧰 `DMObjectsFinder(project).GetFunctions(new FunctionsFilter())` — enumerate functions.
  `ValidateApi.cs` invokes exactly this chain by reflection.
- 🧰 `Function.Name`, `Function.VisibleName`, `Function.Page.Name` — wrap each in
  try/catch; they can throw on degenerate objects.

⚠️ [ExportEngravingData.cs](../ExportEngravingData.cs) was written against this object
model **as a simple script**, so it does **not compile** on build 25625 — it must be
rebuilt as an add‑in or via reflection once `ValidateApi` confirms the reflection route.

**Confirmed by reflection on EPLAN 2026.0.3** (`ValidateApi` run) — the DataModel /
HEServices assemblies *are* loaded (their DLLs carry a `u` suffix: `Eplan.EplApi.DataModelu`,
`…HEServicesu`), and:
- ✅ **present:** `DMObjectsFinder.GetFunctions` (3 overloads), `DMObjectsFinder.GetPages`,
  `DMObjectsFinder.GetConnections`, `Function.VisibleName`, `Project.ProjectName`,
  `Project.ProjectLinkFilePath`, `Connection` (type), `ActionManager`.
- ❌ **absent — web‑research inventions, do NOT use:** `Function.SymbolName`,
  `Connection.GetConnectedFunctions()`, `Page.Connections`.
- ❌ **Reflection is a DEAD END for the object model — DEFINITIVELY (ValidateApi v.3–v.5).**
  You can *reach the types* by reflection, but you can't get a `Project` **instance** to
  work with: the only project‑getter, `SelectionSet.GetCurrentProject(bool)`, **always**
  throws `NoLockingStepException S063110 ("Failed to generate … 'LockingStep'")` in a
  script — for `true` AND `false`, and **even after** `set_LockProjectByDefault(false)`.
  The script runtime can't create a LockingStep, which `GetCurrentProject` requires. So:
  > **The DataModel object model (`Project`/`Function`/properties) is ADD‑IN ONLY in a
  > simple script — for reads AND writes.** No reflection escape hatch.
- For the record, the real signatures the validator dug out (use these in an **add‑in**):
  `FunctionsFilter` is `Eplan.EplApi.DataModel.FunctionsFilter` (*not* `…Filters.…`);
  `DMObjectsFinder` has `ctor(Project)`/`ctor()`; read overload `GetFunctions(FunctionsFilter)
  → Function[]`. (One untried script route remains: `ProjectManager.OpenProject(path,…)` —
  it *might* open a project without a LockingStep; unverified.)
- ✅ **What a script CAN still do for data:** the **`label`/report action** (runs in the
  report engine, no `Project` handle, no lock) can export function properties — including
  engraving text — to CSV/XLSX. That's the script‑friendly route for read‑only extraction.

---

## 7. Properties system

EPLAN objects carry typed properties addressed by ID.

```csharp
PropertyValue pv = f.Properties[Properties.Function.FUNC_TEXT];   // by symbolic ID
if (!pv.IsEmpty) { string s = pv.ToString(); }                    // ✅ proven read path

PropertyValue part = f.Properties[Properties.Function.FUNC_ARTICLE_PARTNR, 1]; // indexed ✅
```

⚠️ **Multi-language properties** (`FUNC_TEXT`, `FUNC_GRAVINGTEXT`, …): `pv.ToString()`
returns EPLAN's **internal serialization** `lang@text;` — e.g. `??_??@XXXX-XX;`, where
`??_??` is the *all/unspecified-language* slot and `;` terminates each entry. For the
clean human value, convert to `MultiLangString` (implicit operator) and ask for the
display string (✅ confirmed in the EngravingData add-in):
```csharp
MultiLangString mls = pv;                       // op_Implicit PropertyValue -> MultiLangString
string text = mls.GetStringToDisplay(new Languages().GuiLanguage.GetNumber());  // "XXXX-XX"
```
To **write** one back, `Set` a `MultiLangString` (PropertyValue has **no public ctor**):
```csharp
var mls = new MultiLangString();
mls.AddString(ISOCode.Language.L_en_US, "XXXX-XX");
f.Properties[id].Set(mls);                       // Set(String)/Set(MultiLangString)/Set(int)…
```
- ✅ `PropertyValue.IsEmpty` — always check before converting.
- ✅ `PropertyValue.ToString()` — the conversion we use in production.
- ✅ **Indexed properties** use `Properties[id, index]`, and the index is **1‑based**
  (part number #1 is index `1`). [ExportEngravingData.cs:104‑113](../ExportEngravingData.cs).
- ✅ `AnyPropertyId` is the base type — handy for writing generic read helpers
  (`private static string Read(Function f, AnyPropertyId id)`).
- ❌ `PropertyValue.To<T>()` does **NOT exist** (confirmed absent on 2026.0.3 — the web
  research invented it). Use `ToString()` + `IsEmpty`, or `PropertyValue`'s implicit casts
  (`string s = pv;`, `int i = pv;`).

✅ **Defensive read helper** — properties throw more often than you'd like, so this
pattern (from ExportEngravingData) is the house style:
```csharp
private static string Read(Function f, AnyPropertyId id)
{
    try { PropertyValue pv = f.Properties[id];
          return pv.IsEmpty ? "" : pv.ToString(); }
    catch { return ""; }
}
```

### Property IDs we've confirmed (Function) ✅
Symbolic name is what the code uses; the numeric ID is what EPLAN error messages and
older docs cite — handy when cross‑referencing.

| Symbolic (`Properties.Function.*`) | Numeric | Meaning | Notes |
|---|---|---|---|
| `FUNC_TEXT` | 20011 | Function/device text | editable; MultiLangString underneath |
| `FUNC_MOUNTINGLOCATION` | 20024 | Mounting location | |
| `FUNC_GRAVINGTEXT` | 20025 | Engraving / nameplate text | editable |
| `FUNC_ARTICLE_PARTNR` | 20100 | Part number | **indexed** (1‑based) |

📘 Project‑level IDs (`Properties.Project.PROJECT_NAME`, `PROJECT_TITLE`, …) exist by the
same convention but we haven't exercised them.

---

## 8. SelectionSet — the interactive project handle

```csharp
using Eplan.EplApi.HEServices;   // ⚠ HEServices, not Base
using Eplan.EplApi.DataModel;

Project project = new SelectionSet().GetCurrentProject(true);  // true ⇒ throw if none
```
- ✅ Resolves the project the user has selected in the Pages navigator. The `true`
  argument makes it throw when nothing is selected — wrap it and tell the user to click
  the project node first ([ExportEngravingData.cs:36‑45](../ExportEngravingData.cs),
  [FinalizeProject_Manual.cs:38‑53](../FinalizeProject_Manual.cs)).
- ⚠️ **Interactive context only.** In eBuild there is no selection — use the injected
  `ProjectName` string instead (§2, and eBuild doc).
- ⚠️ Many interactive *actions* with no `/PROJECTNAME` act on the selected project, so
  for manual testing **single‑click the project node first** or they silently no‑op
  ([PostGenerationNumbering.cs:5‑10](../PostGenerationNumbering.cs)).

---

## 9. Dialogs & user feedback — `Decider`

The supported way to show a message or ask yes/no. ⚠️ **Interactive context only — never
in eBuild** (it will hang/fail unattended; log to a file there instead, §13 of eBuild doc).

```csharp
new Decider().Decide(
    EnumDecisionType.eOkDecision,     // dialog kind
    message,                          // body text
    "Title",
    EnumDecisionReturn.eOK,           // default button
    EnumDecisionReturn.eOK);          // (suppressed/return value)
```
Exact 5‑arg shape proven in [PostGenerationNumbering.cs:77‑82](../PostGenerationNumbering.cs)
and [ExportEngravingData.cs:148‑153](../ExportEngravingData.cs).

- ✅ `EnumDecisionType.eOkDecision` (info/OK). 📘 `eYesNoDecision`, `eYesNoCancelDecision`,
  `eOkCancelDecision` per docs.
- ✅ `EnumDecisionReturn.eOK`. 📘 `eYes`, `eNo`, `eCancel`. Capture the return to branch
  on Yes/No.

Our convention is a tiny `Tell(string)` wrapper around an `eOkDecision` so a silent
no‑op is never invisible.

---

## 10. Multi‑language text — `MultiLangString`, `Languages`, `ISOCode`

⚠️ **The web research got this API wrong.** The **confirmed** surface, straight from
[PageNavi_ContextMenu_OpenFolders.cs:20,50‑53](../PageNavi_ContextMenu_OpenFolders.cs):

```csharp
// Current GUI language as an ISOCode.Language:
ISOCode.Language gui = new Languages().GuiLanguage.GetNumber();   // ✅ not "CurrentLanguage"

MultiLangString mls = new MultiLangString();
mls.AddString(ISOCode.Language.L_de_DE, "…deutscher Text…");      // ✅ enum members L_xx_XX
mls.AddString(ISOCode.Language.L_en_US, "…english text…");
string shown = mls.GetStringToDisplay(gui);                       // ✅ display for a language
```
- ✅ Languages are the **enum** `ISOCode.Language.L_de_DE`, `…L_en_US`, … — not a string
  constructor like `new ISOCode.Language("de_DE")` (that form is 📘 unverified / likely
  wrong).
- ✅ `GetStringToDisplay(lang)` returns the best string for that language; it can come
  back empty, so fall back to a hard‑coded default (the PageNavi script does exactly
  this for each menu label).
- ✅ Property values that are multilingual (e.g. `FUNC_TEXT`) are `MultiLangString`
  underneath — relevant when you write text **back** (round‑trip import is a TODO noted
  in memory).

---

## 11. Context menus & ribbon — `Eplan.EplApi.Gui`

⚠️ **Also corrected vs. web research.** The **confirmed** API from
[PageNavi_ContextMenu_OpenFolders.cs:83‑90](../PageNavi_ContextMenu_OpenFolders.cs):

```csharp
// Where the menu attaches — set by PROPERTIES, not a constructor:
ContextMenuLocation loc = new ContextMenuLocation();
loc.DialogName      = "PmPageObjectTreeDialog";   // the host dialog
loc.ContextMenuName = "1007";                       // the menu id within it

// Build & add items — AddMenuItem takes the LOCATION first:
Eplan.EplApi.Gui.ContextMenu menu = new Eplan.EplApi.Gui.ContextMenu();   // no-arg ctor
menu.AddMenuItem(loc, menuText, "OpenFolder /FolderName:$(PROJECTPATH)", true, false);
menu.AddMenuItem(loc, menuText2, "OpenFolder /FolderName:$(DOC)", false, false);
```
Signature is `AddMenuItem(ContextMenuLocation, string text, string actionCommand,
bool startGroup, bool …)`. The action command is just an action call string (§4a), so a
menu item is glue between `[DeclareMenu]` and a `[DeclareAction]` in the same file.

✅ **`ContextMenuLocation` has two equivalent forms.** Our repo sets the properties
(`DialogName`/`ContextMenuName`); Suplanus's `PagePdf` uses the **constructor**
`new ContextMenuLocation("PmPageObjectTreeDialog", "1007")` — same dialog + menu IDs,
independently confirming both the location IDs *and* the 5‑arg `AddMenuItem(location,
text, action, bool, bool)` signature. Use whichever you like.

⚠️ Still **wrong** (research's invention): `new ContextMenu(location)` and the 3‑arg
`AddMenuItem("text","action",0)`. `ContextMenu` takes a no‑arg constructor; the location
goes to `AddMenuItem`. `DialogName`/`ContextMenuName` for *other* hosts must be
discovered per dialog (📘 — use the Ctrl+\\ trick / extended action list, §17).

---

## 12. Paths — `PathMap` and EPLAN variables

```csharp
// Resolve an EPLAN path variable at runtime:
string folder = Eplan.EplApi.Base.PathMap.SubstitutePath("$(PROJECTPATH)");   // ✅
```
- ✅ `PathMap.SubstitutePath("$(…)")` expands EPLAN path variables. The PageNavi script
  guards with `if (FolderName.StartsWith("$("))` before substituting
  ([PageNavi_ContextMenu_OpenFolders.cs:28‑31](../PageNavi_ContextMenu_OpenFolders.cs)).
- ✅ **Resolved values confirmed on EPLAN 2026.0.3** (`ValidateApi` run). `$(P)` =
  `$(PROJECTPATH)` = the project's `.edb` data folder:

  | Variable | Resolves to |
  |---|---|
  | `$(PROJECTNAME)` | project name only — `[EES]_…_V01` (no path, no extension) |
  | `$(PROJECTPATH)` = `$(P)` | `…\Projects\EPLANCA\[EES]_…_V01.edb` |
  | `$(DOC)` | `…\[EES]_…_V01.edb\DOC` — ⚠️ **`DOC`**, *not* the `DOCS` folder our scripts write |
  | `$(IMG)` | `…\[EES]_…_V01.edb\Images` |
  | `$(MD_SCRIPTS)` | `C:\Users\Public\Eplan\Data\Scripts\Massiv` |
  | `$(MD_PARTS)` | `C:\Users\Public\Eplan\Data\Parts\Massiv` |
  | `$(MD_MACROS)` | `C:\Users\Public\Eplan\Data\Macros\Massiv` |
  | `$(BIN)` | `C:\Program Files\EPLAN\Platform\2026.0.3\Bin` |
  | `$(EPLAN_VERSION)` | `2026.0.3` |

- ⚠️ The project's **real document dir is `$(DOC)` = `.edb\DOC`**; our export/log scripts
  instead create a custom `.edb\DOCS`. Both work — just don't confuse them.

✅ **Deriving the project's `DOCS` folder** (the house pattern for all output) doesn't
use PathMap at all — it transforms the `.elk` path to the `.edb` data folder:
```csharp
string docsPath = Path.Combine(Path.ChangeExtension(projectPath, ".edb"), "DOCS");
Directory.CreateDirectory(docsPath);
```
Used identically in every export/log script here.

---

## 13. Gotchas (the ones that actually cost us time)

1. ⚠️ **Reload after every edit.** EPLAN runs a *cached compile*. Stamp a version
   constant and print it to every log so you can prove which copy ran:
   ```csharp
   private const string ScriptVersion = "2026-06-05.11";
   log.AppendLine("Script version : " + ScriptVersion);
   ```
   Bump it on **every** change ([PostGenerationExports_eBuild.cs:18](../PostGenerationExports_eBuild.cs)).
2. ⚠️ **`renumber` needs `/PROJECTNAME` in eBuild**, or it silently no‑ops (§5).
3. ⚠️ **Scheme names are case‑sensitive and are the internal name** — the PDF "Default"
   label is really `EPLAN_default_value` (§5).
4. ⚠️ **Check the `bool` return** — actions fail by returning `false`, not throwing.
   Independently verify side effects (`File.Exists`) where you can.
5. ⚠️ **Interactive vs. eBuild**: `SelectionSet` and `Decider` are interactive‑only;
   `ProjectName` + file logging are the eBuild equivalents.
6. ⚠️ **Single‑click the project node** before running interactive numbering scripts.
7. ⚠️ **`XGedSaveProject` does not exist** in the eBuild context — eBuild saves the
   project itself (memory).
8. ⚠️ **`using Eplan.EplApi.Base;`** is required for `Decider`/`PathMap`/`MultiLangString`
   — easy to forget.
9. ⚠️ **API assemblies are version‑locked.** A script built against one EPLAN platform
   version may not load on another; match references to the installed version.

---

## 14. Reusable snippets

**DOCS path + file logging skeleton** (eBuild‑safe, no dialogs):
```csharp
string docsPath = Path.Combine(Path.ChangeExtension(ProjectName, ".edb"), "DOCS");
Directory.CreateDirectory(docsPath);
string logPath = Path.Combine(docsPath, "MyScript.log");
var log = new StringBuilder();
log.AppendLine("=== MyScript " + DateTime.Now + " ===");
log.AppendLine("Script version : " + ScriptVersion);
// … do work, appending results …
try { File.AppendAllText(logPath, log.ToString() + Environment.NewLine); } catch { }
```

**CSV field escaping** (newlines → literal `\n`, quotes doubled) —
[ExportEngravingData.cs:139‑146](../ExportEngravingData.cs):
```csharp
private static string Field(string s)
{
    if (s == null) s = "";
    s = s.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\\n");
    if (s.IndexOf('"') >= 0 || s.IndexOf(',') >= 0)
        s = "\"" + s.Replace("\"", "\"\"") + "\"";
    return s;
}
```

---

## 15. Settings — read & write

EPLAN exposes its entire settings tree to scripts via `Eplan.EplApi.Base.Settings`.
Keys are dotted paths, mostly under `USER.…`; the trailing `int` is an **index**.

✅ **Confirmed** (Suplanus `ExtendedActionList`, verbatim):
```csharp
var s = new Eplan.EplApi.Base.Settings();
bool on = s.GetBoolSetting("USER.ModalDialogs.XSdCustomToolbarComponent.ExtendedActionList", 0);
s.SetBoolSetting("USER.ModalDialogs.XSdCustomToolbarComponent.ExtendedActionList", true, 0);
```
📘 **From Suplanus index** (verify signatures): `GetStringSetting(key, index)` /
`SetStringSetting(key, value, index)`, `GetNumericSetting(key, index)` /
`SetNumericSetting(key, n, index)`, `ReadSettings(xmlPath)` to import a settings XML, and
the `XSettingsImport` action (params `Project`, `XmlFile`) for importing into a project
(combine with `PathMap` `$(MD_SCRIPTS)` / `$(P)`).

⚠️ Writing settings changes the user's EPLAN config **persistently**. If a change should
be temporary, enable it in `[DeclareRegister]` and restore it in `[DeclareUnregister]`.

## 16. Progress bars 📘 (Suplanus `PagePdf`, verbatim — interactive only)

```csharp
Progress p = new Progress("SimpleProgress");
p.SetAllowCancel(true);
p.SetAskOnCancel(true);
p.BeginPart(100, "");
p.ShowImmediately();
try {
    foreach (var item in work) {
        if (p.Canceled()) { p.EndPart(true); return; }
        // … do a unit of work …
    }
    p.EndPart(true);
} catch { p.EndPart(true); }
```
- `BeginPart(weight, text)` … `EndPart(true)` bracket a chunk of work; parts can nest so
  each takes a percentage of the whole. `Step(n)` advances; `Canceled()` polls Cancel.
- ⚠️ Shows UI → **never in eBuild** (§ eBuild doc).

## 17. Debugging scripts + the extended action list

**Extended action list** ✅ (Suplanus, verbatim) — the fastest way to discover action
names and their parameters: flip one setting and EPLAN surfaces every available action in
the UI.
```csharp
new Eplan.EplApi.Base.Settings()
    .SetBoolSetting("USER.ModalDialogs.XSdCustomToolbarComponent.ExtendedActionList", true, 0);
```
Pair it with **Ctrl+\\** (§2) to capture the exact internal action name behind any command
you click — between the two you can reverse‑engineer almost any action call.

**Attaching a debugger** 📘 (Suplanus index — the full page is paywalled; API names only):
- Enable script debugging via the setting `USER.EplanEplApiScriptLog.DebugScripts` (set in
  `[DeclareRegister]`, restore in `[DeclareUnregister]`).
- In code: `if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();`
  and trace with `System.Diagnostics.Debug.WriteLine(...)`.
- Workflow: enable the setting → attach Visual Studio to the running EPLAN process → hit
  the breakpoint. (Removes the reload‑and‑pray loop from §13.)

## 18. The Suplanus example library (free, on GitHub)

`github.com/Suplanus/EplanScriptingProjectBySuplanus` ("All scripts from Suplanus") is the
**free** source of real, runnable examples. The website's *expert* pages
(`eplan-scripting.suplanus.de` — Export, Settings, Parts, Debugging,
actions‑with‑values, …) **redirect to a paid login**, but the equivalent source lives in
this repo. Mirror: `github.com/mensong/EplanScriptingProject-EPlan-Examples`. Map:

| Topic | Folder(s) in the repo |
|---|---|
| PDF / print / export | `PagePdf`, `PdfExportAssistent`, `SendPdfEmail`, `LocationPrint`, `PrintPages`, `QuickprintContextMenu`, `ElkExportToDwg` |
| Settings | `ExtendedSettings`, `SetSetting/*`, `SetUserPaths`, `SettingsRemoveSelection` |
| Language / translation | `SelectLanguage`, `SetLanguageGui`, `Get*Languages`, `MultilanguageToolExamples/*`, `MultiLanguageString`, `TranslateLanguages`, `ExportProjectMissingTranslation` |
| Project properties / mgmt | `GetProjectProperty`, `UpdateProjectProperties`, `GetProjectnameOnPostOpen`, `OpenProjectAndSetPartsDb`, `ProjectHistory` |
| Parts / macros | `InsertUniversalPart3D`, `InsertPageMacro`, `UpdateMacro`, `SwapMacroFromMacroBox`, `CompressPartsDatabase` |
| Actions / menus / navigators | `ExecuteEplanAction`, `ExtendedActionList`, `DynamicMenu`, `ShowNavigators` |
| Connections / data | `AutomaticGeneratingConnections`, `ChangePLCMnemonics`, `ConnectionPointDesignationReverse` |
| Decider / UI | `DeciderClass`, `DeciderDisplayEnable`, `SetCursorColor`, `Watermark`, `InsertComment` |
| Script utilities | `GetCurrentLoadedScripts`, `GetCurrentScriptPath`, `LoadUnloadAllScripts`, `Eventlogger`, `ScriptTest` |

⚠️ Some files are VB (`.vb`), and several touch APIs that need verifying against your
platform version. High‑quality reference, not guaranteed drop‑in.

## 19. Worked examples in this repo

| Script | Demonstrates |
|---|---|
| [PageNavi_ContextMenu_OpenFolders.cs](../PageNavi_ContextMenu_OpenFolders.cs) | `[DeclareAction]` + `[DeclareMenu]`, `ContextMenu`/`ContextMenuLocation`, `MultiLangString`/`Languages`, `PathMap.SubstitutePath`, launching Explorer |
| [PostGenerationNumbering.cs](../PostGenerationNumbering.cs) | Interactive `[Start]`, `generate`/`renumber`, `Decider` summary, dual entry‑point pattern |
| [PostGenerationNumbering_eBuild.cs](../PostGenerationNumbering_eBuild.cs) | eBuild `[Start](string ProjectName)`, `/PROJECTNAME`, file logging, version stamp |
| [PostGenerationExports_eBuild.cs](../PostGenerationExports_eBuild.cs) | `Try`/`TryCtx` wrappers, `label` xlsx export, `export` PDF via `ActionCallingContext`, post‑hoc `File.Exists` check |
| [FinalizeProject_Manual.cs](../FinalizeProject_Manual.cs) | ⚠️ **won't compile as a simple script** (uses `SelectionSet`/`DataModel`, §0). Pattern reference only until rebuilt via reflection/add‑in |
| [ExportEngravingData.cs](../ExportEngravingData.cs) | ⚠️ **won't compile as a simple script** (uses `DMObjectsFinder`/`FunctionsFilter`/`SelectionSet`, §0). Logic is sound; needs reflection/add‑in rebuild |
| [ValidateApi.cs](../ValidateApi.cs) | **Validation harness** (interactive, read‑only): reflection probes of uncertain APIs + runtime probes of project/DataModel/properties/settings/paths/action‑return‑values; writes `DOCS\ValidateApi.log` |
| [ValidateApi_eBuild.cs](../ValidateApi_eBuild.cs) | **Validation probe** (Script‑Typical): does the DataModel work during eBuild generation? logs to `DOCS\ValidateApi_eBuild.log` |

---

## 20. Sources

- EPLAN Help / Infoportal — Scripting & API (`eplan.help`): action references
  (`renumber`, `export`, `label`), namespace docs, `PathMap.SubstitutePath`.
- **Suplanus** — Johann Weiher's EPLAN Scripting guide `eplan-scripting.suplanus.de`
  (beginner pages free; **expert pages paywalled** behind a login). The **free** example
  source is the GitHub repo `Suplanus/EplanScriptingProjectBySuplanus` (verbatim code for
  `ExtendedActionList`, `PagePdf`, `GetProjectProperty` quoted above came from there) and
  the attributes page (event handlers, register/unregister, the Ctrl+\\ trick).
- `github.com/musray/PDFPerLocation` — PDF export per location example.
- This repo's working scripts + project memory note `eplan-ebuild-script-context`
  (the ✅ items).

> ⚠️ Doc‑sourced (📘) items are an aid, not gospel — the web research mis‑stated the
> `MultiLangString`, `ContextMenu`, and `SelectionSet`‑namespace APIs, all corrected
> above from our own code. When in doubt, trust the running scripts.
