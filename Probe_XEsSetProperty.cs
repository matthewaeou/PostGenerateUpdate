// Probe_XEsSetProperty.cs
//
// PROBE (v2, 2026-06-09): can a simple script WRITE a property of the selected object
// via the built-in action XEsSetPropertyAction — and what are the REAL parameter
// names of the related actions?
//
// v1 RESULTS (2026-06-09 18:15, build 25625 — see docs\EPLAN-Scripting-Reference.md §5a):
//   - XEsSetPropertyAction / XEsSetPagePropertyAction / XEsGetPropertyAction all EXIST
//   - the write Execute() returned True (selection survives the Scripts > Run dialog)
//   - read-back with GUESSED param names executed True but returned an empty value
// v2 ADDS:
//   1. Parameter-NAME dump for the key actions via
//      Action.ActionProperties.GetParameterProperties() -> ActionParameterProperties.Name
//      (member chain verified present on 2026.0.3 by reflection of AFu.dll).
//   2. A read-back that uses the DISCOVERED parameter names of XEsGetPropertyAction.
//   3. Self-verification of the write: runs our EngravingDataExport add-in action and
//      greps the CSV for the marker — closes the loop through the data model.
//
// HOW TO RUN (use a DEMO project — the write is intentional):
//   1. Open a schematic page in the graphical editor.
//   2. Select ONE function (a device with a DT — motor, terminal strip, ...).
//   3. Utilities > Scripts > Run... -> Probe_XEsSetProperty.cs
//   Undo the write with Ctrl+Z in the editor (or clear the property manually).
//
// Log: <project>.edb\DOC\Probe_XEsSetProperty.log  (fallback %TEMP%\EPLAN_Scripts)

using System;
using System.IO;
using System.Text;
using Eplan.EplApi.ApplicationFramework;
using Eplan.EplApi.Base;
using Eplan.EplApi.Scripting;

public class ProbeXEsSetProperty
{
    private const string ScriptVersion = "2026-06-09.2";

    private readonly StringBuilder _log = new StringBuilder();
    private CommandLineInterpreter _cli;

    [Start]
    public void Run()
    {
        _cli = new CommandLineInterpreter();
        _log.AppendLine("=== Probe_XEsSetProperty  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
        _log.AppendLine("Script version : " + ScriptVersion);
        _log.AppendLine("user           : " + Environment.UserName + " @ " + Environment.MachineName);

        // ---- 1. capability map (v1 results kept for regression) --------------------
        _log.AppendLine();
        _log.AppendLine("--- action existence (ActionManager.FindAction) ---");
        ProbeAction("selectionset");                       // control: known good
        ProbeAction("EngravingDataExport");                // our add-in — used in step 5
        bool setExists  = ProbeAction("XEsSetPropertyAction");
        bool setPgExists= ProbeAction("XEsSetPagePropertyAction");
        bool getExists  = ProbeAction("XEsGetPropertyAction");

        // ---- 2. REAL parameter names of the interesting actions --------------------
        _log.AppendLine();
        _log.AppendLine("--- declared action parameters (Action.ActionProperties) ---");
        DumpActionParams("XEsSetPropertyAction");
        DumpActionParams("XEsSetPagePropertyAction");
        string[] getParams = DumpActionParams("XEsGetPropertyAction");
        DumpActionParams("XMExportFunctionAction");
        DumpActionParams("selectionset");
        DumpActionParams("export");
        DumpActionParams("label");
        DumpActionParams("renumber");
        DumpActionParams("generate");
        DumpActionParams("partslist");
        DumpActionParams("XSettingsImport");
        DumpActionParams("EngravingDataExport");

        // ---- 3. project context -----------------------------------------------------
        _log.AppendLine();
        _log.AppendLine("--- context ---");
        string projectPath = "";
        try
        {
            ActionCallingContext ss = new ActionCallingContext();
            ss.AddParameter("TYPE", "PROJECT");
            _cli.Execute("selectionset", ss);
            ss.GetParameter("PROJECTNAME", ref projectPath);
            if (string.IsNullOrEmpty(projectPath)) ss.GetParameter("PROJECT", ref projectPath);
        }
        catch (Exception ex) { _log.AppendLine("selectionset EXCEPTION : " + ex.Message); }
        _log.AppendLine("project : " + (string.IsNullOrEmpty(projectPath) ? "(none resolved)" : projectPath));

        // ---- 4. THE WRITE: engraving text (20025) of the selected function(s) -------
        string marker = "XESPROBE " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string cmd = "XEsSetPropertyAction /PropertyId:20025 /PropertyIndex:0 /PropertyValue:\"" + marker + "\"";
        _log.AppendLine();
        _log.AppendLine("--- write probe (engraving text 20025) ---");
        _log.AppendLine("command  : " + cmd);
        bool okWrite = false;
        try
        {
            okWrite = _cli.Execute(cmd);
            _log.AppendLine("returned : " + okWrite);
        }
        catch (Exception ex) { _log.AppendLine("EXCEPTION: " + ex); }

        // ---- 5. self-verify through the data model: export engraving CSV, find marker
        _log.AppendLine();
        _log.AppendLine("--- write verification (EngravingDataExport -> CSV grep) ---");
        bool exportOk = false, markerFound = false;
        string csvPath = "";
        try
        {
            if (!string.IsNullOrEmpty(projectPath) && projectPath.EndsWith(".elk", StringComparison.OrdinalIgnoreCase))
            {
                csvPath = Path.Combine(Path.ChangeExtension(projectPath, ".edb"), "DOC", "EngravingData.csv");
                exportOk = _cli.Execute("EngravingDataExport");
                _log.AppendLine("EngravingDataExport returned : " + exportOk);
                if (File.Exists(csvPath))
                {
                    string csv = File.ReadAllText(csvPath);
                    markerFound = csv.Contains(marker);
                    _log.AppendLine("CSV exists  : True (" + csvPath + ")");
                    _log.AppendLine("marker found: " + markerFound + "  (searched for '" + marker + "')");
                    if (!markerFound)
                        _log.AppendLine("generic 'XESPROBE' found: " + csv.Contains("XESPROBE"));
                }
                else _log.AppendLine("CSV exists  : False (" + csvPath + ")");
            }
            else _log.AppendLine("skipped — no project path resolved");
        }
        catch (Exception ex) { _log.AppendLine("EXCEPTION: " + ex.Message); }

        // ---- 6. read-back using the DISCOVERED parameter names ----------------------
        if (getExists)
        {
            _log.AppendLine();
            _log.AppendLine("--- read-back probe (XEsGetPropertyAction, discovered params) ---");
            try
            {
                ActionCallingContext rctx = new ActionCallingContext();
                if (getParams != null && getParams.Length > 0)
                {
                    foreach (string p in getParams)
                    {
                        string lower = p.ToLowerInvariant();
                        if (lower.Contains("index"))      rctx.AddParameter(p, "0");
                        else if (lower.Contains("id"))    rctx.AddParameter(p, "20025");
                        // leave anything else unset; it may be the output slot
                    }
                }
                else
                {
                    // no declared params reported — fall back to the v1 guesses
                    rctx.AddParameter("PropertyId", "20025");
                    rctx.AddParameter("PropertyIndex", "0");
                }
                bool okRead = _cli.Execute("XEsGetPropertyAction", rctx);
                _log.AppendLine("returned : " + okRead);

                // read EVERY declared parameter back out and log its value
                if (getParams != null)
                {
                    foreach (string p in getParams)
                    {
                        string v = "";
                        try { rctx.GetParameter(p, ref v); } catch { v = "(GetParameter threw)"; }
                        _log.AppendLine("  out " + p.PadRight(20) + "= '" + v + "'");
                    }
                }
                string guess = "";
                try { rctx.GetParameter("PropertyValue", ref guess); } catch { }
                _log.AppendLine("  out PropertyValue (guess) = '" + guess + "'");
            }
            catch (Exception ex) { _log.AppendLine("EXCEPTION: " + ex.Message); }
        }

        // ---- 7. log + summary ---------------------------------------------------------
        string logPath = WriteLog(projectPath);
        Tell(
            "Probe_XEsSetProperty " + ScriptVersion + "\n\n" +
            "XEsSetPropertyAction : " + (setExists ? "EXISTS" : "MISSING") +
            "   XEsSetPagePropertyAction : " + (setPgExists ? "EXISTS" : "MISSING") + "\n" +
            "write returned       : " + okWrite + "\n" +
            "export returned      : " + exportOk + "\n" +
            "MARKER FOUND IN CSV  : " + markerFound + "   <- the verdict\n\n" +
            "Marker: " + marker + "\n" +
            "Undo the write with Ctrl+Z.\n\n" +
            "Param-name dumps + read-back detail: " + logPath);
    }

    // =====================================================================

    private bool ProbeAction(string name)
    {
        try
        {
            Eplan.EplApi.ApplicationFramework.Action act = new ActionManager().FindAction(name);
            bool exists = (act != null);
            _log.AppendLine(name.PadRight(30) + ": " + (exists ? "EXISTS" : "MISSING"));
            return exists;
        }
        catch (Exception ex)
        {
            _log.AppendLine(name.PadRight(30) + ": EXCEPTION " + ex.Message);
            return false;
        }
    }

    // Logs and returns the declared parameter names of an action (null if unavailable).
    private string[] DumpActionParams(string actionName)
    {
        try
        {
            Eplan.EplApi.ApplicationFramework.Action act = new ActionManager().FindAction(actionName);
            if (act == null) { _log.AppendLine(actionName.PadRight(28) + ": MISSING"); return null; }

            ActionProperties ap = act.ActionProperties;
            System.Collections.ArrayList list = ap.GetParameterProperties();
            if (list == null || list.Count == 0)
            {
                _log.AppendLine(actionName.PadRight(28) + ": (no declared parameters)");
                return new string[0];
            }
            string[] names = new string[list.Count];
            StringBuilder line = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                ActionParameterProperties p = list[i] as ActionParameterProperties;
                names[i] = (p != null) ? p.Name : "(?)";
                line.Append(names[i]);
                if (i < list.Count - 1) line.Append(", ");
            }
            _log.AppendLine(actionName.PadRight(28) + ": " + line);
            return names;
        }
        catch (Exception ex)
        {
            _log.AppendLine(actionName.PadRight(28) + ": EXCEPTION " + ex.Message);
            return null;
        }
    }

    private string WriteLog(string projectPath)
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
            File.AppendAllText(p, _log.ToString() + Environment.NewLine, new UTF8Encoding(true));
            return p;
        }
        catch { return "(log write failed)"; }
    }

    private void Tell(string message)
    {
        try
        {
            new Decider().Decide(EnumDecisionType.eOkDecision, message, "Probe_XEsSetProperty",
                EnumDecisionReturn.eOK, EnumDecisionReturn.eOK);
        }
        catch { }
    }
}
