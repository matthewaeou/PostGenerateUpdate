// PostGenerationNumbering_eBuild.cs
//
// DEPLOYMENT copy for eBuild Script-Typicals. Runs unattended after the
// Project Builder finishes generation. No dialogs: results are appended to a
// log file so a hands-off generation run is never blocked waiting for a click.
//
// Setup:
//   Attach this under Script-Typicals in the Designer. ProjectName is supplied
//   automatically by the generator and must NOT be declared as a script
//   parameter in the Designer.
//
// Notes:
//   - The actions run against the just-generated (active) project, the same
//     way the manual test did against the selected project. No PROJECTNAME is
//     passed, so USESELECTION:0 stays valid (the two are mutually exclusive).
//   - Edit LogPath if you want the log somewhere more convenient than temp.

using System;
using System.IO;
using System.Text;
using Eplan.EplApi.ApplicationFramework;

public class PostGenerationNumbering
{
    // Scheme names exactly as they appear in the project settings (case sensitive).
    private const string DeviceScheme     = "ECLIPSE ROW_IDENTIFIER";
    private const string ConnectionScheme = "Eclipse NFPA Standard without PLC address";

    // Silent log. Resolves to the Windows temp folder, e.g.
    // C:\Users\<you>\AppData\Local\Temp\PostGenerationNumbering.log
    private static readonly string LogPath =
        Path.Combine(Path.GetTempPath(), "PostGenerationNumbering.log");

    [Start]
    public void RunFromEBuild(string ProjectName)
    {
        CommandLineInterpreter cli = new CommandLineInterpreter();
        StringBuilder log = new StringBuilder();
        log.AppendLine("=== Post-generation numbering " + DateTime.Now + " ===");
        log.AppendLine("Project: " + ProjectName);

        try
        {
            // 1) Regenerate connections so definition points exist.
            bool r1 = cli.Execute("generate /TYPE:CONNECTIONS");
            log.AppendLine("generate connections : " + r1);

            // 2) Renumber DEVICE tags across the whole project.
            //    Use PROJECTNAME instead of USESELECTION so the action targets
            //    the correct project in the eBuild context.
            string devCmd =
                "renumber /TYPE:DEVICES /CONFIGSCHEME:\"" + DeviceScheme + "\" " +
                "/PROJECTNAME:\"" + ProjectName + "\" " +
                "/STARTVALUE:1 /STEPVALUE:1 " +
                "/ALSONUMERATEDBYPLC:0 /POSTNUMERATE:0";
            bool r2 = cli.Execute(devCmd);
            log.AppendLine("renumber devices     : " + r2);

            // 3) Renumber CONNECTIONS (wire numbers).
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

        WriteLog(log.ToString());
        return;
    }

    private static void WriteLog(string text)
    {
        try
        {
            File.AppendAllText(LogPath, text + Environment.NewLine);
        }
        catch
        {
            // Never let a logging failure break a generation run.
        }
    }
}
