// ValidateApi_eBuild.cs
//
// eBuild-CONTEXT validation probe (a Script-Typical is a "simple script", so it is bound
// by the SAME assembly limits as Scripts > Run): it may use only System, System.Xml,
// System.Drawing, System.Windows.Forms, Eplan.EplApi.Base, .ApplicationFramework, .Gui,
// .Scripting. It CANNOT reference Eplan.EplApi.DataModel / .Filters / .HEServices at
// compile time, so this probe reaches them ONLY by reflection.
//
// It answers the eBuild reference doc's open questions, logged to
//   <project>.edb\DOC\ValidateApi_eBuild.log :
//   Q1  Is the DataModel assembly even LOADED during generation?
//   Q2  Can we get a Project handle (SelectionSet) and enumerate functions by reflection?
//   Q3  Does the action infrastructure (CommandLineInterpreter + return values) work?
//
// SAFE: writes nothing to the project. NO DIALOGS (would hang generation). Attach as a
// Script-Typical; ProjectName is injected — do NOT declare it as a Designer parameter.
// Bump ScriptVersion on every edit (cached compile); the log prints which copy ran.

using System.IO;
using System.Text;
using System.Reflection;

public class ValidateApiEBuild
{
    private const string ScriptVersion = "2026-06-08.2";

    [Start]
    public void RunFromEBuild(string ProjectName)
    {
        StringBuilder log = new StringBuilder();
        log.AppendLine("=== ValidateApi_eBuild " + System.DateTime.Now + " ===");
        log.AppendLine("Script version : " + ScriptVersion);
        log.AppendLine("ProjectName    : " + ProjectName);
        log.AppendLine("");

        // ---- Environment: which EPLAN assemblies are loaded during generation? -------
        foreach (Assembly asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                AssemblyName an = asm.GetName();
                if (an.Name != null && an.Name.StartsWith("Eplan.EplApi"))
                    log.AppendLine("asm : " + an.Name + "  " + an.Version);
            }
            catch { }
        }
        log.AppendLine("");
        log.AppendLine("Q1 DataModel loaded  : " + (FindType("Eplan.EplApi.DataModel.Project") != null));
        log.AppendLine("Q1 HEServices loaded : " + (FindType("Eplan.EplApi.HEServices.SelectionSet") != null));
        log.AppendLine("");

        // ---- Q2: get a Project handle + enumerate functions, all by reflection -------
        try
        {
            object project = null;
            try
            {
                object sel = New("Eplan.EplApi.HEServices.SelectionSet");
                project = Call(sel, "GetCurrentProject", new object[] { false }); // false = don't throw
                log.AppendLine("Q2 SelectionSet.GetCurrentProject(false) : " +
                    (project == null ? "NULL (no current project by selection)" : "OK -> " + PathOf(project)));
            }
            catch (System.Exception ex) { log.AppendLine("Q2 GetCurrentProject(false) EXCEPTION : " + ExMsg(ex)); }

            if (project != null)
            {
                object finder = New("Eplan.EplApi.DataModel.DMObjectsFinder", project);
                object filter = New("Eplan.EplApi.DataModel.Filters.FunctionsFilter");
                object fnsObj = Call(finder, "GetFunctions", new object[] { filter });
                System.Array fns = fnsObj as System.Array;
                log.AppendLine("Q2 DMObjectsFinder.GetFunctions : OK -> " + (fns == null ? -1 : fns.Length) + " functions");
                log.AppendLine(">>> RESULT: DataModel IS reachable by reflection in the eBuild generation context.");
            }
            else
            {
                object pm = FindType("Eplan.EplApi.DataModel.ProjectManager");
                log.AppendLine(">>> RESULT: no Project via SelectionSet in eBuild. ProjectManager present: " + (pm != null)
                    + " (could open '" + ProjectName + "' by path — not attempted: lock risk).");
            }
        }
        catch (System.Exception ex) { log.AppendLine("Q2 reflection chain EXCEPTION : " + ExMsg(ex)); }

        log.AppendLine("");

        // ---- Q3: action infrastructure + return values -------------------------------
        try
        {
            CommandLineInterpreter cli = new CommandLineInterpreter();
            ActionCallingContext acc = new ActionCallingContext();
            acc.AddParameter("TYPE", "PROJECT");
            bool ok = cli.Execute("selectionset", acc);
            string proj = "";
            try { acc.GetParameter("PROJECT", ref proj); } catch { }
            log.AppendLine("Q3 action 'selectionset' TYPE=PROJECT : ok=" + ok + " PROJECT='" + proj + "'");
        }
        catch (System.Exception ex) { log.AppendLine("Q3 action EXCEPTION : " + ExMsg(ex)); }

        log.AppendLine("");
        log.AppendLine("=== end (copy this into the eBuild reference §6/§13) ===");

        // ---- write the log (DOC, with %TEMP% fallback) ------------------------------
        string dir;
        try
        {
            dir = Path.Combine(Path.ChangeExtension(ProjectName, ".edb"), "DOC");
            Directory.CreateDirectory(dir);
        }
        catch { dir = Path.Combine(Path.GetTempPath(), "EPLAN_Scripts"); try { Directory.CreateDirectory(dir); } catch { } }

        try { File.AppendAllText(Path.Combine(dir, "ValidateApi_eBuild.log"),
                                 log.ToString() + System.Environment.NewLine, new UTF8Encoding(true)); }
        catch { }
    }

    // ===== reflection helpers =====

    private static System.Type FindType(string fullName)
    {
        foreach (Assembly asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            try { System.Type t = asm.GetType(fullName); if (t != null) return t; }
            catch { }
        }
        return null;
    }

    private static object New(string typeName, params object[] args)
    {
        System.Type t = FindType(typeName);
        if (t == null) throw new System.Exception("type not loaded: " + typeName);
        return System.Activator.CreateInstance(t, args);
    }

    private static object Call(object target, string method, object[] args)
    {
        System.Type t = target.GetType();
        int want = args == null ? 0 : args.Length;
        foreach (MethodInfo m in t.GetMethods())
        {
            if (m.Name != method) continue;
            ParameterInfo[] ps = m.GetParameters();
            if (ps.Length != want) continue;
            bool ok = true;
            for (int i = 0; i < ps.Length; i++)
                if (args[i] != null && !ps[i].ParameterType.IsInstanceOfType(args[i])) { ok = false; break; }
            if (ok) return m.Invoke(target, args);
        }
        throw new System.Exception(method + "(" + want + " args) not matched on " + t.FullName);
    }

    private static string PathOf(object project)
    {
        try
        {
            PropertyInfo pi = project.GetType().GetProperty("ProjectLinkFilePath");
            object v = pi == null ? null : pi.GetValue(project, null);
            return v == null ? "" : v.ToString();
        }
        catch (System.Exception ex) { return "EXCEPTION " + ex.Message; }
    }

    private static string ExMsg(System.Exception ex) { return ex.GetType().Name + ": " + ex.Message; }
}
