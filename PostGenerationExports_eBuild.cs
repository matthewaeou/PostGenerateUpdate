// PostGenerationExports_eBuild.cs
//
// Exports PDF and summarized parts list to the project's DOCS folder
// after eBuild generation. Log is written to the same DOCS folder.
//
// PROBE MODE: the PDF export action name is not yet confirmed for this
// EPLAN module, so a list of candidate commands is tried in one run. The
// log shows which (if any) returns True. Once known, collapse to the winner.

using System;
using System.IO;
using System.Text;
using Eplan.EplApi.ApplicationFramework;

public class PostGenerationExports
{
    private const string PdfScheme       = "Default";
    private const string PartsListScheme = "Summarized parts list";
    private const string Language        = "en_US";

    private CommandLineInterpreter _cli;
    private StringBuilder _log;

    // Run one action, log its result, never throw out of the batch.
    private bool Try(string label, string command)
    {
        try
        {
            bool ok = _cli.Execute(command);
            _log.AppendLine(label.PadRight(34) + ": " + ok);
            return ok;
        }
        catch (Exception ex)
        {
            _log.AppendLine(label.PadRight(34) + ": EXCEPTION " + ex.Message);
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
        _log.AppendLine("Project : " + ProjectName);

        string pdfFile   = Path.Combine(docsPath, projectBase + ".pdf");
        string partsFile = Path.Combine(docsPath, "Parts_List.xlsx");

        string proj = " /PROJECTNAME:\"" + ProjectName + "\"";
        string pdf  = " /EXPORTFILE:\"" + pdfFile + "\"";
        string pscm = " /EXPORTSCHEME:\"" + PdfScheme + "\"";

        // ---- Parts list (label action + /LANGUAGE, confirmed param) ----
        Try("PL label",
            "label /CONFIGSCHEME:\"" + PartsListScheme + "\" /EXPORTFILE:\"" +
            partsFile + "\" /LANGUAGE:" + Language + proj);

        // ---- PDF: probe candidate action names, stop on first success ----
        string[][] pdfCandidates = new string[][]
        {
            new[] { "PDF export",                "export"                  + pdf + pscm + proj },
            new[] { "PDF print",                 "print"                   + pdf + pscm + proj },
            new[] { "PDF EplApiExportPdf",       "EplApiExportPdf"         + pdf + pscm + proj },
            new[] { "PDF PdfExport",             "PdfExport"               + pdf + pscm + proj },
            new[] { "PDF XPrjActionExportPdf",   "XPrjActionExportPdf"     + pdf + pscm + proj },
            new[] { "PDF XMExportPdfPrjAction",  "XMExportPdfPrjAction"    + pdf + pscm + proj },
            new[] { "PDF ExportPdfAction",       "ExportPdfAction"         + pdf + pscm + proj },
            new[] { "PDF XGedExportImageToPDF",  "XGedExportImageToPDF"    + pdf + pscm + proj },
        };

        foreach (string[] c in pdfCandidates)
        {
            if (Try(c[0], c[1])) break;
        }

        try { File.AppendAllText(logPath, _log.ToString() + Environment.NewLine); } catch { }
    }
}
