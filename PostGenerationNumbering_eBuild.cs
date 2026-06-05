// PostGenerationNumbering_eBuild.cs
//
// DEPLOYMENT copy for eBuild Script-Typicals. Runs unattended after the
// Project Builder finishes generation. No dialogs: results are appended to a
// log file in the project's DOCS folder.
//
// Setup:
//   Attach this under Script-Typicals in the Designer. ProjectName is supplied
//   automatically by the generator and must NOT be declared as a script
//   parameter in the Designer.

using System;
using System.IO;
using System.Text;
using Eplan.EplApi.ApplicationFramework;

public class PostGenerationNumbering
{
    private const string DeviceScheme     = "ECLIPSE ROW_IDENTIFIER";
    private const string ConnectionScheme = "Eclipse NFPA Standard without PLC address";

    [Start]
    public void RunFromEBuild(string ProjectName)
    {
        string docsPath = Path.Combine(Path.ChangeExtension(ProjectName, ".edb"), "DOCS");
        Directory.CreateDirectory(docsPath);
        string logPath = Path.Combine(docsPath, "PostGenerationNumbering.log");

        CommandLineInterpreter cli = new CommandLineInterpreter();
        StringBuilder log = new StringBuilder();
        log.AppendLine("=== Post-generation numbering " + DateTime.Now + " ===");
        log.AppendLine("Project: " + ProjectName);

        try
        {
            bool r1 = cli.Execute("generate /TYPE:CONNECTIONS");
            log.AppendLine("generate connections : " + r1);

            string devCmd =
                "renumber /TYPE:DEVICES /CONFIGSCHEME:\"" + DeviceScheme + "\" " +
                "/PROJECTNAME:\"" + ProjectName + "\" " +
                "/STARTVALUE:1 /STEPVALUE:1 " +
                "/ALSONUMERATEDBYPLC:0 /POSTNUMERATE:0";
            bool r2 = cli.Execute(devCmd);
            log.AppendLine("renumber devices     : " + r2);

            string conCmd =
                "renumber /TYPE:CONNECTIONS /CONFIGSCHEME:\"" + ConnectionScheme + "\" " +
                "/PROJECTNAME:\"" + ProjectName + "\"";
            bool r3 = cli.Execute(conCmd);
            log.AppendLine("renumber connections : " + r3);
        }
        catch (Exception ex)
        {
            log.AppendLine("EXCEPTION: " + ex.Message);
        }

        try { File.AppendAllText(logPath, log.ToString() + Environment.NewLine); } catch { }
    }
}
