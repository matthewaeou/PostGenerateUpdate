// Probe_XEsSetProperty_Menu.cs
//
// PROBE v4 (2026-06-10): write CONFIRMED from a GED context menu (v3) — now CHASE THE
// READ-BACK. Can a context-menu action READ a selected object's property via the read
// twin XEsGetPropertyAction, and what is its OUTPUT parameter name?
//
// v3 RESULT (2026-06-10, build 25625): from a GED context-menu [DeclareAction],
// XEsSetPropertyAction WROTE function text (20011) + engraving text (20025) of the
// right-clicked function — CSV-verified. (The same call from Scripts>Run returns True
// but writes nothing — selection isn't visible there.) "Editor"/"Ged" menu IDs work.
//
// v4 METHOD — no more guessing the output slot name. Eplan.EplApi.Base.Context (the base
// of ActionCallingContext) exposes GetParameters() -> string[] of ALL parameter names
// in the context. So: run XEsGetPropertyAction, then ENUMERATE every parameter the
// action left behind and log name='value'. Whichever name holds our just-written marker
// IS the output slot. Belt-and-braces diagnostics also dump GetParameterCount(),
// GetStrings(), and GetContextParameter().GetList(). Read-back inputs mirror the setter
// (PropertyId/PropertyIndex); if those input names are wrong the action returns ok but
// no value matches — logged as such.
//
// This still does the v3 write first (so there's a known value to read back), keeps the
// CSV verification, then adds the read-back enumeration for 20025 and 20011.
//
// REGISTERED MENU ENTRIES:
//   1. GED context menu (DialogName "Editor" / ContextMenuName "Ged"), and
//   2. control entry in the page-navigator menu (PmPageObjectTreeDialog/1007).
//
// HOW TO RUN (demo project!):
//   1. LOAD this script (File > Extras > Interfaces > Scripts > Load) — [DeclareMenu]
//      scripts must be LOADED, not run. Restart EPLAN once if the entry doesn't appear.
//   2. Open a schematic page, RIGHT-CLICK directly on a device/function.
//   3. Click "XESPROBE: write+read marker".
//   4. Read the verdict dialog; full param enumeration is in the log. Undo with Ctrl+Z,
//      then Scripts > Unload.
//
// Log: <project>.edb\DOC\Probe_XEsSetProperty.log (appends to the v1/v2/v3 narrative)

using System;
using System.IO;
using System.Text;
using Eplan.EplApi.ApplicationFramework;
using Eplan.EplApi.Base;
using Eplan.EplApi.Gui;
using Eplan.EplApi.Scripting;

public class ProbeXEsSetPropertyMenu
{
    private const string ScriptVersion = "2026-06-10.4-menu";
    private const string SEP = " || ";   // separator for concatenated read-back values

    [DeclareMenu()]
    public void CreateMenu()
    {
        // 1. GED context menu — the Macro Utility's own location IDs.
        ContextMenuLocation locGed = new ContextMenuLocation();
        locGed.DialogName      = "Editor";
        locGed.ContextMenuName = "Ged";

        // 2. Control entry in the page navigator (known-good IDs from PageNavi script).
        ContextMenuLocation locNavi = new ContextMenuLocation();
        locNavi.DialogName      = "PmPageObjectTreeDialog";
        locNavi.ContextMenuName = "1007";

        Eplan.EplApi.Gui.ContextMenu menu = new Eplan.EplApi.Gui.ContextMenu();
        menu.AddMenuItem(locGed,  "XESPROBE: write+read marker",              "XEsProbeWrite", true,  false);
        menu.AddMenuItem(locNavi, "XESPROBE: write+read (navigator control)", "XEsProbeWrite", true,  false);
    }

    [DeclareAction("XEsProbeWrite")]
    public void XEsProbeWrite()
    {
        CommandLineInterpreter cli = new CommandLineInterpreter();
        StringBuilder log = new StringBuilder();
        log.AppendLine("=== Probe_XEsSetProperty (MENU)  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
        log.AppendLine("Script version : " + ScriptVersion);
        log.AppendLine("user           : " + Environment.UserName + " @ " + Environment.MachineName);

        // project context
        string projectPath = "";
        try
        {
            ActionCallingContext ss = new ActionCallingContext();
            ss.AddParameter("TYPE", "PROJECT");
            cli.Execute("selectionset", ss);
            ss.GetParameter("PROJECTNAME", ref projectPath);
            if (string.IsNullOrEmpty(projectPath)) ss.GetParameter("PROJECT", ref projectPath);
        }
        catch (Exception ex) { log.AppendLine("selectionset EXCEPTION : " + ex.Message); }
        log.AppendLine("project : " + (string.IsNullOrEmpty(projectPath) ? "(none resolved)" : projectPath));

        string stamp = DateTime.Now.ToString("HH:mm:ss");
        string markerFT  = "XESPROBE-FT "  + stamp;   // function text  20011 — visible on page
        string markerENG = "XESPROBE-ENG " + stamp;   // engraving text 20025 — CSV-verifiable

        // write 1: function text (20011) — gives instant visual feedback on the page
        bool okFT = false;
        string cmdFT = "XEsSetPropertyAction /PropertyId:20011 /PropertyIndex:0 /PropertyValue:\"" + markerFT + "\"";
        log.AppendLine();
        log.AppendLine("--- write 1: function text 20011 ---");
        log.AppendLine("command  : " + cmdFT);
        try { okFT = cli.Execute(cmdFT); log.AppendLine("returned : " + okFT); }
        catch (Exception ex) { log.AppendLine("EXCEPTION: " + ex.Message); }

        // write 2: engraving text (20025) — verified through the CSV below
        bool okENG = false;
        string cmdENG = "XEsSetPropertyAction /PropertyId:20025 /PropertyIndex:0 /PropertyValue:\"" + markerENG + "\"";
        log.AppendLine();
        log.AppendLine("--- write 2: engraving text 20025 ---");
        log.AppendLine("command  : " + cmdENG);
        try { okENG = cli.Execute(cmdENG); log.AppendLine("returned : " + okENG); }
        catch (Exception ex) { log.AppendLine("EXCEPTION: " + ex.Message); }

        // verification: export engraving data, grep for the marker
        bool exportOk = false, markerFound = false;
        log.AppendLine();
        log.AppendLine("--- verification (EngravingDataExport -> CSV grep) ---");
        try
        {
            if (!string.IsNullOrEmpty(projectPath) && projectPath.EndsWith(".elk", StringComparison.OrdinalIgnoreCase))
            {
                string csvPath = Path.Combine(Path.ChangeExtension(projectPath, ".edb"), "DOC", "EngravingData.csv");
                exportOk = cli.Execute("EngravingDataExport");
                log.AppendLine("EngravingDataExport returned : " + exportOk);
                if (File.Exists(csvPath))
                {
                    string csv = File.ReadAllText(csvPath);
                    markerFound = csv.Contains(markerENG);
                    log.AppendLine("marker found in CSV : " + markerFound + "  (searched '" + markerENG + "')");
                }
                else log.AppendLine("CSV not found: " + csvPath);
            }
            else log.AppendLine("skipped — no project path resolved");
        }
        catch (Exception ex) { log.AppendLine("EXCEPTION: " + ex.Message); }

        // read-back: run XEsGetPropertyAction, then ENUMERATE every parameter it leaves
        // in the context — whichever holds our marker is the output slot name.
        string engReadValue = ReadBackProbe(cli, log, 20025, 0, markerENG);
        string ftReadValue  = ReadBackProbe(cli, log, 20011, 0, markerFT);
        bool readWorked = (engReadValue != null && engReadValue.Contains(markerENG))
                       || (ftReadValue  != null && ftReadValue.Contains(markerFT));

        // log + verdict
        string logPath = WriteLog(log, projectPath);
        Tell(
            "XESPROBE via CONTEXT MENU  (" + ScriptVersion + ")\n\n" +
            "WRITE\n" +
            "  function text (20011) returned : " + okFT + "\n" +
            "  engraving (20025) returned     : " + okENG + "\n" +
            "  marker found in CSV            : " + markerFound + "\n\n" +
            "READ-BACK (XEsGetPropertyAction)\n" +
            "  read worked (marker echoed)    : " + readWorked + "   <- the new verdict\n" +
            "  20025 read = '" + Trunc(engReadValue, 50) + "'\n" +
            "  20011 read = '" + Trunc(ftReadValue, 50) + "'\n\n" +
            "Full parameter enumeration is in the log (reveals the output slot name).\n" +
            "Undo with Ctrl+Z.\n\nLog: " + logPath);
    }

    // =====================================================================

    // Runs XEsGetPropertyAction for (propId, propIdx) and dumps EVERY parameter the
    // action leaves in the context. Returns all out-values concatenated (SEP-joined) so
    // the caller can test whether the just-written marker was echoed back.
    private string ReadBackProbe(CommandLineInterpreter cli, StringBuilder log, int propId, int propIdx, string expected)
    {
        log.AppendLine();
        log.AppendLine("--- read-back: XEsGetPropertyAction PropertyId=" + propId + " PropertyIndex=" + propIdx + " ---");
        StringBuilder allValues = new StringBuilder();
        try
        {
            ActionCallingContext acc = new ActionCallingContext();
            acc.AddParameter("PropertyId", propId.ToString());
            acc.AddParameter("PropertyIndex", propIdx.ToString());
            bool ok = cli.Execute("XEsGetPropertyAction", acc);
            log.AppendLine("returned : " + ok);

            // 1) primary channel — enumerate ALL named parameters now in the context
            try { log.AppendLine("GetParameterCount : " + acc.GetParameterCount()); }
            catch (Exception ex) { log.AppendLine("GetParameterCount EXCEPTION : " + ex.Message); }

            try
            {
                string[] names = acc.GetParameters();
                log.AppendLine("GetParameters     : " + (names == null ? "(null)" : names.Length + " name(s)"));
                if (names != null)
                {
                    foreach (string n in names)
                    {
                        string v = "";
                        try { acc.GetParameter(n, ref v); } catch { v = "(GetParameter threw)"; }
                        log.AppendLine("    " + n.PadRight(22) + "= '" + v + "'");
                        if (v != null) allValues.Append(v).Append(SEP);
                    }
                }
            }
            catch (Exception ex) { log.AppendLine("GetParameters EXCEPTION : " + ex.Message); }

            // 2) secondary channel — GetStrings()
            try
            {
                string[] strs = acc.GetStrings();
                if (strs != null && strs.Length > 0)
                {
                    log.AppendLine("GetStrings        : " + strs.Length + " item(s)");
                    for (int i = 0; i < strs.Length; i++)
                    {
                        log.AppendLine("    [" + i + "] = '" + strs[i] + "'");
                        if (strs[i] != null) allValues.Append(strs[i]).Append(SEP);
                    }
                }
                else log.AppendLine("GetStrings        : (empty)");
            }
            catch (Exception ex) { log.AppendLine("GetStrings EXCEPTION : " + ex.Message); }

            // 3) tertiary channel — ContextParameterBlock value list
            try
            {
                ContextParameterBlock cpb = acc.GetContextParameter();
                if (cpb != null)
                {
                    System.Collections.Generic.List<object> list = cpb.GetList();
                    if (list != null && list.Count > 0)
                    {
                        log.AppendLine("ContextParameter  : " + list.Count + " value(s)");
                        foreach (object o in list)
                        {
                            string v = (o == null) ? "(null)" : o.ToString();
                            log.AppendLine("    = '" + v + "'");
                            if (o != null) allValues.Append(v).Append(SEP);
                        }
                    }
                    else log.AppendLine("ContextParameter  : (empty)");
                }
            }
            catch (Exception ex) { log.AppendLine("ContextParameter EXCEPTION : " + ex.Message); }

            log.AppendLine("expected to find  : '" + expected + "'");
        }
        catch (Exception ex) { log.AppendLine("EXCEPTION: " + ex.Message); }
        return allValues.ToString();
    }

    private static string Trunc(string s, int n)
    {
        if (string.IsNullOrEmpty(s)) return "(empty)";
        return s.Length <= n ? s : s.Substring(0, n) + "...";
    }

    private string WriteLog(StringBuilder log, string projectPath)
    {
        try
        {
            string dir;
            if (!string.IsNullOrEmpty(projectPath) && projectPath.EndsWith(".elk", StringComparison.OrdinalIgnoreCase))
                dir = Path.Combine(Path.ChangeExtension(projectPath, ".edb"), "DOC");
            else
                dir = Path.Combine(Path.GetTempPath(), "EPLAN_Scripts");
            Directory.CreateDirectory(dir);
            string p = Path.Combine(dir, "Probe_XEsSetProperty.log");
            File.AppendAllText(p, log.ToString() + Environment.NewLine, new UTF8Encoding(true));
            return p;
        }
        catch { return "(log write failed)"; }
    }

    private void Tell(string message)
    {
        try
        {
            new Decider().Decide(EnumDecisionType.eOkDecision, message, "Probe_XEsSetProperty_Menu",
                EnumDecisionReturn.eOK, EnumDecisionReturn.eOK);
        }
        catch { }
    }
}
