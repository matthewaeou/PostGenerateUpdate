// PostGenerationExports_eBuild.cs
//
// Exports the summarized parts list and a project PDF to the project's
// DOCS folder after eBuild generation. Log is written to the same folder.
//
// PDF export uses the documented EPLAN 'export' action with
// TYPE=PDFPROJECTSCHEME, invoked via ActionCallingContext (the canonical,
// path-safe form). Earlier failures were wrong /TYPE values, not a blocked
// context. Ref: EPLAN 'export' action docs + github.com/musray/PDFPerLocation.

using System;
using System.IO;
using System.Text;
using Eplan.EplApi.ApplicationFramework;

public class PostGenerationExports
{
    private const string ScriptVersion   = "2026-06-05.10";
    // Scheme NAME (not the dialog label). EPLAN reported the valid name as
    // 'EPLAN_default_value' — the PDF dialog just displays it as "Default".
    private const string PdfScheme       = "EPLAN_default_value";
    private const string PartsListScheme = "Summarized parts list";
    private const string Language        = "en_US";

    private CommandLineInterpreter _cli;
    private StringBuilder _log;

    // Execute a command STRING, log the result, never throw out of the batch.
    private bool Try(string label, string command)
    {
        try
        {
            bool ok = _cli.Execute(command);
            _log.AppendLine(label.PadRight(30) + ": " + ok);
            return ok;
        }
        catch (Exception ex)
        {
            _log.AppendLine(label.PadRight(30) + ": EXCEPTION " + ex.Message);
            return false;
        }
    }

    // Execute an action via ActionCallingContext (key/value pairs), log result.
    private bool TryCtx(string label, string action, params string[] kv)
    {
        try
        {
            ActionCallingContext ctx = new ActionCallingContext();
            for (int i = 0; i + 1 < kv.Length; i += 2)
                ctx.AddParameter(kv[i], kv[i + 1]);
            bool ok = _cli.Execute(action, ctx);
            _log.AppendLine(label.PadRight(30) + ": " + ok);
            return ok;
        }
        catch (Exception ex)
        {
            _log.AppendLine(label.PadRight(30) + ": EXCEPTION " + ex.Message);
            return false;
        }
    }

    [Start]
    public void RunFromEBuild(string ProjectName)
    {
        string docsPath    = Path.Combine(Path.ChangeExtension(ProjectName, ".edb"), "DOCS");
        string projectBase = Path.GetFileNameWithoutExtension(ProjectName);
        string logPath     = Path.Combine(docsPath, "PostGenerationExports.log");
        Directory.CreateDirectory(docsPath);

        _cli = new CommandLineInterpreter();
        _log = new StringBuilder();
        _log.AppendLine("=== Post-generation exports " + DateTime.Now + " ===");
        _log.AppendLine("Script version : " + ScriptVersion);
        _log.AppendLine("Project : " + ProjectName);

        string pdfFile   = Path.Combine(docsPath, projectBase + ".pdf");
        string partsFile = Path.Combine(docsPath, "Parts_List.xlsx");

        // ---- Parts list (label action + /LANGUAGE, confirmed working) ----
        Try("PL label",
            "label /CONFIGSCHEME:\"" + PartsListScheme + "\" /EXPORTFILE:\"" +
            partsFile + "\" /LANGUAGE:" + Language +
            " /PROJECTNAME:\"" + ProjectName + "\"");

        // ---- PDF: correct action is 'export' with TYPE=PDFPROJECTSCHEME ----
        // Try several parameter shapes in ONE run; stop at first success. The
        // file-exists check below is the ground truth regardless of which wins.

        // 1) ctx, full params (documented, path-safe form).
        bool pdfOk = TryCtx("PDF ctx full", "export",
            "TYPE",        "PDFPROJECTSCHEME",
            "EXPORTSCHEME", PdfScheme,
            "EXPORTFILE",   pdfFile,
            "PROJECTNAME",  ProjectName,
            "LANGUAGE",     Language);

        // 2) ctx, no LANGUAGE (in case export rejects the language code).
        if (!pdfOk)
            pdfOk = TryCtx("PDF ctx no-LANG", "export",
                "TYPE",        "PDFPROJECTSCHEME",
                "EXPORTSCHEME", PdfScheme,
                "EXPORTFILE",   pdfFile,
                "PROJECTNAME",  ProjectName);

        // 3) ctx, no PROJECTNAME (use active project, as the reference example).
        if (!pdfOk)
            pdfOk = TryCtx("PDF ctx no-PROJ", "export",
                "TYPE",        "PDFPROJECTSCHEME",
                "EXPORTSCHEME", PdfScheme,
                "EXPORTFILE",   pdfFile);

        // 4) command-string equivalent, in case the ctx path behaves differently.
        if (!pdfOk)
            pdfOk = Try("PDF cli full",
                "export /TYPE:PDFPROJECTSCHEME" +
                " /EXPORTFILE:\"" + pdfFile + "\"" +
                " /EXPORTSCHEME:\"" + PdfScheme + "\"" +
                " /PROJECTNAME:\"" + ProjectName + "\"");

        // Independent confirmation: did the file actually land on disk?
        _log.AppendLine("PDF file exists : " + File.Exists(pdfFile));
        _log.AppendLine("PDF path        : " + pdfFile);

        try { File.AppendAllText(logPath, _log.ToString() + Environment.NewLine); } catch { }
    }
}
