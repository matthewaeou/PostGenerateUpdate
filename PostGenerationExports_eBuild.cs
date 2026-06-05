// PostGenerationExports_eBuild.cs
//
// Exports PDF and summarized parts list to the project's DOCS folder
// after eBuild generation. Log is written to the same DOCS folder.

using System;
using System.IO;
using System.Text;
using Eplan.EplApi.ApplicationFramework;

public class PostGenerationExports
{
    private const string PdfScheme       = "Default";
    private const string PartsListScheme = "Summarized parts list";
    private const string Language        = "en_US";

    [Start]
    public void RunFromEBuild(string ProjectName)
    {
        string docsPath    = Path.Combine(Path.ChangeExtension(ProjectName, ".edb"), "DOCS");
        string projectBase = Path.GetFileNameWithoutExtension(ProjectName);
        string logPath     = Path.Combine(docsPath, "PostGenerationExports.log");
        Directory.CreateDirectory(docsPath);

        CommandLineInterpreter cli = new CommandLineInterpreter();
        StringBuilder log = new StringBuilder();
        log.AppendLine("=== Post-generation exports " + DateTime.Now + " ===");
        log.AppendLine("Project : " + ProjectName);

        try
        {
            string pdfFile   = Path.Combine(docsPath, projectBase + ".pdf");
            string partsFile = Path.Combine(docsPath, "Parts_List.xlsx");

            // --- Parts list: label action needs /LANGUAGE ---
            bool r2 = cli.Execute(
                "label" +
                " /CONFIGSCHEME:\"" + PartsListScheme + "\"" +
                " /EXPORTFILE:\"" + partsFile + "\"" +
                " /LANGUAGE:" + Language +
                " /PROJECTNAME:\"" + ProjectName + "\"");
            log.AppendLine("PL  label +LANGUAGE +PROJECTNAME  : " + r2);

            // --- PDF: try remaining action name candidates ---
            bool r1a = cli.Execute(
                "EPlanExportPDF" +
                " /EXPORTFILE:\"" + pdfFile + "\"" +
                " /EXPORTSCHEME:\"" + PdfScheme + "\"" +
                " /PROJECTNAME:\"" + ProjectName + "\"");
            log.AppendLine("PDF EPlanExportPDF                : " + r1a);

            if (!r1a)
            {
                bool r1b = cli.Execute(
                    "XPrjActionPDFExport" +
                    " /EXPORTFILE:\"" + pdfFile + "\"" +
                    " /EXPORTSCHEME:\"" + PdfScheme + "\"" +
                    " /PROJECTNAME:\"" + ProjectName + "\"");
                log.AppendLine("PDF XPrjActionPDFExport           : " + r1b);
            }

            if (!r1a)
            {
                bool r1c = cli.Execute(
                    "XGedPrintPDF" +
                    " /EXPORTFILE:\"" + pdfFile + "\"" +
                    " /EXPORTSCHEME:\"" + PdfScheme + "\"" +
                    " /PROJECTNAME:\"" + ProjectName + "\"");
                log.AppendLine("PDF XGedPrintPDF                  : " + r1c);
            }
        }
        catch (Exception ex)
        {
            log.AppendLine("EXCEPTION: " + ex.Message);
        }

        try { File.AppendAllText(logPath, log.ToString() + Environment.NewLine); } catch { }
    }
}
