// ValidateApi.cs
//
// INTERACTIVE validation harness for EPLAN "simple scripts" (Utilities > Scripts > Run).
//
// !!! HARD CONSTRAINT discovered 2026-06-08 (EPLAN build 25625) !!!
// A simple script may ONLY use these assemblies:
//     System, System.Xml, System.Drawing, System.Windows.Forms,
//     Eplan.EplApi.Base, Eplan.EplApi.ApplicationFramework,
//     Eplan.EplApi.Gui, Eplan.EplApi.Scripting
// You CANNOT reference Eplan.EplApi.DataModel / .DataModel.Filters / .HEServices
//   -> doing so fails to compile with CS0234 "namespace does not exist".
// EPLAN also AUTO-INJECTS `using System;`, `using Eplan.EplApi.ApplicationFramework;`,
//   `using Eplan.EplApi.Base;`, `using Eplan.EplApi.Scripting;` — re-declaring them gives
//   a CS0105 warning (harmless, but we omit them).
//
// Therefore this script reaches the DataModel ONLY by REFLECTION (those assemblies are
// loaded in-process when a project is open; we just can't bind to them at compile time).
// Whether that reflection escape hatch works is itself the most important thing we test —
// it decides how ExportEngravingData / FinalizeProject must be rewritten.
//
// SAFE TO RUN: read-only. Only writes are a no-op Settings write-back and the log file.
// Optional export probes (PDF to %TEMP%) are OFF by default — flip RunExportProbes.
//
// HOW TO RUN: select the project node in the Pages navigator, then
//   Utilities > Scripts > Run... -> ValidateApi -> Run().  Read the summary; full detail
//   is in the named log file.

using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;

public class ValidateApi
{
    private const string ScriptVersion = "2026-06-08.5";

    // Flip to true to also exercise a whole-project PDF export to %TEMP%.
    private static readonly bool RunExportProbes = false;

    private readonly StringBuilder _log = new StringBuilder();
    private int _pass, _fail, _info;

    [Start]
    public void Run()
    {
        _log.AppendLine("=== ValidateApi " + System.DateTime.Now + " ===");
        _log.AppendLine("Script version : " + ScriptVersion);

        SectionEnvironment();
        SectionRuntimeApi();
        string projectPath = SectionActionsAndProject();
        SectionDataModelViaReflection();
        SectionPaths();
        SectionLanguages();
        SectionSettings();
        SectionExports(projectPath);

        string logPath = WriteLog(projectPath);
        string summary =
            "ValidateApi " + ScriptVersion + "\n\n" +
            "PASS : " + _pass + "\n" +
            "FAIL : " + _fail + "\n" +
            "INFO : " + _info + "\n\n" +
            "Full detail written to:\n" + logPath + "\n\n" +
            "Key question answered in the log: can we reach the DataModel by reflection?";
        Tell(summary);
    }

    // =====================================================================

    private void SectionEnvironment()
    {
        Head("ENVIRONMENT — loaded EPLAN assemblies (reflection feasibility)");
        List<string> names = new List<string>();
        foreach (Assembly asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                AssemblyName an = asm.GetName();
                if (an.Name != null && an.Name.StartsWith("Eplan"))
                    names.Add(an.Name + "  " + an.Version);
            }
            catch { }
        }
        names.Sort();
        foreach (string s in names) Info("assembly", s);

        // Which key assemblies are LOADED (=> reflection can reach them):
        Info("DataModel loaded",  (FindType("Eplan.EplApi.DataModel.Project") != null).ToString());
        Info("HEServices loaded", (FindType("Eplan.EplApi.HEServices.SelectionSet") != null).ToString());
        Info("Filters loaded",    (FindType("Eplan.EplApi.DataModel.Filters.FunctionsFilter") != null).ToString());
        Info("Gui loaded",        (FindType("Eplan.EplApi.Gui.ContextMenu") != null).ToString());
    }

    private void SectionRuntimeApi()
    {
        Head("RUNTIME API — type/member presence (note: present != compile-referenceable)");

        // Available-to-scripts assemblies (Base / ApplicationFramework) — should be present:
        CheckMethod("ActionCallingContext.GetParameter", "Eplan.EplApi.ApplicationFramework.ActionCallingContext", "GetParameter", true);
        CheckMethod("ActionCallingContext.AddParameter", "Eplan.EplApi.ApplicationFramework.ActionCallingContext", "AddParameter", true);
        CheckMethod("MultiLangString.GetStringToDisplay", "Eplan.EplApi.Base.MultiLangString", "GetStringToDisplay", true);
        CheckMethod("PathMap.SubstitutePath", "Eplan.EplApi.Base.PathMap", "SubstitutePath", true);
        CheckMethod("Settings.GetBoolSetting", "Eplan.EplApi.Base.Settings", "GetBoolSetting", true);
        CheckMethod("Settings.SetStringSetting", "Eplan.EplApi.Base.Settings", "SetStringSetting", false);
        CheckMethod("Settings.GetNumericSetting", "Eplan.EplApi.Base.Settings", "GetNumericSetting", false);
        CheckType  ("ActionManager", "Eplan.EplApi.ApplicationFramework.ActionManager", false);

        // DataModel/HEServices members — INFO only (only meaningful if the asm is loaded).
        // These answer the docs' open questions about the object model surface.
        CheckMethod("DMObjectsFinder.GetFunctions", "Eplan.EplApi.DataModel.DMObjectsFinder", "GetFunctions", false);
        CheckMethod("DMObjectsFinder.GetPages", "Eplan.EplApi.DataModel.DMObjectsFinder", "GetPages", false);
        CheckMethod("DMObjectsFinder.GetConnections", "Eplan.EplApi.DataModel.DMObjectsFinder", "GetConnections", false);
        CheckProperty("Function.VisibleName", "Eplan.EplApi.DataModel.Function", "VisibleName", false);
        CheckProperty("Function.SymbolName", "Eplan.EplApi.DataModel.Function", "SymbolName", false);
        CheckProperty("Project.ProjectName", "Eplan.EplApi.DataModel.Project", "ProjectName", false);
        CheckProperty("Project.ProjectLinkFilePath", "Eplan.EplApi.DataModel.Project", "ProjectLinkFilePath", false);
        CheckType  ("Connection", "Eplan.EplApi.DataModel.Connection", false);
        CheckMethod("Connection.GetConnectedFunctions", "Eplan.EplApi.DataModel.Connection", "GetConnectedFunctions", false);
        CheckProperty("Page.Connections", "Eplan.EplApi.DataModel.Page", "Connections", false);
        CheckMethod("PropertyValue.To<T> (generic)", "Eplan.EplApi.DataModel.PropertyValue", "To", false);
    }

    // Get the project PATH via the 'selectionset' action — works without DataModel.
    private string SectionActionsAndProject()
    {
        Head("ACTIONS — project access without DataModel (read-only)");
        string projectPath = "";

        // selectionset TYPE=PROJECT -> PROJECT (path or name)
        try
        {
            CommandLineInterpreter cli = new CommandLineInterpreter();
            ActionCallingContext acc = new ActionCallingContext();
            acc.AddParameter("TYPE", "PROJECT");
            bool ok = cli.Execute("selectionset", acc);
            string proj = "";
            try { acc.GetParameter("PROJECT", ref proj); } catch { }
            projectPath = proj;
            if (ok && !string.IsNullOrEmpty(proj)) Pass("selectionset TYPE=PROJECT", "PROJECT='" + proj + "'");
            else Fail("selectionset TYPE=PROJECT", "ok=" + ok + " PROJECT='" + proj + "' (select the project node first)");
        }
        catch (System.Exception ex) { Fail("selectionset TYPE=PROJECT", Ex(ex)); }

        // selectionset TYPE=PAGES -> PAGES
        try
        {
            CommandLineInterpreter cli = new CommandLineInterpreter();
            ActionCallingContext acc = new ActionCallingContext();
            acc.AddParameter("TYPE", "PAGES");
            bool ok = cli.Execute("selectionset", acc);
            string pages = "";
            try { acc.GetParameter("PAGES", ref pages); } catch { }
            int n = string.IsNullOrEmpty(pages) ? 0 : pages.Split(';').Length;
            Info("selectionset TYPE=PAGES", "ok=" + ok + ", selected pages=" + n);
        }
        catch (System.Exception ex) { Info("selectionset TYPE=PAGES", Ex(ex)); }

        // GetProjectProperty action: id/index -> value (read project props without DataModel)
        string[] ids = new string[] { "10013", "10011", "10900", "10901", "10006" };
        foreach (string id in ids)
        {
            try
            {
                CommandLineInterpreter cli = new CommandLineInterpreter();
                ActionCallingContext acc = new ActionCallingContext();
                acc.AddParameter("id", id);
                acc.AddParameter("index", "1");
                bool ok = cli.Execute("GetProjectProperty", acc);
                string val = "";
                try { acc.GetParameter("value", ref val); } catch { }
                Info("GetProjectProperty id=" + id, "ok=" + ok + " value='" + Clip(val) + "'");
            }
            catch (System.Exception ex) { Info("GetProjectProperty id=" + id, Ex(ex)); }
        }
        return projectPath;
    }

    // THE KEY TEST: reach SelectionSet -> Project -> DMObjectsFinder -> functions, all by
    // reflection. If this passes, the DataModel is usable from a simple script via
    // reflection (the escape hatch for ExportEngravingData / FinalizeProject).
    private void SectionDataModelViaReflection()
    {
        Head("DATAMODEL VIA REFLECTION — READ path (GetCurrentProject(false) + correct filter)");

        object sel;
        try { sel = New("Eplan.EplApi.HEServices.SelectionSet"); }
        catch (System.Exception ex) { Fail("new SelectionSet", Ex(ex)); return; }

        // KEY FIX vs v.4: the LOCK is governed by the LockProjectByDefault PROPERTY
        // (default TRUE), not the bool arg — both GetCurrentProject(true)/(false) hit
        // NoLockingStepException S063110 in a script. Turn it off for a read-only handle.
        try { Call(sel, "set_LockProjectByDefault", new object[] { false }); Pass("set LockProjectByDefault=false", "ok"); }
        catch (System.Exception ex) { Info("set LockProjectByDefault=false", Ex(ex)); }

        object project = null;
        try { project = Call(sel, "GetCurrentProject", new object[] { false }); Pass("GetCurrentProject(false)", project == null ? "null" : project.GetType().FullName); }
        catch (System.Exception ex) { Fail("GetCurrentProject(false)", Ex(ex)); }
        if (project == null)
        {
            try { project = Call(sel, "GetCurrentProject", new object[] { true }); Pass("GetCurrentProject(true)", project == null ? "null" : project.GetType().FullName); }
            catch (System.Exception ex) { Fail("GetCurrentProject(true)", Ex(ex)); }
        }
        if (project == null) { Fail("read path", "no read-only project handle (locking still blocked)"); return; }

        try { Info("project path", "" + GetProp(project, "ProjectLinkFilePath")); } catch (System.Exception ex) { Info("project path", Ex(ex)); }

        object finder;
        try { finder = New("Eplan.EplApi.DataModel.DMObjectsFinder", project); }
        catch (System.Exception ex) { Fail("new DMObjectsFinder", Ex(ex)); return; }

        // CORRECT namespace (v.3 found it): Eplan.EplApi.DataModel.FunctionsFilter (NOT .Filters).
        object filter;
        try { filter = New("Eplan.EplApi.DataModel.FunctionsFilter"); }
        catch (System.Exception ex) { Fail("new FunctionsFilter", Ex(ex)); return; }

        System.Array fns;
        try { object o = Call(finder, "GetFunctions", new object[] { filter }); fns = o as System.Array;
              Pass("GetFunctions(filter)", (fns == null ? -1 : fns.Length) + " functions"); }
        catch (System.Exception ex) { Fail("GetFunctions(filter)", Ex(ex)); return; }
        if (fns == null) return;

        // Prove PROPERTY READS work — exactly what the engraving EXPORT needs.
        object gravId = PropId("Function", "FUNC_GRAVINGTEXT");
        Info("FUNC_GRAVINGTEXT id", gravId == null ? "NOT RESOLVED via Properties+Function" : gravId.ToString());

        // One detailed attempt to surface any property-read error / shape:
        if (fns.Length > 0 && gravId != null)
        {
            try
            {
                object f0 = fns.GetValue(0);
                object propList = GetProp(f0, "Properties");
                object pv = Call(propList, "get_Item", new object[] { gravId });
                Info("prop read detail", "PropertyValue=" + (pv == null ? "null" : pv.GetType().Name)
                    + " IsEmpty=" + GetProp(pv, "IsEmpty") + " val='" + Clip(pv == null ? "" : pv.ToString()) + "'");
            }
            catch (System.Exception ex) { Info("prop read detail", Ex(ex)); }
        }

        int withGrav = 0, shown = 0;
        foreach (object f in fns)
        {
            string grav = gravId == null ? "" : SafeReadProp(f, gravId);
            if (!string.IsNullOrEmpty(grav)) withGrav++;
            if (shown < 5 && !string.IsNullOrEmpty(grav))
            {
                string name = ""; try { name = "" + GetProp(f, "Name"); } catch { }
                Info("engraved fn", "[" + name + "] '" + Clip(grav) + "'"); shown++;
            }
        }
        Pass("reflection property reads", withGrav + " of " + fns.Length + " functions carry engraving text");
        _log.AppendLine(">>> RESULT: read-only DataModel (enumerate + property read) via reflection " +
                        (withGrav >= 0 ? "WORKS" : "unknown") + " — engraving EXPORT is feasible as a simple script.");
    }

    // Resolve a static property id like Properties.Function.FUNC_GRAVINGTEXT by reflection.
    private static object PropId(string nested, string member)
    {
        System.Type t = FindType("Eplan.EplApi.DataModel.Properties+" + nested);
        if (t == null) return null;
        try { PropertyInfo pi = t.GetProperty(member); if (pi != null) return pi.GetValue(null, null); } catch { }
        try { FieldInfo fi = t.GetField(member); if (fi != null) return fi.GetValue(null); } catch { }
        return null;
    }

    // Read f.Properties[id] -> string, via reflection. Returns "" on empty/any failure.
    private static string SafeReadProp(object function, object id)
    {
        try
        {
            object propList = GetProp(function, "Properties");
            object pv = Call(propList, "get_Item", new object[] { id });
            object empty = null; try { empty = GetProp(pv, "IsEmpty"); } catch { }
            if (empty is bool && (bool)empty) return "";
            return pv == null ? "" : pv.ToString();
        }
        catch { return ""; }
    }

    private void SectionPaths()
    {
        Head("PATHMAP — which variables resolve here");
        string[] vars = new string[] {
            "$(PROJECTNAME)", "$(PROJECTPATH)", "$(P)", "$(DOC)", "$(IMG)",
            "$(MD_SCRIPTS)", "$(MD_PARTS)", "$(MD_MACROS)", "$(BIN)", "$(EPLAN_VERSION)"
        };
        foreach (string v in vars)
        {
            try
            {
                string r = PathMap.SubstitutePath(v);
                if (string.IsNullOrEmpty(r) || r == v) Info("PathMap " + v, "UNRESOLVED");
                else Info("PathMap " + v, r);
            }
            catch (System.Exception ex) { Info("PathMap " + v, Ex(ex)); }
        }
    }

    private void SectionLanguages()
    {
        Head("LANGUAGES + MULTILANGSTRING (Base — available)");
        try
        {
            ISOCode.Language gui = new Languages().GuiLanguage.GetNumber();
            Pass("Languages().GuiLanguage.GetNumber()", "GUI language = " + gui.ToString());

            MultiLangString mls = new MultiLangString();
            mls.AddString(ISOCode.Language.L_en_US, "validate-en");
            mls.AddString(ISOCode.Language.L_de_DE, "validate-de");
            string shown = mls.GetStringToDisplay(gui);
            if (!string.IsNullOrEmpty(shown)) Pass("MultiLangString round-trip", "display = '" + shown + "'");
            else Info("MultiLangString GetStringToDisplay", "empty for " + gui.ToString());
        }
        catch (System.Exception ex) { Fail("Languages / MultiLangString", Ex(ex)); }
    }

    private void SectionSettings()
    {
        Head("SETTINGS — read + no-op write-back (non-destructive)");
        const string key = "USER.ModalDialogs.XSdCustomToolbarComponent.ExtendedActionList";
        try
        {
            // Fully qualified: ApplicationFramework also defines Settings -> bare name is ambiguous.
            Eplan.EplApi.Base.Settings s = new Eplan.EplApi.Base.Settings();
            bool before = s.GetBoolSetting(key, 0);
            Info("Settings.GetBoolSetting", key + " = " + before);
            s.SetBoolSetting(key, before, 0);
            bool after = s.GetBoolSetting(key, 0);
            if (after == before) Pass("Settings.SetBoolSetting (no-op write-back)", "preserved (" + after + ")");
            else { s.SetBoolSetting(key, before, 0); Fail("Settings.SetBoolSetting", "changed! restored to " + before); }
        }
        catch (System.Exception ex) { Fail("Settings read/write", Ex(ex)); }
    }

    private void SectionExports(string projectPath)
    {
        Head("EXPORTS (optional — RunExportProbes=" + RunExportProbes + ")");
        if (!RunExportProbes) { Info("export probes", "skipped (set RunExportProbes=true)"); return; }

        string temp = Path.Combine(Path.GetTempPath(), "EPLAN_ValidateApi");
        try { Directory.CreateDirectory(temp); } catch { }
        try
        {
            CommandLineInterpreter cli = new CommandLineInterpreter();
            ActionCallingContext acc = new ActionCallingContext();
            string pdf = Path.Combine(temp, "ValidateApi.pdf");
            acc.AddParameter("TYPE", "PDFPROJECTSCHEME");
            acc.AddParameter("EXPORTSCHEME", "EPLAN_default_value");
            acc.AddParameter("EXPORTFILE", pdf);
            if (!string.IsNullOrEmpty(projectPath)) acc.AddParameter("PROJECTNAME", projectPath);
            bool ok = cli.Execute("export", acc);
            if (ok && File.Exists(pdf)) Pass("export PDFPROJECTSCHEME", pdf);
            else Fail("export PDFPROJECTSCHEME", "ok=" + ok + " exists=" + File.Exists(pdf));
        }
        catch (System.Exception ex) { Fail("export PDFPROJECTSCHEME", Ex(ex)); }
    }

    // ===== reflection helpers (string-based: never break compilation) =====

    private static System.Type FindType(string fullName)
    {
        foreach (Assembly asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            try { System.Type t = asm.GetType(fullName); if (t != null) return t; }
            catch { }
        }
        return null;
    }

    private static object New(string typeName, params object[] args)
    {
        System.Type t = FindType(typeName);
        if (t == null) throw new System.Exception("type not loaded: " + typeName);
        return System.Activator.CreateInstance(t, args);
    }

    private static object Call(object target, string method, object[] args)
    {
        System.Type t = target.GetType();
        int want = args == null ? 0 : args.Length;
        foreach (MethodInfo m in t.GetMethods())
        {
            if (m.Name != method) continue;
            ParameterInfo[] ps = m.GetParameters();
            if (ps.Length != want) continue;
            bool ok = true;
            for (int i = 0; i < ps.Length; i++)
                if (args[i] != null && !ps[i].ParameterType.IsInstanceOfType(args[i])) { ok = false; break; }
            if (ok) return m.Invoke(target, args);
        }
        throw new System.Exception(method + "(" + want + " args) not matched on " + t.FullName);
    }

    private static object GetProp(object target, string prop)
    {
        PropertyInfo pi = target.GetType().GetProperty(prop);
        if (pi == null) throw new System.Exception("no property " + prop);
        return pi.GetValue(target, null);
    }

    private void DumpMethods(string label, string typeName, string nameContains)
    {
        System.Type t = FindType(typeName);
        if (t == null) { Info(label, "type not loaded: " + typeName); return; }
        foreach (MethodInfo m in t.GetMethods())
        {
            if (nameContains != null && m.Name.IndexOf(nameContains) < 0) continue;
            ParameterInfo[] ps = m.GetParameters();
            StringBuilder sig = new StringBuilder(m.Name + "(");
            for (int i = 0; i < ps.Length; i++) { if (i > 0) sig.Append(", "); sig.Append(ps[i].ParameterType.Name); }
            sig.Append(") : " + m.ReturnType.Name);
            Info(label, sig.ToString());
        }
    }

    private void DumpCtors(string label, string typeName)
    {
        System.Type t = FindType(typeName);
        if (t == null) { Info(label, "type not loaded: " + typeName); return; }
        foreach (ConstructorInfo c in t.GetConstructors())
        {
            ParameterInfo[] ps = c.GetParameters();
            StringBuilder sig = new StringBuilder("ctor(");
            for (int i = 0; i < ps.Length; i++) { if (i > 0) sig.Append(", "); sig.Append(ps[i].ParameterType.Name); }
            sig.Append(")");
            Info(label, sig.ToString());
        }
    }

    private void FindTypesNamed(string label, string simpleName)
    {
        int found = 0;
        foreach (Assembly asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
            catch { continue; }
            if (types == null) continue;
            foreach (System.Type t in types)
                if (t != null && t.Name == simpleName) { Info(label, t.FullName + "  @ " + asm.GetName().Name); found++; }
        }
        if (found == 0) Info(label, "none found in loaded assemblies");
    }

    private static System.Type FindAnyType(string simpleName)
    {
        foreach (Assembly asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
            catch { continue; }
            if (types == null) continue;
            foreach (System.Type t in types)
                if (t != null && t.Name == simpleName) return t;
        }
        return null;
    }

    private static int NameCount(MethodInfo[] ms, string name)
    {
        int c = 0;
        foreach (MethodInfo m in ms) if (m.Name == name) c++;
        return c;
    }

    private void CheckType(string label, string typeName, bool claimed)
    {
        bool has = FindType(typeName) != null;
        Record(label, has, claimed, typeName + (has ? " present" : " absent"));
    }

    private void CheckMethod(string label, string typeName, string method, bool claimed)
    {
        System.Type t = FindType(typeName);
        if (t == null) { Record(label, false, claimed, "type not loaded: " + typeName); return; }
        int c = NameCount(t.GetMethods(), method);
        Record(label, c > 0, claimed, typeName + "." + method + (c > 0 ? " present (" + c + ")" : " absent"));
    }

    private void CheckProperty(string label, string typeName, string prop, bool claimed)
    {
        System.Type t = FindType(typeName);
        if (t == null) { Record(label, false, claimed, "type not loaded: " + typeName); return; }
        bool has;
        try { has = t.GetProperty(prop) != null; } catch { has = true; }
        Record(label, has, claimed, typeName + "." + prop + (has ? " present" : " absent"));
    }

    private void Record(string label, bool has, bool claimed, string detail)
    {
        if (claimed) { if (has) Pass(label, detail); else Fail(label, detail); }
        else Info(label, detail);
    }

    // ===== logging / output =====

    private void Head(string title) { _log.AppendLine(""); _log.AppendLine("---- " + title + " ----"); }
    private void Pass(string n, string d) { _pass++; _log.AppendLine("[PASS] " + n + Tail(d)); }
    private void Fail(string n, string d) { _fail++; _log.AppendLine("[FAIL] " + n + Tail(d)); }
    private void Info(string n, string d) { _info++; _log.AppendLine("[INFO] " + n + Tail(d)); }
    private static string Tail(string d) { return string.IsNullOrEmpty(d) ? "" : " : " + d; }
    private static string Ex(System.Exception ex)
    {
        // Unwrap InnerException (TargetInvocationException hides the real error here).
        StringBuilder sb = new StringBuilder();
        System.Exception e = ex;
        int depth = 0;
        while (e != null && depth < 6)
        {
            if (depth > 0) sb.Append("  <-  ");
            sb.Append(e.GetType().Name + ": " + e.Message);
            e = e.InnerException; depth++;
        }
        return sb.ToString();
    }
    private static string Clip(string s) { if (s == null) return ""; s = s.Replace("\r", " ").Replace("\n", " "); return s.Length > 60 ? s.Substring(0, 60) + "..." : s; }

    private string WriteLog(string projectPath)
    {
        string dir = null;
        try
        {
            if (!string.IsNullOrEmpty(projectPath) && (projectPath.IndexOf(":\\") >= 0 || projectPath.IndexOf(".elk") >= 0))
                dir = Path.Combine(Path.ChangeExtension(projectPath, ".edb"), "DOC");
        }
        catch { dir = null; }
        if (dir == null) dir = Path.Combine(Path.GetTempPath(), "EPLAN_Scripts");
        try { Directory.CreateDirectory(dir); } catch { dir = Path.GetTempPath(); }

        string path = Path.Combine(dir, "ValidateApi.log");
        try { File.AppendAllText(path, _log.ToString() + System.Environment.NewLine, new UTF8Encoding(true)); }
        catch { }
        return path;
    }

    private static void Tell(string text)
    {
        new Decider().Decide(EnumDecisionType.eOkDecision, text, "ValidateApi",
            EnumDecisionReturn.eOK, EnumDecisionReturn.eOK);
    }
}
