// RunEngravingExport.cs
//
// Launcher: triggers the EngravingDataExport action provided by the add-in
// Eplan.EplAddIn.EngravingData.dll. The add-in must be REGISTERED and LOADED first
// (EPLAN > Utilities > API > Add-Ins... > Add... > pick the DLL).
//
// Run: select the project node in the Pages navigator, then either
//   - click "Export Engraving" on the EPLANCA ribbon tab (calls the same action), or
//   - Utilities > Scripts > Run...  ->  RunEngravingExport  ->  Run().
// The ribbon button (added by the ProjectCheck add-in's OnInitGui) is the quickest path.

public class RunEngravingExport
{
    [Start]
    public void Run()
    {
        // CommandLineInterpreter is in Eplan.EplApi.ApplicationFramework (auto-injected).
        new CommandLineInterpreter().Execute("EngravingDataExport");
    }
}
