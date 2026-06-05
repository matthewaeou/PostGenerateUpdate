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

            // --- PDF: try known action name variants ---
            bool r1a = cli.Execute(
                "XGedExportPDF" +
                " /EXPORTFILE:\"" + pdfFile + "\"" +
                " /EXPORTSCHEME:\"" + PdfScheme + "\"" +
                " /PROJECTNAME:\"" + ProjectName + "\"");
            log.AppendLine("PDF XGedExportPDF +PROJECTNAME   : " + r1a);

            if (!r1a)
            {
                bool r1b = cli.Execute(
                    "XEsExportPDF" +
                    " /EXPORTFILE:\"" + pdfFile + "\"" +
                    " /EXPORTSCHEME:\"" + PdfScheme + "\"" +
                    " /PROJECTNAME:\"" + ProjectName + "\"");
                log.AppendLine("PDF XEsExportPDF +PROJECTNAME    : " + r1b);
            }

            // --- Parts list: try known action name variants ---
            bool r2a = cli.Execute(
                "XPrjActionLabelingExport" +
                " /CONFIGSCHEME:\"" + PartsListScheme + "\"" +
                " /EXPORTFILE:\"" + partsFile + "\"" +
                " /PROJECTNAME:\"" + ProjectName + "\"");
            log.AppendLine("PL  XPrjActionLabelingExport      : " + r2a);

            if (!r2a)
            {
                bool r2b = cli.Execute(
                    "label" +
                    " /CONFIGSCHEME:\"" + PartsListScheme + "\"" +
                    " /EXPORTFILE:\"" + partsFile + "\"");
                log.AppendLine("PL  label -PROJECTNAME            : " + r2b);
            }

            if (!r2a)
            {
                bool r2c = cli.Execute(
                    "label" +
                    " /SETTINGS:\"" + PartsListScheme + "\"" +
                    " /EXPORTFILE:\"" + partsFile + "\"" +
                    " /PROJECTNAME:\"" + ProjectName + "\"");
                log.AppendLine("PL  label /SETTINGS +PROJECTNAME  : " + r2c);
            }
        }
        catch (Exception ex)
        {
            log.AppendLine("EXCEPTION: " + ex.Message);
        }

        try { File.AppendAllText(logPath, log.ToString() + Environment.NewLine); } catch { }
    }
}
