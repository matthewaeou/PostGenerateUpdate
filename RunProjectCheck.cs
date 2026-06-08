// RunProjectCheck.cs
//
// Launcher: triggers the ProjectCheck action provided by the add-in
// Eplan.EplAddIn.ProjectCheck.dll. The add-in must be REGISTERED and LOADED
// (EPLAN > Utilities > API > Add-Ins... > Add... > pick the DLL).
//
// Run: select the project node in the Pages navigator, then
//      Utilities > Scripts > Run...  ->  RunProjectCheck  ->  Run().
//
// Results: summary dialog + <project>.edb\DOC\ProjectCheck.log

public class RunProjectCheck
{
    [Start]
    public void Run()
    {
        new CommandLineInterpreter().Execute("ProjectCheck");
    }
}
