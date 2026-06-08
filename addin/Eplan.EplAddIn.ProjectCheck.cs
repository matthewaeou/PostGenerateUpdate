// Eplan.EplAddIn.ProjectCheck.cs
//
// Project validation add-in for EPLAN Electric P8.
// Provides the 'ProjectCheck' action — run via RunProjectCheck.cs or
// Utilities > API > Execute action.
//
// Checks performed:
//   1. UNNUMBERED    device tags containing '?' (not yet numbered by renumber)
//   2. NO_PART_NR    numbered devices with no part number on any placement
//   3. NO_FUNC_TEXT  numbered devices with no function text on any placement
//   4. PANEL_ONLY    devices placed in panel layout but absent from all schematic pages
//   5. NO_LOCATION   panel-placed devices with no mounting location on the panel placement
//
// Output: <project>.edb\DOC\ProjectCheck.log  (appended each run) + summary dialog.
//
// ⚠️  PAGE TYPE CALIBRATION
//     Checks 4 and 5 use EPLAN page-type integers. Expected values (EPLAN documentation):
//       Schematic types  : 0 (SINGLELINE), 1 (MULTILINER), 4 (INTERCONNECT),
//                          5 (HYDRAULICS), 6 (PNEUMATICS)
//       Panel type       : 3 (PANELLAYOUT)
//     Every run logs a "PAGE TYPE DISCOVERY" section — verify the values match on first
//     run. If wrong, adjust SchematicTypes / PanelType at the top of ProjectCheckAction.
//     Page type is read via Properties.Page.PAGE_TYPE_NUMERIC (confirmed in DataModel DLL).
//
// Build:  addin\build.ps1  ->  addin\bin\Eplan.EplAddIn.ProjectCheck.dll
// Deploy: EPLAN > Utilities > API > Add-Ins... > Add...  (load on start)

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Eplan.EplApi.ApplicationFramework;
using Eplan.EplApi.Base;
using Eplan.EplApi.DataModel;
using Eplan.EplApi.HEServices;

namespace Eplan.EplAddIn.ProjectCheck
{
    // ------------------------------------------------------------------ add-in lifecycle
    public class ProjectCheckAddIn : IEplAddIn
    {
        public bool OnRegister(ref bool bLoadOnStart) { bLoadOnStart = true; return true; }
        public bool OnUnregister() { return true; }
        public bool OnInit()      { return true; }
        public bool OnInitGui()   { return true; }
        public bool OnExit()      { return true; }
    }

    // ------------------------------------------------------------------ check action
    public class ProjectCheckAction : IEplAction
    {
        public const string ActionName = "ProjectCheck";

        // ---- Page type constants (verify from PAGE TYPE DISCOVERY on first run) --------
        static readonly HashSet<int> SchematicTypes = new HashSet<int> { 0, 1, 4, 5, 6 };
        const int PanelType = 3;

        public bool OnRegister(ref string Name, ref int Ordinal)
        { Name = ActionName; Ordinal = 20; return true; }
        public void GetActionProperties(ref ActionProperties ap) { }

        public bool Execute(ActionCallingContext ctx)
        {
            var log = new CheckLog("ProjectCheck");
            try
            {
                Project project = new SelectionSet().GetCurrentProject(true);
                string elk = project.ProjectLinkFilePath;
                log.SetProject(elk);

                Function[] fns = new DMObjectsFinder(project).GetFunctions(new FunctionsFilter());
                log.Line("Functions scanned : " + fns.Length);
                log.Line("");

                // ----------------------------------------------------------
                // Single pass: collect per-DT aggregate data.
                //
                // A device tag (DT) can have multiple Function objects across pages
                // (e.g. -K1 coil on page 1, -K1 contacts on page 2, -K1 panel box on
                // page 3).  Checks must look at ALL placements before deciding — e.g.
                // "has any placement a part number?" rather than just the first one found.
                // ----------------------------------------------------------
                var dtMap = new Dictionary<string, DTInfo>();
                var pgTypeCount = new Dictionary<int, int>();   // for discovery report

                foreach (Function f in fns)
                {
                    string name = SafeName(f);
                    if (string.IsNullOrEmpty(name)) continue;

                    DTInfo info;
                    if (!dtMap.TryGetValue(name, out info))
                    {
                        bool unnumbered = (f.VisibleName ?? "").Contains("?");
                        info = new DTInfo(name, unnumbered, SafePageName(f));
                        dtMap[name] = info;
                    }

                    string pgName = SafePageName(f);
                    if (!info.Pages.Contains(pgName)) info.Pages.Add(pgName);

                    // Page type
                    int pgType = PageType(f);
                    if (pgType >= 0)
                    {
                        int c; pgTypeCount.TryGetValue(pgType, out c);
                        pgTypeCount[pgType] = c + 1;
                        info.PageTypes.Add(pgType);
                    }

                    // Part number — flag if ANY placement carries it
                    if (!info.HasPartNr)
                    {
                        string part = ReadIdx(f, Properties.Function.FUNC_ARTICLE_PARTNR, 1);
                        if (!string.IsNullOrEmpty(part)) info.HasPartNr = true;
                    }

                    // Function text — flag if ANY placement carries it
                    if (!info.HasFuncText)
                    {
                        string txt = ReadDisp(f, Properties.Function.FUNC_TEXT);
                        if (!string.IsNullOrEmpty(txt)) info.HasFuncText = true;
                    }

                    // Panel-specific properties
                    if (pgType == PanelType)
                    {
                        info.HasPanelPlacement = true;
                        if (!info.HasMountingLoc)
                        {
                            string loc = ReadDisp(f, Properties.Function.FUNC_MOUNTINGLOCATION);
                            if (!string.IsNullOrEmpty(loc)) info.HasMountingLoc = true;
                        }
                        // Remember one panel page for reporting
                        if (string.IsNullOrEmpty(info.PanelPage)) info.PanelPage = pgName;
                    }
                }

                // ----------------------------------------------------------
                // Issue generation
                // ----------------------------------------------------------
                var issues = new List<Issue>();

                foreach (DTInfo info in dtMap.Values)
                {
                    // Check 1 — unnumbered
                    if (info.IsUnnumbered)
                    {
                        issues.Add(new Issue("UNNUMBERED", info.Name, info.FirstPage,
                            "Device tag contains '?' — not yet numbered"));
                        continue;   // rest of checks not meaningful until numbered
                    }

                    // Check 2 — missing part number
                    if (!info.HasPartNr)
                        issues.Add(new Issue("NO_PART_NR", info.Name, info.FirstPage,
                            "No part number (FUNC_ARTICLE_PARTNR[1]) on any placement"));

                    // Check 3 — missing function text
                    if (!info.HasFuncText)
                        issues.Add(new Issue("NO_FUNC_TEXT", info.Name, info.FirstPage,
                            "No function text on any placement"));

                    // Check 4 — panel-only (no schematic)
                    // Only if we have page type data (pgTypeCount.Count > 0)
                    if (pgTypeCount.Count > 0 && info.HasPanelPlacement)
                    {
                        bool hasSchematic = false;
                        foreach (int t in info.PageTypes)
                            if (SchematicTypes.Contains(t)) { hasSchematic = true; break; }
                        if (!hasSchematic)
                            issues.Add(new Issue("PANEL_ONLY", info.Name,
                                string.Join(", ", info.Pages.ToArray()),
                                "Placed in panel layout but has no schematic representation"));
                    }

                    // Check 5 — panel device missing mounting location
                    if (pgTypeCount.Count > 0 && info.HasPanelPlacement && !info.HasMountingLoc)
                        issues.Add(new Issue("NO_LOCATION", info.Name,
                            info.PanelPage ?? info.FirstPage,
                            "Panel placement has no mounting location assigned"));
                }

                // ----------------------------------------------------------
                // Write report
                // ----------------------------------------------------------
                log.Line("=== ISSUES ===");
                log.Line("");
                WriteGroup(log, issues, "UNNUMBERED",   "1. Unnumbered devices");
                WriteGroup(log, issues, "NO_PART_NR",   "2. Missing part number");
                WriteGroup(log, issues, "NO_FUNC_TEXT", "3. Missing function text");
                WriteGroup(log, issues, "PANEL_ONLY",   "4. Panel-only placements");
                WriteGroup(log, issues, "NO_LOCATION",  "5. Panel device missing mounting location");

                log.Line("=== PAGE TYPE DISCOVERY ===");
                log.Line("Known: 0=SINGLELINE  1=MULTILINER  3=PANELLAYOUT  " +
                         "4=INTERCONNECT  5=HYDRAULICS  6=PNEUMATICS");
                if (pgTypeCount.Count == 0)
                    log.Line("  (no page type data — Properties.Page.PAGETYPE may need calibration;" +
                             " checks 4+5 skipped)");
                else
                    foreach (var kvp in pgTypeCount)
                        log.Line("  type " + kvp.Key + " : " + kvp.Value + " function(s)");

                log.Line("");
                log.Line("Total issues : " + issues.Count);
                log.Save();

                // Summary dialog
                var sb = new StringBuilder();
                sb.AppendLine("Project Check — " + issues.Count + " issue(s) found.");
                sb.AppendLine("");
                SummLine(sb, issues, "UNNUMBERED",   "Unnumbered devices");
                SummLine(sb, issues, "NO_PART_NR",   "Missing part number");
                SummLine(sb, issues, "NO_FUNC_TEXT", "Missing function text");
                SummLine(sb, issues, "PANEL_ONLY",   "Panel-only placements");
                SummLine(sb, issues, "NO_LOCATION",  "Panel device missing location");
                sb.AppendLine("");
                sb.AppendLine("Full report: " + log.LogPath);
                Msg(sb.ToString());
                return true;
            }
            catch (Exception ex)
            {
                log.Line("EXCEPTION: " + ex); log.Save();
                Msg("ProjectCheck FAILED:\n\n" + ex.Message);
                return false;
            }
        }

        static void WriteGroup(CheckLog log, List<Issue> issues, string code, string title)
        {
            var g = issues.FindAll(i => i.Code == code);
            log.Line(title + " (" + g.Count + "):");
            if (g.Count == 0)
                log.Line("  (none)");
            else
                foreach (var i in g)
                    log.Line("  " + i.DT.PadRight(32) + "  " + i.Location.PadRight(18) + "  " + i.Message);
            log.Line("");
        }

        static void SummLine(StringBuilder sb, List<Issue> issues, string code, string label)
        {
            sb.AppendLine("  " + label.PadRight(32) + ": " + issues.FindAll(i => i.Code == code).Count);
        }

        // ---- property helpers -----------------------------------------------

        // Returns the page-type integer for f's page, or -1 on any failure.
        static int PageType(Function f)
        {
            try
            {
                PropertyValue pv = f.Page.Properties[Properties.Page.PAGE_TYPE_NUMERIC];
                if (pv.IsEmpty) return -1;
                return (int)pv;
            }
            catch { return -1; }
        }

        static string SafeName(Function f)     { try { return f.Name ?? ""; }       catch { return ""; } }
        static string SafePageName(Function f) { try { return f.Page.Name ?? ""; }  catch { return ""; } }

        static string ReadIdx(Function f, AnyPropertyId id, int idx)
        {
            try { PropertyValue pv = f.Properties[id, idx]; return pv.IsEmpty ? "" : pv.ToString(); }
            catch { return ""; }
        }

        static string ReadDisp(Function f, AnyPropertyId id)
        {
            try
            {
                PropertyValue pv = f.Properties[id];
                if (pv.IsEmpty) return "";
                MultiLangString mls = pv;
                return mls.GetStringToDisplay(GuiLang()) ?? "";
            }
            catch
            {
                try { PropertyValue pv = f.Properties[id]; return pv.IsEmpty ? "" : pv.ToString(); }
                catch { return ""; }
            }
        }

        static ISOCode.Language GuiLang()
        {
            try { return new Languages().GuiLanguage.GetNumber(); }
            catch { return ISOCode.Language.L_en_US; }
        }

        static void Msg(string t)
        {
            new Decider().Decide(EnumDecisionType.eOkDecision, t, "ProjectCheck",
                EnumDecisionReturn.eOK, EnumDecisionReturn.eOK);
        }
    }

    // ------------------------------------------------------------------ per-DT data
    internal class DTInfo
    {
        internal readonly string Name;
        internal readonly bool   IsUnnumbered;
        internal readonly string FirstPage;      // page of first function seen (for reporting)
        internal string          PanelPage;      // page of first panel placement
        internal readonly List<string>   Pages     = new List<string>();
        internal readonly HashSet<int>   PageTypes = new HashSet<int>();
        internal bool HasPartNr;
        internal bool HasFuncText;
        internal bool HasPanelPlacement;
        internal bool HasMountingLoc;

        internal DTInfo(string name, bool unnumbered, string firstPage)
        { Name = name; IsUnnumbered = unnumbered; FirstPage = firstPage; }
    }

    // ------------------------------------------------------------------ issue record
    internal class Issue
    {
        internal readonly string Code, DT, Location, Message;
        internal Issue(string code, string dt, string loc, string msg)
        { Code = code; DT = dt; Location = loc; Message = msg; }
    }

    // ------------------------------------------------------------------ file logging
    internal sealed class CheckLog
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private readonly string        _name;
        private string                 _elk;
        internal string LogPath { get; private set; }

        internal CheckLog(string name)
        {
            _name = name;
            _sb.AppendLine("=== " + name + "  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
            _sb.AppendLine("user : " + Environment.UserName + " @ " + Environment.MachineName);
        }

        internal void SetProject(string elk) { _elk = elk; _sb.AppendLine("project : " + elk); }
        internal void Line(string s)         { _sb.AppendLine(s); }

        internal void Save()
        {
            string dir;
            try
            {
                dir = !string.IsNullOrEmpty(_elk)
                    ? Path.Combine(Path.ChangeExtension(_elk, ".edb"), "DOC")
                    : Path.Combine(Path.GetTempPath(), "EPLAN_Scripts");
                Directory.CreateDirectory(dir);
            }
            catch { dir = Path.GetTempPath(); }
            LogPath = Path.Combine(dir, _name + ".log");
            try { File.AppendAllText(LogPath, _sb.ToString() + Environment.NewLine, new UTF8Encoding(true)); }
            catch { }
        }
    }
}
