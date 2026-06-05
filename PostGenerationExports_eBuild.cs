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
        log.AppendLine("DOCS    : " + docsPath);

        try
        {
            // 1) Export full project PDF.
            string pdfFile = Path.Combine(docsPath, projectBase + ".pdf");
            bool r1 = cli.Execute(
                "XGedExportPDF" +
                " /EXPORTFILE:\"" + pdfFile + "\"" +
                " /EXPORTSCHEME:\"" + PdfScheme + "\"");
            log.AppendLine("export PDF        : " + r1 + "  -> " + pdfFile);

            // 2) Export summarized parts list to Excel.
            string partsFile = Path.Combine(docsPath, "Parts_List.xlsx");
            bool r2 = cli.Execute(
                "label" +
                " /CONFIGSCHEME:\"" + PartsListScheme + "\"" +
                " /EXPORTFILE:\"" + partsFile + "\"" +
                " /PROJECTNAME:\"" + ProjectName + "\"");
            log.AppendLine("export parts list : " + r2 + "  -> " + partsFile);
        }
        catch (Exception ex)
        {
            log.AppendLine("EXCEPTION: " + ex.Message);
        }

        try { File.AppendAllText(logPath, log.ToString() + Environment.NewLine); } catch { }
    }
}
