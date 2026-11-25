using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace BnbcUltimatePro
{
    /// <summary>
    /// Talks to ETABS through ETABSv1.dll, which is located in the user's own ETABS
    /// installation and loaded at runtime by reflection. No CSI binaries are redistributed.
    /// </summary>
    internal sealed class EtabsConnector
    {
        public string DllPath;
        public bool IsRunning;
        public bool Connected;
        public string Version = "";
        public string ModelFile = "";
        public string Message = "Not checked yet";

        Assembly asm;
        object model;
        Type modelI;

        public void Detect()
        {
            DllPath = null; IsRunning = false;
            try
            {
                foreach (var p in Process.GetProcessesByName("ETABS"))
                {
                    IsRunning = true;
                    try
                    {
                        var f = Path.Combine(Path.GetDirectoryName(p.MainModule.FileName), "ETABSv1.dll");
                        if (File.Exists(f)) { DllPath = f; break; }
                    }
                    catch { }
                }
            }
            catch { }

            if (DllPath == null)
            {
                var roots = new[] { Environment.GetEnvironmentVariable("ProgramFiles"), Environment.GetEnvironmentVariable("ProgramFiles(x86)") };
                foreach (var root in roots.Where(r => !string.IsNullOrEmpty(r)))
                {
                    var cs = Path.Combine(root, "Computers and Structures");
                    if (!Directory.Exists(cs)) continue;
                    foreach (var d in Directory.GetDirectories(cs, "ETABS*").OrderByDescending(x => x))
                    {
                        var f = Path.Combine(d, "ETABSv1.dll");
                        if (File.Exists(f)) { DllPath = f; break; }
                    }
                    if (DllPath != null) break;
                }
            }

            if (DllPath == null)
                Message = "ETABS installation (ETABSv1.dll) not found. Install ETABS 22 first.";
            else if (!IsRunning)
                Message = "ETABS found but not running. Open ETABS 22 and your model, then press Connect.";
            else
                Message = "ETABS is running. Press Connect.";
        }

        public bool Connect()
        {
            Connected = false; Version = ""; ModelFile = "";
            Detect();
            if (DllPath == null || !IsRunning) return false;
            try
            {
                asm = Assembly.LoadFrom(DllPath);
                var helperT = asm.GetType("ETABSv1.Helper", true);
                var helperI = asm.GetType("ETABSv1.cHelper", true);
                var helper = Activator.CreateInstance(helperT);
                var obj = helperI.GetMethod("GetObject").Invoke(helper, new object[] { "CSI.ETABS.API.ETABSObject" });
                if (obj == null) { Message = "Could not attach to the running ETABS instance."; return false; }
                var oapiI = asm.GetType("ETABSv1.cOAPI", true);
                model = oapiI.GetProperty("SapModel").GetValue(obj, null);
                modelI = asm.GetType("ETABSv1.cSapModel", true);

                var args = new object[] { null, 0.0 };
                modelI.GetMethod("GetVersion").Invoke(model, args);
                Version = (args[0] as string) ?? "";
                if (!Version.StartsWith("22"))
                {
                    Message = "API Version Mismatch: ETABS reports version '" + Version + "', this tool requires 22.x.";
                    return false;
                }
                ModelFile = (modelI.GetMethod("GetModelFilename").Invoke(model, new object[] { true }) as string) ?? "";
                Connected = true;
                Message = ModelFile.Length == 0
                    ? "Connected (ETABS " + Version + "), but no model is open. Open or create a model."
                    : "Connected to ETABS " + Version + " - model: " + ModelFile;
                return true;
            }
            catch (Exception ex)
            {
                var e = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                Message = "Connection failed: " + e.Message;
                return false;
            }
        }

        public bool HasModel { get { return Connected && ModelFile.Length > 0; } }

        object LoadPatterns()
        {
            return modelI.GetProperty("LoadPatterns").GetValue(model, null);
        }

        public List<string> GetLoadPatternNames()
        {
            var lp = LoadPatterns();
            var lpI = asm.GetType("ETABSv1.cLoadPatterns", true);
            var args = new object[] { 0, null };
            int ret = (int)lpI.GetMethod("GetNameList").Invoke(lp, args);
            if (ret != 0) throw new Exception("GetNameList returned " + ret);
            var names = args[1] as string[];
            return names == null ? new List<string>() : names.ToList();
        }

        public int AddLoadPattern(string name, string type, double selfWeight)
        {
            var lp = LoadPatterns();
            var lpI = asm.GetType("ETABSv1.cLoadPatterns", true);
            var enumT = asm.GetType("ETABSv1.eLoadPatternType", true);
            var val = Enum.Parse(enumT, type);
            return (int)lpI.GetMethod("Add").Invoke(lp, new object[] { name, val, selfWeight, true });
        }

        List<string> NameList(object target, Type iface)
        {
            var m = iface.GetMethod("GetNameList", new Type[] { typeof(int).MakeByRefType(), typeof(string[]).MakeByRefType() });
            if (m == null) return null;
            var args = new object[] { 0, null };
            int ret = (int)m.Invoke(target, args);
            if (ret != 0) return null;
            var names = args[1] as string[];
            return names == null ? new List<string>() : names.ToList();
        }

        /// <summary>Load case names (null if the list could not be read).</summary>
        public List<string> GetLoadCaseNames()
        {
            var lc = modelI.GetProperty("LoadCases").GetValue(model, null);
            return NameList(lc, asm.GetType("ETABSv1.cLoadCases", true));
        }

        public List<string> GetComboNames()
        {
            var rc = modelI.GetProperty("RespCombo").GetValue(model, null);
            return NameList(rc, asm.GetType("ETABSv1.cCombo", true)) ?? new List<string>();
        }

        public int AddCombo(string name, List<KeyValuePair<string, double>> items)
        {
            var rc = modelI.GetProperty("RespCombo").GetValue(model, null);
            var cI = asm.GetType("ETABSv1.cCombo", true);
            var enumT = asm.GetType("ETABSv1.eCNameType", true);
            int ret = (int)cI.GetMethod("Add", new Type[] { typeof(string), typeof(int) }).Invoke(rc, new object[] { name, 0 });
            if (ret != 0) return ret;
            var setM = cI.GetMethods().First(m => m.Name == "SetCaseList");
            foreach (var it in items)
            {
                var args = new object[] { name, Enum.ToObject(enumT, 0), it.Key, it.Value };
                int r = (int)setM.Invoke(rc, args);
                if (r != 0)
                {
                    try { cI.GetMethod("Delete", new Type[] { typeof(string) }).Invoke(rc, new object[] { name }); } catch { }
                    return r;
                }
            }
            return 0;
        }

        /// <summary>True if a combination with this name already exists (checked by reading its case list).</summary>
        public bool ComboExists(string name)
        {
            try
            {
                var rc = modelI.GetProperty("RespCombo").GetValue(model, null);
                var cI = asm.GetType("ETABSv1.cCombo", true);
                var m = cI.GetMethods().FirstOrDefault(x => x.Name == "GetCaseList");
                if (m == null) return false;
                var args = new object[] { name, 0, null, null, null };
                int ret = (int)m.Invoke(rc, args);
                return ret == 0 && (int)args[1] > 0;
            }
            catch { return false; }
        }

        public List<string> GetStoryNames()
        {
            var st = modelI.GetProperty("Story").GetValue(model, null);
            return NameList(st, asm.GetType("ETABSv1.cStory", true));
        }

        /// <summary>Counts the objects currently selected in ETABS (areas = type 5, frames = type 2).</summary>
        public void GetSelectionCounts(out int areas, out int frames)
        {
            areas = 0; frames = 0;
            var sel = modelI.GetProperty("SelectObj").GetValue(model, null);
            var sI = asm.GetType("ETABSv1.cSelect", true);
            var args = new object[] { 0, null, null };
            int ret = (int)sI.GetMethod("GetSelected").Invoke(sel, args);
            if (ret != 0) throw new Exception("GetSelected returned " + ret);
            var types = args[1] as int[];
            if (types == null) return;
            foreach (var t in types) { if (t == 5) areas++; else if (t == 2) frames++; }
        }

        object GetUnits() { return modelI.GetMethod("GetPresentUnits").Invoke(model, null); }
        int SetUnits(object u) { return (int)modelI.GetMethod("SetPresentUnits").Invoke(model, new object[] { u }); }

        /// <summary>Uniform gravity load (kN/m2) on the objects selected in ETABS. Replaces an existing load of that pattern.</summary>
        public int AssignAreaUniform(string pattern, double kNperM2)
        {
            var unitsT = asm.GetType("ETABSv1.eUnits", true);
            var itemT = asm.GetType("ETABSv1.eItemType", true);
            var prev = GetUnits();
            SetUnits(Enum.Parse(unitsT, "kN_m_C"));
            try
            {
                var ao = modelI.GetProperty("AreaObj").GetValue(model, null);
                var aI = asm.GetType("ETABSv1.cAreaObj", true);
                var m = aI.GetMethods().First(x => x.Name == "SetLoadUniform" && x.GetParameters().Length == 7);
                return (int)m.Invoke(ao, new object[] { "", pattern, kNperM2, 10, true, "Global", Enum.Parse(itemT, "SelectedObjects") });
            }
            finally { SetUnits(prev); }
        }

        /// <summary>Uniform gravity line load (kN/m) on the frame objects selected in ETABS. Replaces an existing load of that pattern.</summary>
        public int AssignFrameLine(string pattern, double kNperM)
        {
            var unitsT = asm.GetType("ETABSv1.eUnits", true);
            var itemT = asm.GetType("ETABSv1.eItemType", true);
            var prev = GetUnits();
            SetUnits(Enum.Parse(unitsT, "kN_m_C"));
            try
            {
                var fo = modelI.GetProperty("FrameObj").GetValue(model, null);
                var fI = asm.GetType("ETABSv1.cFrameObj", true);
                var m = fI.GetMethods().First(x => x.Name == "SetLoadDistributed" && x.GetParameters().Length == 12);
                return (int)m.Invoke(fo, new object[] { "", pattern, 1, 10, 0.0, 1.0, kNperM, kNperM, "Global", true, true, Enum.Parse(itemT, "SelectedObjects") });
            }
            finally { SetUnits(prev); }
        }

        public class ReactionRow { public string Joint { get; set; } public string Case { get; set; } public double F1 { get; set; } public double F2 { get; set; } public double F3 { get; set; } public double M1 { get; set; } public double M2 { get; set; } public double M3 { get; set; } }

        public int CountSelected(int objectType)
        {
            var sel = modelI.GetProperty("SelectObj").GetValue(model, null);
            var sI = asm.GetType("ETABSv1.cSelect", true);
            var args = new object[] { 0, null, null };
            int ret = (int)sI.GetMethod("GetSelected").Invoke(sel, args);
            if (ret != 0) throw new Exception("GetSelected returned " + ret);
            var types = args[1] as int[];
            return types == null ? 0 : types.Count(t => t == objectType);
        }

        /// <summary>Reactions (kN, kN-m) of the joints selected in ETABS for the given combinations. Analysis must have been run.</summary>
        public List<ReactionRow> GetJointReactions(List<string> combos, out int code)
        {
            var rows = new List<ReactionRow>();
            code = 0;
            var unitsT = asm.GetType("ETABSv1.eUnits", true);
            var elmT = asm.GetType("ETABSv1.eItemTypeElm", true);
            var prev = GetUnits();
            SetUnits(Enum.Parse(unitsT, "kN_m_C"));
            try
            {
                var res = modelI.GetProperty("Results").GetValue(model, null);
                var resI = asm.GetType("ETABSv1.cAnalysisResults", true);
                var setup = resI.GetProperty("Setup").GetValue(res, null);
                var setupI = asm.GetType("ETABSv1.cAnalysisResultsSetup", true);
                code = (int)setupI.GetMethod("DeselectAllCasesAndCombosForOutput").Invoke(setup, null);
                if (code != 0) return rows;
                var selM = setupI.GetMethod("SetComboSelectedForOutput", new Type[] { typeof(string), typeof(bool) });
                foreach (var c in combos)
                {
                    code = (int)selM.Invoke(setup, new object[] { c, true });
                    if (code != 0) return rows;
                }
                var jr = resI.GetMethods().First(m => m.Name == "JointReact" && m.GetParameters().Length == 14);
                var args = new object[] { "", Enum.Parse(elmT, "SelectionElm"), 0, null, null, null, null, null, null, null, null, null, null, null };
                code = (int)jr.Invoke(res, args);
                if (code != 0) return rows;
                int n = (int)args[2];
                var obj = (string[])args[3]; var lc = (string[])args[5];
                var f1 = (double[])args[8]; var f2 = (double[])args[9]; var f3 = (double[])args[10];
                var m1 = (double[])args[11]; var m2 = (double[])args[12]; var m3 = (double[])args[13];
                for (int i = 0; i < n; i++)
                    rows.Add(new ReactionRow { Joint = obj[i], Case = lc[i], F1 = f1[i], F2 = f2[i], F3 = f3[i], M1 = m1[i], M2 = m2[i], M3 = m3[i] });
                return rows;
            }
            finally { SetUnits(prev); }
        }

        /// <summary>Story names and elevations (m). Null if the list could not be read.</summary>
        public List<KeyValuePair<string, double>> GetStoryElevations()
        {
            var names = GetStoryNames();
            if (names == null) return null;
            var unitsT = asm.GetType("ETABSv1.eUnits", true);
            var prev = GetUnits();
            SetUnits(Enum.Parse(unitsT, "kN_m_C"));
            try
            {
                var st = modelI.GetProperty("Story").GetValue(model, null);
                var sI = asm.GetType("ETABSv1.cStory", true);
                var m = sI.GetMethods().First(x => x.Name == "GetElevation");
                var list = new List<KeyValuePair<string, double>>();
                foreach (var n in names)
                {
                    var args = new object[] { n, 0.0 };
                    int ret = (int)m.Invoke(st, args);
                    if (ret != 0) return null;
                    list.Add(new KeyValuePair<string, double>(n, (double)args[1]));
                }
                return list;
            }
            finally { SetUnits(prev); }
        }
    }
}
