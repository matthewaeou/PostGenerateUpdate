// FinalizeProject_Manual.cs
//
// FALLBACK / INSURANCE script. Only needed if the in-generation PDF export
// (PostGenerationExports_eBuild.cs) still fails during eBuild generation
// because the print/PDF engine isn't available in that context.
//
// This runs in the NORMAL interactive EPLAN context, where PDF export is
// known to work (same as the menu PDF export dialog). Run it AFTER an eBuild
// generation finishes:
//   1. Single-click the just-generated project in the Pages navigator.
//   2. Utilities > Scripts > Run...  -> pick this file -> Run().
//
// It re-uses the project's own DOCS folder and the same schemes as the
// unattended exports script, so output lands in the same place.

using System;
using System.IO;
using System.Text;
using Eplan.EplApi.ApplicationFramework;
using Eplan.EplApi.HEServices;
using Eplan.EplApi.DataModel;

public class FinalizeProjectManual
{
    private const string ScriptVersion   = "2026-06-05.9";
    private const string PdfScheme       = "Default";
    private const string PartsListScheme = "Summarized parts list";
    private const string Language        = "en_US";

    [Start]
    public void Run()
    {
        StringBuilder log = new StringBuilder();
        CommandLineInterpreter cli = new CommandLineInterpreter();

        string projectPath;
        try
        {
            // Resolve the currently selected/open project (interactive context).
            SelectionSet sel = new SelectionSet();
            Project project = sel.GetCurrentProject(true);
            projectPath = project.ProjectLinkFilePath;   // full path to the .elk
        }
        catch (Exception ex)
        {
            new Decider().Decide(
                EnumDecisionType.eOkDecision,
                "Could not resolve the current project. Select the project node "
                + "in the Pages navigator first.\n\n" + ex.Message,
                "Finalize project", EnumDecisionReturn.eOK, EnumDecisionReturn.eOK);
            return;
        }

        string docsPath    = Path.Combine(Path.ChangeExtension(projectPath, ".edb"), "DOCS");
        string projectBase = Path.GetFileNameWithoutExtension(projectPath);
        string logPath     = Path.Combine(docsPath, "PostGenerationExports.log");
        Directory.CreateDirectory(docsPath);

        string pdfFile   = Path.Combine(docsPath, projectBase + ".pdf");
        string partsFile = Path.Combine(docsPath, "Parts_List.xlsx");

        log.AppendLine("=== Manual finalize " + DateTime.Now + " ===");
        log.AppendLine("Script version : " + ScriptVersion);
        log.AppendLine("Project : " + projectPath);

        // Parts list (data-model; works in any context).
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
        catch (Exception ex) { pdfOk = false; log.AppendLine("PDF EXCEPTION   : " + ex.Message); }
        log.AppendLine("PDF export      : " + pdfOk);
        log.AppendLine("PDF file exists : " + File.Exists(pdfFile));

        try { File.AppendAllText(logPath, log.ToString() + Environment.NewLine); } catch { }

        new Decider().Decide(
            EnumDecisionType.eOkDecision,
            log.ToString(),
            "Finalize project result",
            EnumDecisionReturn.eOK, EnumDecisionReturn.eOK);
    }

    private static bool Exec(CommandLineInterpreter cli, string command)
    {
        try { return cli.Execute(command); }
        catch { return false; }
    }
}
