// ExportEngravingData.cs
//
// Exports field-item nameplate data for round-trip editing. Includes every
// function that has a non-empty Engraving text property (FUNC_GRAVINGTEXT),
// i.e. the items shown in the mechanical model.
//
// Output: <project>.edb\DOCS\EngravingData.csv  (UTF-8, opens in Excel)
//   Columns: Key, DT, Page, Location, PartNumber, FunctionText, EngravingText
//   - Key is the full identifying device tag; the import matches on it, so do
//     NOT edit the Key column.
//   - FunctionText and EngravingText are the editable columns.
//   - Newlines inside a text value are written as the literal token \n so each
//     record stays on one line; the import converts \n back to real newlines.
//
// Run: select the project in the Pages navigator, then
//      Utilities > Scripts > Run... -> ExportEngravingData -> Run().

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Eplan.EplApi.ApplicationFramework;
using Eplan.EplApi.HEServices;
using Eplan.EplApi.DataModel;
using Eplan.EplApi.DataModel.Filters;
using Eplan.EplApi.Base;

public class ExportEngravingData
{
    private const string ScriptVersion = "2026-06-06.1";

    [Start]
    public void Run()
    {
        Project project;
        try
        {
            project = new SelectionSet().GetCurrentProject(true);
        }
        catch (Exception ex)
        {
            Tell("Could not resolve the current project. Select the project node "
                 + "in the Pages navigator first.\n\n" + ex.Message);
            return;
        }

        string elk      = project.ProjectLinkFilePath;
        string docsPath  = Path.Combine(Path.ChangeExtension(elk, ".edb"), "DOCS");
        Directory.CreateDirectory(docsPath);
        string csvPath   = Path.Combine(docsPath, "EngravingData.csv");

        StringBuilder csv = new StringBuilder();
        csv.AppendLine(Row("Key", "DT", "Page", "Location",
                           "PartNumber", "FunctionText", "EngravingText"));

        var seenKeys = new HashSet<string>();
        int rows = 0, dupes = 0;

        DMObjectsFinder finder = new DMObjectsFinder(project);
        Function[] functions = finder.GetFunctions(new FunctionsFilter());

        foreach (Function f in functions)
        {
            string engraving = Read(f, Properties.Function.FUNC_GRAVINGTEXT);
            if (string.IsNullOrEmpty(engraving))
                continue;

            string key = SafeName(f);
            if (!seenKeys.Add(key))
                dupes++;   // two occurrences share a DT and both carry engraving

            string dt       = SafeVisibleName(f);
            string page     = SafePage(f);
            string location = Read(f, Properties.Function.FUNC_MOUNTINGLOCATION);
            string part     = ReadIndexed(f, Properties.Function.FUNC_ARTICLE_PARTNR, 1);
            string func     = Read(f, Properties.Function.FUNC_TEXT);

            csv.AppendLine(Row(key, dt, page, location, part, func, engraving));
            rows++;
        }

        File.WriteAllText(csvPath, csv.ToString(), new UTF8Encoding(true));

        Tell("Script version : " + ScriptVersion + "\n"
             + "Exported " + rows + " engraved item(s).\n"
             + (dupes > 0 ? "WARNING: " + dupes + " duplicate key(s) — re-import may "
                            + "be ambiguous for those.\n" : "")
             + "\nFile: " + csvPath);
    }

    // ---- property reads (defensive: never throw) ---------------------------

    private static string Read(Function f, AnyPropertyId id)
    {
        try
        {
            PropertyValue pv = f.Properties[id];
            if (pv.IsEmpty) return "";
            return pv.ToString();
        }
        catch { return ""; }
    }

    private static string ReadIndexed(Function f, AnyPropertyId id, int index)
    {
        try
        {
            PropertyValue pv = f.Properties[id, index];
            if (pv.IsEmpty) return "";
            return pv.ToString();
        }
        catch { return ""; }
    }

    private static string SafeName(Function f)
    {
        try { return f.Name; } catch { return ""; }
    }

    private static string SafeVisibleName(Function f)
    {
        try { return f.VisibleName; } catch { return ""; }
    }

    private static string SafePage(Function f)
    {
        try { return f.Page.Name; } catch { return ""; }
    }

    // ---- CSV helpers -------------------------------------------------------

    private static string Row(params string[] fields)
    {
        for (int i = 0; i < fields.Length; i++)
            fields[i] = Field(fields[i]);
        return string.Join(",", fields);
    }

    private static string Field(string s)
    {
        if (s == null) s = "";
        s = s.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\\n");
        if (s.IndexOf('"') >= 0 || s.IndexOf(',') >= 0)
            s = "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    private static void Tell(string text)
    {
        new Decider().Decide(
            EnumDecisionType.eOkDecision, text, "Export engraving data",
            EnumDecisionReturn.eOK, EnumDecisionReturn.eOK);
    }
}
