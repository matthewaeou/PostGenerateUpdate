// Eplan.EplAddIn.EngravingData.cs
//
// EPLAN Electric P8 ADD-IN for the field-item engraving-text round trip.
// Built as a compiled add-in because the DataModel object model (Project / Function /
// properties) is NOT reachable from a "simple script": SelectionSet.GetCurrentProject
// throws NoLockingStepException S063110 in the script runtime. An add-in runs inside
// EPLAN's action framework, which HAS the locking context, so it can read AND write the
// model. (See docs\EPLAN-Scripting-Reference.md §0/§6.)
//
// Provides two actions (callable from a launcher script, the command line, or
// Utilities > API > Execute action):
//   EngravingDataExport  -> writes <project>.edb\DOC\EngravingData.csv
//   EngravingDataImport  -> reads that CSV back and writes FunctionText + EngravingText
//
// CSV columns: Key, DT, Page, Location, PartNumber, FunctionText, EngravingText
//   Key = Function.Name (full identifying DT) — the import matches on it; do NOT edit it.
//   Newlines inside a value are encoded as the literal token \n on export and decoded
//   on import, so each record stays on one physical line.
//
// Build: addin\build.ps1   ->   addin\bin\Eplan.EplAddIn.EngravingData.dll
// Deploy: EPLAN > Utilities > API > Add-Ins... > Add... > pick the DLL (load on start).

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Eplan.EplApi.ApplicationFramework;   // IEplAddIn, IEplAction, ActionProperties, ActionCallingContext
using Eplan.EplApi.Base;                   // Decider, MultiLangString, ISOCode, Languages
using Eplan.EplApi.DataModel;              // Project, Function, DMObjectsFinder, FunctionsFilter, Properties, PropertyValue
using Eplan.EplApi.HEServices;             // SelectionSet

namespace Eplan.EplAddIn.EngravingData
{
    // ------------------------------------------------------------------ add-in lifecycle
    public class EngravingDataAddIn : IEplAddIn
    {
        public bool OnRegister(ref bool bLoadOnStart) { bLoadOnStart = true; return true; }
        public bool OnUnregister() { return true; }
        public bool OnInit() { return true; }
        public bool OnInitGui() { return true; }
        public bool OnExit() { return true; }
    }

    // ------------------------------------------------------------------ EXPORT action
    public class EngravingExportAction : IEplAction
    {
        public const string ActionName = "EngravingDataExport";

        public bool OnRegister(ref string Name, ref int Ordinal) { Name = ActionName; Ordinal = 20; return true; }
        public void GetActionProperties(ref ActionProperties actionProperties) { }

        public bool Execute(ActionCallingContext ctx)
        {
            var log = new EngravingLog("EngravingDataExport");
            try
            {
                Project project = new SelectionSet().GetCurrentProject(true);
                string elk      = project.ProjectLinkFilePath;
                string docsPath = Path.Combine(Path.ChangeExtension(elk, ".edb"), "DOC");
                Directory.CreateDirectory(docsPath);
                string csvPath  = Path.Combine(docsPath, "EngravingData.csv");
                log.SetProject(elk);

                var csv = new StringBuilder();
                csv.AppendLine(Csv.Row("Key", "DT", "Page", "Location", "PartNumber", "FunctionText", "EngravingText"));

                var finder    = new DMObjectsFinder(project);
                Function[] fns = finder.GetFunctions(new FunctionsFilter());
                var seenKeys  = new HashSet<string>();
                int rows = 0, dupes = 0;

                foreach (Function f in fns)
                {
                    string engraving = Prop.ReadDisplay(f, Properties.Function.FUNC_GRAVINGTEXT);
                    if (string.IsNullOrEmpty(engraving)) continue;

                    string key      = Prop.Name(f);
                    if (!seenKeys.Add(key)) dupes++;
                    string dt       = Prop.VisibleName(f);
                    string page     = Prop.PageName(f);
                    string location = Prop.ReadDisplay(f, Properties.Function.FUNC_MOUNTINGLOCATION);
                    string part     = Prop.ReadIndexed(f, Properties.Function.FUNC_ARTICLE_PARTNR, 1);
                    string func     = Prop.ReadDisplay(f, Properties.Function.FUNC_TEXT);

                    csv.AppendLine(Csv.Row(key, dt, page, location, part, func, engraving));
                    rows++;
                }

                File.WriteAllText(csvPath, csv.ToString(), new UTF8Encoding(true));
                log.Line("Exported " + rows + " engraved item(s); " + dupes + " duplicate key(s).");
                log.Line("File: " + csvPath);
                log.Save();

                Msg("Engraving export\n\nExported " + rows + " engraved item(s)."
                    + (dupes > 0 ? "\nWARNING: " + dupes + " duplicate key(s)." : "")
                    + "\n\nFile:\n" + csvPath);
                return true;
            }
            catch (Exception ex)
            {
                log.Line("EXCEPTION: " + ex); log.Save();
                Msg("Engraving export FAILED:\n\n" + ex.Message);
                return false;
            }
        }

        private static void Msg(string t) { new Decider().Decide(EnumDecisionType.eOkDecision, t, "EngravingDataExport", EnumDecisionReturn.eOK, EnumDecisionReturn.eOK); }
    }

    // ------------------------------------------------------------------ IMPORT action
    public class EngravingImportAction : IEplAction
    {
        public const string ActionName = "EngravingDataImport";

        public bool OnRegister(ref string Name, ref int Ordinal) { Name = ActionName; Ordinal = 20; return true; }
        public void GetActionProperties(ref ActionProperties actionProperties) { }

        public bool Execute(ActionCallingContext ctx)
        {
            var log = new EngravingLog("EngravingDataImport");
            try
            {
                Project project = new SelectionSet().GetCurrentProject(true);   // locked for write
                string elk      = project.ProjectLinkFilePath;
                string docsPath = Path.Combine(Path.ChangeExtension(elk, ".edb"), "DOC");
                string csvPath  = Path.Combine(docsPath, "EngravingData.csv");
                log.SetProject(elk);

                if (!File.Exists(csvPath)) { Msg("No CSV found at:\n" + csvPath + "\n\nRun the export first."); return false; }

                // Parse CSV -> map Key => (FunctionText, EngravingText)
                var records = Csv.ReadFile(csvPath);   // list of string[] (already unescaped)
                var byKey   = new Dictionary<string, string[]>();
                for (int i = 1; i < records.Count; i++)   // skip header
                {
                    string[] r = records[i];
                    if (r.Length >= 7 && !string.IsNullOrEmpty(r[0])) byKey[r[0]] = r;
                }

                var finder    = new DMObjectsFinder(project);
                Function[] fns = finder.GetFunctions(new FunctionsFilter());
                int updated = 0, missing = 0;

                foreach (Function f in fns)
                {
                    string key = Prop.Name(f);
                    string[] r;
                    if (!byKey.TryGetValue(key, out r)) continue;

                    string newFunc = r[5];   // FunctionText
                    string newEng  = r[6];   // EngravingText
                    Prop.WriteMultiLang(f, Properties.Function.FUNC_TEXT, newFunc, project);
                    Prop.WriteMultiLang(f, Properties.Function.FUNC_GRAVINGTEXT, newEng, project);
                    updated++;
                    byKey.Remove(key);
                }
                missing = byKey.Count;

                log.Line("Updated " + updated + " function(s); " + missing + " CSV key(s) not found in project.");
                log.Save();
                Msg("Engraving import\n\nUpdated " + updated + " function(s)."
                    + (missing > 0 ? "\n" + missing + " CSV key(s) had no match in the project." : ""));
                return true;
            }
            catch (Exception ex)
            {
                log.Line("EXCEPTION: " + ex); log.Save();
                Msg("Engraving import FAILED:\n\n" + ex.Message);
                return false;
            }
        }

        private static void Msg(string t) { new Decider().Decide(EnumDecisionType.eOkDecision, t, "EngravingDataImport", EnumDecisionReturn.eOK, EnumDecisionReturn.eOK); }
    }

    // ------------------------------------------------------------------ property helpers
    internal static class Prop
    {
        public static string Read(Function f, AnyPropertyId id)
        {
            try { PropertyValue pv = f.Properties[id]; return pv.IsEmpty ? "" : pv.ToString(); }
            catch { return ""; }
        }
        public static string ReadIndexed(Function f, AnyPropertyId id, int index)
        {
            try { PropertyValue pv = f.Properties[id, index]; return pv.IsEmpty ? "" : pv.ToString(); }
            catch { return ""; }
        }

        // Read a MULTI-LANGUAGE property as the clean DISPLAY string for the GUI language.
        // (pv.ToString() returns EPLAN's internal serialization "lang@text;", e.g.
        // "??_??@XXXX-XX;" — not what a human wants in a CSV.) Falls back to the raw read
        // if the property turns out not to be a MultiLangString.
        public static string ReadDisplay(Function f, AnyPropertyId id)
        {
            try
            {
                PropertyValue pv = f.Properties[id];
                if (pv.IsEmpty) return "";
                MultiLangString mls = pv;                 // implicit PropertyValue -> MultiLangString
                string s = mls.GetStringToDisplay(GuiLanguage());
                return s ?? "";
            }
            catch { return Read(f, id); }
        }
        public static string Name(Function f)        { try { return f.Name; } catch { return ""; } }
        public static string VisibleName(Function f) { try { return f.VisibleName; } catch { return ""; } }
        public static string PageName(Function f)    { try { return f.Page.Name; } catch { return ""; } }

        // Write a value into a (multi-language) property. PropertyValue has no public
        // ctor; the EPLAN idiom is to Set() the live PropertyValue returned by the indexer.
        // Try MultiLangString (matches how we read, via the GUI language); fall back to a
        // plain string for non-multilang properties.
        public static void WriteMultiLang(Function f, AnyPropertyId id, string value, Project project)
        {
            value = value ?? "";
            try
            {
                var mls = new MultiLangString();
                mls.AddString(GuiLanguage(), value);
                f.Properties[id].Set(mls);
            }
            catch
            {
                f.Properties[id].Set(value);
            }
        }

        private static ISOCode.Language GuiLanguage()
        {
            try { return new Languages().GuiLanguage.GetNumber(); }
            catch { return ISOCode.Language.L_en_US; }
        }
    }

    // ------------------------------------------------------------------ CSV helpers
    internal static class Csv
    {
        public static string Row(params string[] fields)
        {
            for (int i = 0; i < fields.Length; i++) fields[i] = Field(fields[i]);
            return string.Join(",", fields);
        }

        private static string Field(string s)
        {
            if (s == null) s = "";
            s = s.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\\n");
            if (s.IndexOf('"') >= 0 || s.IndexOf(',') >= 0) s = "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        // Parse an RFC-4180-ish CSV file; decodes the literal \n token back to a newline.
        public static List<string[]> ReadFile(string path)
        {
            var rows = new List<string[]>();
            string text = File.ReadAllText(path, Encoding.UTF8);
            int i = 0, n = text.Length;
            var field = new StringBuilder();
            var record = new List<string>();
            bool inQuotes = false;

            while (i < n)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < n && text[i + 1] == '"') { field.Append('"'); i += 2; continue; }
                        inQuotes = false; i++; continue;
                    }
                    field.Append(c); i++; continue;
                }
                if (c == '"') { inQuotes = true; i++; continue; }
                if (c == ',') { record.Add(Decode(field.ToString())); field.Length = 0; i++; continue; }
                if (c == '\r') { i++; continue; }
                if (c == '\n') { record.Add(Decode(field.ToString())); field.Length = 0; rows.Add(record.ToArray()); record = new List<string>(); i++; continue; }
                field.Append(c); i++;
            }
            if (field.Length > 0 || record.Count > 0) { record.Add(Decode(field.ToString())); rows.Add(record.ToArray()); }
            return rows;
        }

        private static string Decode(string s) { return s == null ? "" : s.Replace("\\n", "\n"); }
    }

    // ------------------------------------------------------------------ file logging
    internal sealed class EngravingLog
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private readonly string _name;
        private string _projectElk;

        public EngravingLog(string name)
        {
            _name = name;
            _sb.AppendLine("=== " + name + "  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
            _sb.AppendLine("user : " + Environment.UserName + " @ " + Environment.MachineName);
        }

        public void SetProject(string elk) { _projectElk = elk; _sb.AppendLine("project : " + elk); }
        public void Line(string s) { _sb.AppendLine(s); }

        public void Save()
        {
            string dir;
            try
            {
                dir = !string.IsNullOrEmpty(_projectElk)
                    ? Path.Combine(Path.ChangeExtension(_projectElk, ".edb"), "DOC")
                    : Path.Combine(Path.GetTempPath(), "EPLAN_Scripts");
                Directory.CreateDirectory(dir);
            }
            catch { dir = Path.GetTempPath(); }
            try { File.AppendAllText(Path.Combine(dir, _name + ".log"), _sb.ToString() + Environment.NewLine, new UTF8Encoding(true)); }
            catch { }
        }
    }
}
