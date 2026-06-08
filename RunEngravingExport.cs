// RunEngravingExport.cs
//
// Launcher: triggers the EngravingDataExport action provided by the add-in
// Eplan.EplAddIn.EngravingData.dll. The add-in must be REGISTERED and LOADED first
// (EPLAN > Utilities > API > Add-Ins... > Add... > pick the DLL).
//
// Run: select the project node in the Pages navigator, then
//      Utilities > Scripts > Run...  ->  RunEngravingExport  ->  Run().
//
// If this fails with a locking error (NoLockingStepException), the script context can't
// hand the action a writable project — trigger the action from the EPLAN GUI instead
// (Utilities > API), and tell me; I'll add a ribbon/menu button to the add-in.

public class RunEngravingExport
{
    [Start]
    public void Run()
    {
        // CommandLineInterpreter is in Eplan.EplApi.ApplicationFramework (auto-injected).
        new CommandLineInterpreter().Execute("EngravingDataExport");
    }
}
