// Probe_MasterData.cs
//
// PROBE 2 of 2 (2026-06-09): may a simple script reference Eplan.EplApi.MasterData?
//
// >>> THE TEST IS WHETHER THIS FILE COMPILES WHEN LOADED VIA Scripts > Run... <<<
//
// If EPLAN reports CS0234 ("The type or namespace name 'MasterData' does not exist in
// the namespace 'Eplan.EplApi'") then the simple-script allowlist excludes MasterData
// on this build — record that in docs\EPLAN-Scripting-Reference.md §0/§5a and stop.
// If it loads and runs, simple scripts have the PARTS DATABASE object model.
//
// WHY WE EXPECT IT TO COMPILE: the shipped EPLAN Consulting Macro Utility V2.0
// (production script add-on) compiles `using Eplan.EplApi.MasterData;` as a script on
// platform 2022/2023. Every member used below is verbatim from that script AND was
// verified present with identical signatures on 2026.0.3 by reflection
// (MDPartsManagement.OpenDatabase() -> MDPartsDatabase,
//  MDPartsDatabase.GetParts(MDObjectFilter) -> MDPart[],
//  MDObjectFilter.AddPropertyCondition(MDAnyPropertyId, CompareOperator, string),
//  MDAnyPropertyId.Id/.Index, MDPart.PartNr).
// So a compile failure can ONLY mean the allowlist — not API drift.
//
// WHAT IT DOES (read-only): opens the configured parts database, counts parts whose
// part number (22001) is non-empty, logs the first part number, and dumps MDPart's
// real public property surface (reflection) for the reference docs. No writes.
//
// HOW TO RUN: no project or selection needed.
//   Utilities > Scripts > Run... -> Probe_MasterData.cs
// Log: %TEMP%\EPLAN_Scripts\Probe_MasterData.log

using System;
using System.IO;
using System.Text;
using System.Reflection;
using Eplan.EplApi.ApplicationFramework;
using Eplan.EplApi.Base;
using Eplan.EplApi.Scripting;
using Eplan.EplApi.MasterData;          // <<< THE TEST — CS0234 here means "not allowed"

public class ProbeMasterData
{
    private const string ScriptVersion = "2026-06-09.1";

    private readonly StringBuilder _log = new StringBuilder();

    [Start]
    public void Run()
    {
        _log.AppendLine("=== Probe_MasterData  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
        _log.AppendLine("Script version : " + ScriptVersion);
        _log.AppendLine("user           : " + Environment.UserName + " @ " + Environment.MachineName);
        _log.AppendLine();
        _log.AppendLine("COMPILED: using Eplan.EplApi.MasterData; was accepted by the script host.");
        _log.AppendLine();

        int count = -1;
        string firstPart = "";
        bool ok = false;

        try
        {
            using (MDPartsDatabase db = new MDPartsManagement().OpenDatabase())
            {
                if (db == null)
                {
                    _log.AppendLine("OpenDatabase() returned null — no parts database configured?");
                }
                else
                {
                    _log.AppendLine("OpenDatabase() OK : " + db.GetType().FullName);

                    // All parts with a non-empty part number (22001).
                    MDPart[] parts = null;
                    try
                    {
                        MDObjectFilter filter = new MDObjectFilter();
                        MDAnyPropertyId pid = new MDAnyPropertyId();
                        pid.Id = 22001;
                        filter.AddPropertyCondition(pid, MDObjectFilter.CompareOperator.OperatorNotEqual, "");
                        parts = db.GetParts(filter);
                    }
                    catch (Exception exFilter)
                    {
                        _log.AppendLine("GetParts(filter 22001<>'') EXCEPTION: " + exFilter.Message);
                        try { parts = db.GetParts(new MDObjectFilter()); }   // fallback: empty filter
                        catch (Exception exAll) { _log.AppendLine("GetParts(empty filter) EXCEPTION: " + exAll.Message); }
                    }

                    if (parts != null)
                    {
                        count = parts.Length;
                        _log.AppendLine("parts found       : " + count);
                        if (count > 0)
                        {
                            try { firstPart = parts[0].PartNr; } catch (Exception ex) { firstPart = "(PartNr threw: " + ex.Message + ")"; }
                            _log.AppendLine("first part number : " + firstPart);

                            // Dump the REAL MDPart property surface for the reference docs.
                            _log.AppendLine();
                            _log.AppendLine("--- MDPart public properties (reflection, runtime truth) ---");
                            try
                            {
                                PropertyInfo[] props = parts[0].GetType().GetProperties();
                                string[] names = new string[props.Length];
                                for (int i = 0; i < props.Length; i++)
                                    names[i] = props[i].Name + " : " + props[i].PropertyType.Name;
                                Array.Sort(names);
                                foreach (string n in names) _log.AppendLine("  " + n);
                            }
                            catch (Exception ex) { _log.AppendLine("  reflection dump EXCEPTION: " + ex.Message); }
                        }
                        ok = true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _log.AppendLine("RUNTIME EXCEPTION: " + ex);
        }

        string logPath = WriteLog();
        Tell(
            "Probe_MasterData " + ScriptVersion + "\n\n" +
            "COMPILED — Eplan.EplApi.MasterData IS allowed in a simple script.\n\n" +
            "runtime OK : " + ok + "\n" +
            "parts found: " + count + "\n" +
            "first part : " + firstPart + "\n\n" +
            "Full detail (incl. MDPart property dump): " + logPath);
    }

    // =====================================================================

    private string WriteLog()
    {
        try
        {
            string dir = Path.Combine(Path.GetTempPath(), "EPLAN_Scripts");
            Directory.CreateDirectory(dir);
            string p = Path.Combine(dir, "Probe_MasterData.log");
            File.AppendAllText(p, _log.ToString() + Environment.NewLine, new UTF8Encoding(true));
            return p;
        }
        catch { return "(log write failed)"; }
    }

    private void Tell(string message)
    {
        try
        {
            new Decider().Decide(EnumDecisionType.eOkDecision, message, "Probe_MasterData",
                EnumDecisionReturn.eOK, EnumDecisionReturn.eOK);
        }
        catch { }
    }
}
