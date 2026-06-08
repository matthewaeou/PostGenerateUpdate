// FinalizeProject_Manual.cs
//
// Manual "finalize" for the CURRENTLY SELECTED project: exports the summarized
// parts list (xlsx) and a whole-project PDF into <project>.edb\DOC.
//
// Runs in the normal interactive EPLAN context — via Utilities > Scripts > Run, or
// the "Finalize Project" button on the EPLANCA ribbon tab — where the PDF/print
// engine is available. Handy as a one-click re-export after manual edits, and as a
// fallback if the in-generation export (PostGenerationExports_eBuild.cs) ever fails.
//
// HOW TO RUN: single-click the project node in the Pages navigator first (the label
// and export actions act on the selected project), then run the script / click the
// ribbon button.
//
// NOTE: simple scripts cannot reference the DataModel / HEServices assemblies on this
// EPLAN build (CS0234), so the project path is resolved via the 'selectionset' action
// — NOT SelectionSet.GetCurrentProject (which also throws NoLockingStepException in a
// script). See memory: eplan-simple-script-assembly-limits.

using System;
using System.IO;
using System.Text;
using Eplan.EplApi.ApplicationFramework;
using Eplan.EplApi.Base;          // Decider, EnumDecisionType, EnumDecisionReturn

public class FinalizeProjectManual
{
    private const string ScriptVersion   = "2026-06-08.1";
    // Scheme NAME (not the dialog label). EPLAN reports the PDF scheme's valid name
    // as 'EPLAN_default_value' — the dialog just displays it as "Default".
    private const string PdfScheme       = "EPLAN_default_value";
    private const string PartsListScheme = "Summarized parts list";
    private const string Language        = "en_US";

    [Start]
    public void Run()
    {
        CommandLineInterpreter cli = new CommandLineInterpreter();
        StringBuilder log = new StringBuilder();
        log.AppendLine("=== FinalizeProject  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
        log.AppendLine("Script version : " + ScriptVersion);
        log.AppendLine("user           : " + Environment.UserName + " @ " + Environment.MachineName);

        // Resolve the selected project path via the selectionset action (no DataModel).
        string projectPath = "";
        try
        {
            ActionCallingContext ss = new ActionCallingContext();
            ss.AddParameter("TYPE", "PROJECT");
            cli.Execute("selectionset", ss);
            ss.GetParameter("PROJECTNAME", ref projectPath);
            if (string.IsNullOrEmpty(projectPath)) ss.GetParameter("PROJECT", ref projectPath);
        }
        catch (Exception ex) { log.AppendLine("selectionset EXCEPTION : " + ex); projectPath = ""; }
        log.AppendLine("project        : " + (string.IsNullOrEmpty(projectPath) ? "(none selected)" : projectPath));

        if (string.IsNullOrEmpty(projectPath) || !projectPath.EndsWith(".elk"))
        {
            WriteLog(log.ToString(), projectPath);
            Tell("Could not resolve the current project.\n\nSingle-click the project node "
                 + "in the Pages navigator first, then run again.");
            return;
        }

        string docPath     = Path.Combine(Path.ChangeExtension(projectPath, ".edb"), "DOC");
        string projectBase = Path.GetFileNameWithoutExtension(projectPath);
        Directory.CreateDirectory(docPath);
        string pdfFile   = Path.Combine(docPath, projectBase + ".pdf");
        string partsFile = Path.Combine(docPath, "Parts_List.xlsx");

        // Parts list (label action; data-model export, works in any context).
        log.AppendLine("PL label        : " + Exec(cli,
            "label /CONFIGSCHEME:\"" + PartsListScheme + "\" /EXPORTFILE:\"" +
            partsFile + "\" /LANGUAGE:" + Language +
            " /PROJECTNAME:\"" + projectPath + "\""));

        // PDF (interactive context -> print engine available).
        ActionCallingContext ctx = new ActionCallingContext();
        ctx.AddParameter("TYPE",        "PDFPROJECTSCHEME");
        ctx.AddParameter("EXPORTSCHEME", PdfScheme);
        ctx.AddParameter("EXPORTFILE",   pdfFile);
        ctx.AddParameter("PROJECTNAME",  projectPath);
        bool pdfOk;
        try { pdfOk = cli.Execute("export", ctx); }
        catch (Exception ex) { pdfOk = false; log.AppendLine("PDF EXCEPTION   : " + ex); }
        log.AppendLine("PDF export      : " + pdfOk);
        log.AppendLine("PDF file exists : " + File.Exists(pdfFile));

        WriteLog(log.ToString(), projectPath);

        new Decider().Decide(
            EnumDecisionType.eOkDecision, log.ToString(),
            "Finalize project result", EnumDecisionReturn.eOK, EnumDecisionReturn.eOK);
    }

    private static bool Exec(CommandLineInterpreter cli, string command)
    {
        try { return cli.Execute(command); } catch { return false; }
    }

    // Write log to the project's DOC folder; fall back to %TEMP% if no project resolved.
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
        try { File.AppendAllText(Path.Combine(dir, "FinalizeProject.log"),
                                 text + Environment.NewLine, new UTF8Encoding(true)); }
        catch { }
    }

    private static void Tell(string text)
    {
        new Decider().Decide(EnumDecisionType.eOkDecision, text, "Finalize project",
            EnumDecisionReturn.eOK, EnumDecisionReturn.eOK);
    }
}
