// RunEngravingImport.cs
//
// Launcher: triggers the EngravingDataImport action provided by the add-in
// Eplan.EplAddIn.EngravingData.dll. The add-in must be REGISTERED and LOADED first
// (EPLAN > Utilities > API > Add-Ins... > Add... > pick the DLL).
//
// Reads <project>.edb\DOC\EngravingData.csv and writes the FunctionText + EngravingText
// columns back onto the matching functions (matched by the Key column = device tag).
//
// ⚠️ This MODIFIES the project. Test on a COPY first. Run: select the project node, then
//    Utilities > Scripts > Run...  ->  RunEngravingImport  ->  Run().

public class RunEngravingImport
{
    [Start]
    public void Run()
    {
        new CommandLineInterpreter().Execute("EngravingDataImport");
    }
}
