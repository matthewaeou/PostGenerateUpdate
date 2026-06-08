// PostGenerationNumbering.cs
//
// Regenerates connections, renumbers device tags, and renumbers connections
// (wire numbers) on the currently SELECTED project.
//
// IMPORTANT for manual testing:
//   The renumber action with no PROJECTNAME acts on the SELECTED project.
//   Before Utilities > Scripts > Run..., single-click the project node in the
//   Pages navigator so it is selected, otherwise the actions find nothing and
//   silently do nothing.
//
// Entry points:
//   TESTING:    RunManual()  -> Utilities > Scripts > Run...
//   DEPLOYMENT: comment out RunManual, uncomment RunFromEBuild. eBuild's
//               Project Builder supplies ProjectName automatically.

using System;
using System.IO;
using System.Text;
using Eplan.EplApi.ApplicationFramework;
using Eplan.EplApi.Base;

public class PostGenerationNumbering
{
    // Scheme names exactly as they appear in the project settings (case sensitive).
    private const string DeviceScheme     = "ECLIPSE ROW_IDENTIFIER";
    private const string ConnectionScheme = "Eclipse NFPA Standard without PLC address";

    // ---- TESTING entry point -----------------------------------------------
    [Start]
    public void RunManual()
    {
        Execute();
        return;
    }

    // ---- DEPLOYMENT entry point (eBuild) -----------------------------------
    // [Start]
    // public void RunFromEBuild(string ProjectName)
    // {
    //     Execute();
    //     return;
    // }

    private void Execute()
    {
        CommandLineInterpreter cli = new CommandLineInterpreter();
        StringBuilder log = new StringBuilder();
        log.AppendLine("=== PostGenerationNumbering  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
        log.AppendLine("user    : " + Environment.UserName + " @ " + Environment.MachineName);

        // Resolve current project path via selectionset (no DataModel needed).
        string projectPath = "";
        try
        {
            ActionCallingContext ctxSS = new ActionCallingContext();
            ctxSS.AddParameter("TYPE", "PROJECT");
            cli.Execute("selectionset", ctxSS);
            ctxSS.GetParameter("PROJECTNAME", ref projectPath);
        }
        catch { projectPath = ""; }
        log.AppendLine("project : " + (string.IsNullOrEmpty(projectPath) ? "(none selected)" : projectPath));

        try
        {
            // 1) Regenerate connections so definition points exist.
            bool r1 = cli.Execute("generate /TYPE:CONNECTIONS");
            log.AppendLine("generate connections : " + r1);

            // 2) Renumber DEVICE tags across the whole project.
            //    No IDENTIFIER parameter, so numbering is NOT scoped to one
            //    identifier letter. POSTNUMERATE:0 renumbers all (not just "?").
            string devCmd =
                "renumber /TYPE:DEVICES /CONFIGSCHEME:\"" + DeviceScheme + "\" " +
                "/STARTVALUE:1 /STEPVALUE:1 /USESELECTION:0 " +
                "/ALSONUMERATEDBYPLC:0 /POSTNUMERATE:0";
            bool r2 = cli.Execute(devCmd);
            log.AppendLine("renumber devices     : " + r2);

            // 3) Renumber CONNECTIONS (wire numbers).
            string conCmd =
                "renumber /TYPE:CONNECTIONS /CONFIGSCHEME:\"" + ConnectionScheme + "\"";
            bool r3 = cli.Execute(conCmd);
            log.AppendLine("renumber connections : " + r3);
        }
        catch (Exception ex)
        {
            log.AppendLine("EXCEPTION: " + ex);
        }

        // Write log to project's DOC folder; fall back to %TEMP% if no project resolved.
        WriteLog(log.ToString(), projectPath);

        // Show a summary so a silent no-op is never invisible again.
        new Decider().Decide(
            EnumDecisionType.eOkDecision,
            log.ToString(),
            "Post-generation numbering result",
            EnumDecisionReturn.eOK,
            EnumDecisionReturn.eOK);
    }

    private static void WriteLog(string text, string projectPath)
    {
        string dir;
        try
        {
            dir = (!string.IsNullOrEmpty(projectPath) && projectPath.EndsWith(".elk"))
                ? Path.Combine(Path.ChangeExtension(projectPath, ".edb"), "DOC")
                : Path.Combine(Path.GetTempPath(), "EPLAN_Scripts");
            Directory.CreateDirectory(dir);
        }
        catch { dir = Path.GetTempPath(); }
        try { File.AppendAllText(Path.Combine(dir, "PostGenerationNumbering.log"),
                                 text + Environment.NewLine, new UTF8Encoding(true)); }
        catch { }
    }
}
